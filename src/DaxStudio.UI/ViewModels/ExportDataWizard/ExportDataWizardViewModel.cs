using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Core.Events;
using DaxStudio.Core.Exports;
using DaxStudio.Core.Extensions;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.ViewModels.ExportDataWizard;
using Microsoft.Data.SqlClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    public enum ExportDataWizardPage
    {
        ChooseCsvFolder,
        ChooseParquetFolder,
        BuildSqlConnection,
        ChooseTables,
        ExportStatus,
        Cancel,
        ManualConnectionString,
        ChoosingType
    }

    // UI-facing wrapper around the headless DaxStudio.Core.Exports.ExportDataWizardModel.
    // The Conductor wizard navigation lives here; the actual CSV / Parquet / SQL export
    // work is delegated to the Core model so DaxStudio.CommandLine can use it without a
    // reference to DaxStudio.UI.
    public class ExportDataWizardViewModel : Conductor<IScreen>.Collection.OneActive, IDisposable, IExportDataDetails
    {

        readonly Stack<IScreen> _previousPages = new Stack<IScreen>();
        private readonly ExportDataWizardModel _model;
        private Stopwatch _stopwatch = new Stopwatch();

        public ExportDataWizardViewModel(IEventAggregator eventAggregator, IDocumentToExport document, IGlobalOptions options)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (eventAggregator == null) throw new ArgumentNullException(nameof(eventAggregator));

            _model = new ExportDataWizardModel(eventAggregator, document);
            EventAggregator = eventAggregator;
            Options = options;
            EventAggregator.SubscribeOnUIThread(this);

            // check connection state
            if (document.Connection == null)
            {
                throw new ArgumentException("The current document is not connected to a data source", nameof(document));
            }

            if (!document.Connection.IsConnected)
            {
                throw new ArgumentException("The connection for the current document is not in an open state", nameof(document));
            }

            if (document.Connection.Database.Models.Count == 0)
            {
                throw new ArgumentException("The connection for the current document does not have a data model", nameof(document));
            }

            PopulateTablesList();

            SetupWizardTransitionMap();

            ShowInitialWizardPage();
        }

        private void PopulateTablesList()
        {

            var tables = Document.Connection.Database.Models[Document.Connection.SelectedModelName].Tables.Where(t => t.Private == false).ToList(); //exclude Private (eg Date Template) tables
            if (!tables.Any()) throw new ArgumentException("There are no visible tables to export in the current data model");

            foreach (var t in tables)
            {
                if (t.Columns.Count > 0)
                {
                    Tables.Add(new SelectedTable(t.DaxName, t.Caption, t.IsVisible, t.Private, t.ShowAsVariationsOnly));
                }
                else
                {
                    EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, $"Skipping tables '{t.Caption}' as it has no columns to export"));
                }
            }
        }

        private void SetupWizardTransitionMap()
        {
            TransitionMap.Add<ExportDataWizardChooseTypeViewModel, ExportDataWizardCsvFolderViewModel>(ExportDataWizardPage.ChooseCsvFolder);
            TransitionMap.Add<ExportDataWizardChooseTypeViewModel, ExportDataWizardSqlConnBuilderViewModel>(ExportDataWizardPage.BuildSqlConnection);
            TransitionMap.Add<ExportDataWizardChooseTypeViewModel, ExportDataWizardOutputFolderViewModel>(ExportDataWizardPage.ChooseParquetFolder);
            TransitionMap.Add<ExportDataWizardCsvFolderViewModel, ExportDataWizardChooseTablesViewModel>(ExportDataWizardPage.ChooseTables);
            TransitionMap.Add<ExportDataWizardOutputFolderViewModel, ExportDataWizardChooseTablesViewModel>(ExportDataWizardPage.ChooseTables);
            TransitionMap.Add<ExportDataWizardSqlConnBuilderViewModel, ExportDataWizardSqlConnStrViewModel>(ExportDataWizardPage.ManualConnectionString);
            TransitionMap.Add<ExportDataWizardSqlConnBuilderViewModel, ExportDataWizardChooseTablesViewModel>(ExportDataWizardPage.ChooseTables);
            TransitionMap.Add<ExportDataWizardSqlConnStrViewModel, ExportDataWizardChooseTablesViewModel>(ExportDataWizardPage.ChooseTables);
            TransitionMap.Add<ExportDataWizardChooseTablesViewModel, ExportDataWizardExportStatusViewModel>(ExportDataWizardPage.ExportStatus);
        }


        private async void ShowInitialWizardPage()
        {
            var chooseExportType = new ExportDataWizardChooseTypeViewModel(this);

            await ActivateItemAsync(chooseExportType);
        }



        protected override IScreen DetermineNextItemToActivate(IList<IScreen> list, int lastIndex)
        {
            object nextScreen;
            if (list[lastIndex] is ExportDataWizardBasePageViewModel theScreenThatJustClosed && !theScreenThatJustClosed.BackClicked)
            {
                theScreenThatJustClosed.BackClicked = false;
                _previousPages.Push(theScreenThatJustClosed);
                var nextScreenType = TransitionMap.GetNextScreenType(theScreenThatJustClosed);
                nextScreen = Activator.CreateInstance(nextScreenType, this);
            }
            else
            {
                nextScreen = _previousPages.Pop();
            }

            return nextScreen as IScreen;
        }


        #region Properties
        public IEventAggregator EventAggregator { get; }
        public IGlobalOptions Options { get; }
        public IDocumentToExport Document => _model.Document;

        public ExportDataType ExportType { get; set; }

        public string ServerName { get; set; } = "";
        public string Database { get; set; } = "";
        public string Schema { get; set; } = "dbo";
        public string Username { get; set; } = "";
        public SecureString SecurePassword { get; set; } = new SecureString();
        public SqlAuthenticationType AuthenticationType { get; set; } = SqlAuthenticationType.Windows;
        public string SqlConnectionString { get; set; }

        // Properties forwarded to the headless export model so XAML bindings on the
        // wizard page VMs continue to work unchanged while the actual export logic
        // reads them off the model.
        public bool TrustServerCertificate
        {
            get => _model.TrustServerCertificate;
            set => _model.TrustServerCertificate = value;
        }
        public string CsvDelimiter
        {
            get => _model.CsvDelimiter;
            set => _model.CsvDelimiter = value;
        }
        public bool CsvQuoteStrings
        {
            get => _model.CsvQuoteStrings;
            set => _model.CsvQuoteStrings = value;
        }
        public CsvEncoding CsvEncoding
        {
            get => _model.CsvEncoding;
            set => _model.CsvEncoding = value;
        }

        public string OutputFolder { get; set; } = "";
        public ObservableCollection<SelectedTable> Tables
        {
            get => _model.Tables;
            set => _model.Tables = value;
        }
        public TransitionMap TransitionMap { get; } = new TransitionMap();
        public bool TruncateTables { get; set; } = true;

        public bool CancelRequested
        {
            get => _model.CancelRequested;
            set => _model.CancelRequested = value;
        }

        #endregion

        #region Methods
        public void Cancel()
        {
            //await TryCloseAsync(true);
        }

        public async void Close()
        {
            await TryCloseAsync(true);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                EventAggregator.Unsubscribe(this);
                SecurePassword?.Dispose();
                _model?.Dispose();
            }
        }
        #endregion

        #region Export Code

        public async void Export()
        {
            try
            {
                _stopwatch.Reset();
                _stopwatch.Start();
                await Task.Run(async () =>
                {
                    Document.IsQueryRunning = true;
                    try
                    {
                        switch (ExportType)
                        {
                            case ExportDataType.CsvFolder:
                                await ExportDataToCSV(this.OutputFolder);
                                break;
                            case ExportDataType.SqlTables:
                                await ExportDataToSQLServer(this.SqlConnectionString, this.Schema, this.TruncateTables);
                                break;
                            case ExportDataType.ParquetFolder:
                                await ExportDataToParquet(this.OutputFolder);
                                break;
                            default:
                                throw new ArgumentException("Unknown ExportType requested");
                        }
                        _stopwatch.Stop();
                        Document.OutputMessage("Data Export Complete", _stopwatch.ElapsedMilliseconds);
                    }
                    finally
                    {
                        Document.IsQueryRunning = false;
                        if (_stopwatch.IsRunning) _stopwatch.Stop();

                        Options.PlayLongOperationSound((int)(_stopwatch.ElapsedMilliseconds / 1000));

                    }
                });

            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(ExportDataWizardViewModel), nameof(Export), "Error exporting all data from model");
                await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error when attempting to export all data - {ex.Message}"));
            }
        }

        private async Task ExportDataToParquet(string outputPath)
        {
            if (string.IsNullOrEmpty(Document.Connection.SelectedModelName))
            {
                return;
            }

            var selectedTables = Tables.Where(t => t.IsSelected).ToList();
            await _model.ExportDataToParquetFilesAsync(outputPath, selectedTables);
            await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(selectedTables.LastOrDefault(), true));
            Document.QueryStopWatch.Reset();
        }

        public Task<bool> ExportDataToParquetFilesAsync(string outputPath, List<SelectedTable> selectedTables)
            => _model.ExportDataToParquetFilesAsync(outputPath, selectedTables);

        public async Task ExportDataToCSV(string outputPath)
        {
            // TODO: Use async but to be well done need to apply async on the DBCommand & DBConnection
            // TODO: Show warning message?
            if (string.IsNullOrEmpty(Document.Connection.SelectedModelName))
            {
                return;
            }

            var selectedTables = Tables.Where(t => t.IsSelected).ToList();
            await _model.ExportDataToCsvFilesAsync(outputPath, selectedTables);
            await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(selectedTables.LastOrDefault(), true));
            Document.QueryStopWatch.Reset();
        }

        public Task<bool> ExportDataToCsvFilesAsync(string outputPath, List<SelectedTable> selectedTables)
            => _model.ExportDataToCsvFilesAsync(outputPath, selectedTables);

        public async Task ExportDataToSQLServer(string connStr, string schemaName, bool truncateTables)
        {
            var metadataPane = this.Document.MetadataPane as MetadataPaneViewModel;

            SqlConnectionStringBuilder builder;
            try
            {
                builder = new SqlConnectionStringBuilder(connStr);
            }
            catch (ArgumentException ex)
            {
                // wrap this exception and include the connection string that we could not parse
                throw new ArgumentException($"Error parsing connections string: {connStr} - {ex.Message}", ex);
            }

            builder.ApplicationName = "DAX Studio Table Export";

            var selectedTables = Tables.Where(t => t.IsSelected).ToList();

            // no tables were selected so exit here
            if (selectedTables.Count == 0)
            {
                return;
            }

            // TODO: Use async but to be well done need to apply async on the DBCommand & DBConnection
            // TODO: Show warning message?

            if (metadataPane?.SelectedModel == null)
            {
                return;
            }
            var sqlConnStr = builder.ToString();
            await _model.ExportDataToSqlTables(schemaName, truncateTables, sqlConnStr, selectedTables, Document.Connection);
        }

        public Task ExportDataToSqlTables(string schemaName, bool truncateTables, string sqlConnStr, List<SelectedTable> selectedTables, IConnectionManager connRead)
            => _model.ExportDataToSqlTables(schemaName, truncateTables, sqlConnStr, selectedTables, connRead);

        #endregion

    }
}
