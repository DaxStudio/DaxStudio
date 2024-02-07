using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels.ExportDataWizard;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.ComponentModel;
using DaxStudio.UI.Converters;
using DaxStudio.Common.Extensions;

namespace DaxStudio.UI.ViewModels
{
    public enum ExportDataWizardPage
    {
        ChooseCsvFolder,
        BuildSqlConnection,
        ChooseTables,
        ExportStatus,
        Cancel,
        ManualConnectionString,
        ChoosingType
    }

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum CsvEncoding
    {
        [Description("UTF-8")]
        UTF8,
        [Description("Unicode (UTF-16)")]
        Unicode
    }

    public class ExportDataWizardViewModel : Conductor<IScreen>.Collection.OneActive, IDisposable
    {
        #region Private Fields

        readonly Stack<IScreen> _previousPages = new Stack<IScreen>();
        private string _sqlTableName = string.Empty;
        private long _sqlBatchRows;
        private int _currentTableIdx;
        private int _totalTableCnt;
        private SelectedTable _currentTable;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Regex _illegalFileCharsRegex;
        const long MaxBatchSize = 10000;
        private Stopwatch _stopwatch = new Stopwatch();

        private const string ExportCompleteMsg = "Model Export Complete: {0} tables exported";
        private const string ExportIncompleteMsg = "Model Export Incomplete: {0} of {1} tables exported (last table may be partially populated)";
        private const string ExportTableMsg = "Exported {0:N0} row{1} to {2}";
        #endregion

        #region Constructor
        public ExportDataWizardViewModel(IEventAggregator eventAggregator, IDocumentToExport document, IGlobalOptions options)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            EventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            Options = options;
            EventAggregator.SubscribeOnPublishedThread(this);

            // check connection state
            if (Document.Connection == null)
            {
                throw new ArgumentException("The current document is not connected to a data source", nameof(document));
            }

            if (!Document.Connection.IsConnected)
            {
                throw new ArgumentException("The connection for the current document is not in an open state", nameof(document));
            }

            if (Document.Connection.Database.Models.Count == 0)
            {
                throw new ArgumentException("The connection for the current document does not have a data model", nameof(document));
            }

            PopulateTablesList();

            SetupWizardTransitionMap();

            ShowInitialWizardPage();
        }

        private void PopulateTablesList()
        {
            
            var tables = Document.Connection.Database.Models[Document.Connection.SelectedModelName].Tables.Where(t=>t.Private == false).ToList(); //exclude Private (eg Date Template) tables
            if (!tables.Any()) throw new ArgumentException("There are no visible tables to export in the current data model");

            foreach ( var t in tables)
            {
                if (t.Columns.Count > 0)
                {
                    Tables.Add(new SelectedTable(t.DaxName, t.Caption, t.IsVisible, t.Private, t.ShowAsVariationsOnly));
                }
                else
                {
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Skipping tables '{t.Caption}' as it has no columns to export"));
                }
            }
        }

        private void SetupWizardTransitionMap()
        {
            TransitionMap.Add<ExportDataWizardChooseTypeViewModel, ExportDataWizardCsvFolderViewModel>(ExportDataWizardPage.ChooseCsvFolder);
            TransitionMap.Add<ExportDataWizardChooseTypeViewModel, ExportDataWizardSqlConnBuilderViewModel>(ExportDataWizardPage.BuildSqlConnection);
            TransitionMap.Add<ExportDataWizardCsvFolderViewModel, ExportDataWizardChooseTablesViewModel>(ExportDataWizardPage.ChooseTables);
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

        #endregion

        protected override IScreen DetermineNextItemToActivate(IList<IScreen> list, int lastIndex)
        {
            object nextScreen;
            if (list[lastIndex] is ExportDataWizardBasePageViewModel theScreenThatJustClosed && !theScreenThatJustClosed.BackClicked)
            {
                theScreenThatJustClosed.BackClicked = false;
                _previousPages.Push(theScreenThatJustClosed);
                var nextScreenType = TransitionMap.GetNextScreenType(theScreenThatJustClosed);
                nextScreen = Activator.CreateInstance(nextScreenType, this);
            } else
            {
                nextScreen = _previousPages.Pop();
            }
            
            return nextScreen as IScreen;
        }


        #region Properties
        public IEventAggregator EventAggregator { get; }
        public IGlobalOptions Options { get; }
        public IDocumentToExport Document { get; }

        public ExportDataType ExportType { get; set; }

        public string ServerName { get; set; } = "";
        public string Database { get; set; } = "";
        public string Schema { get; set; } = "dbo";
        public string Username { get; set; } = "";
        public SecureString SecurePassword { get; set; } = new SecureString();
        public SqlAuthenticationType AuthenticationType { get; set; } = SqlAuthenticationType.Windows;
        public string SqlConnectionString { get; set; }
        public string CsvDelimiter { get; set; } = ",";
        public bool CsvQuoteStrings { get; set; } = true;
        public string CsvFolder { get; set; } = "";
        public CsvEncoding CsvEncoding { get; set; } = CsvEncoding.UTF8;
        public ObservableCollection<SelectedTable> Tables { get; } = new ObservableCollection<SelectedTable>();
        public TransitionMap TransitionMap { get; } = new TransitionMap();
        public bool TruncateTables { get; internal set; } = true;

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
                                await ExportDataToCSV(this.CsvFolder);
                                break;
                            case ExportDataType.SqlTables:
                                await ExportDataToSQLServer(this.SqlConnectionString, this.Schema, this.TruncateTables);
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
                //.ContinueWith(HandleFaults, TaskContinuationOptions.OnlyOnFaulted);


                //void HandleFaults(Task t)
                //{
                //    if (t.Exception == null) return;
                //    var ex = t.Exception.GetBaseException();
                //    // calls HandleExceptions on each child exception in the AggregateException from the Task
                //    t.Exception.Handle(HandleExceptions);
                //}

                //bool HandleExceptions(Exception ex)
                //{
                //    Log.Error(ex, "{class} {method} {message}", "ExportDataDialogViewModel", "Export", "Error exporting all data from model");
                //    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error when attempting to export all data - {ex.Message}"));
                //    return true;
                //}
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "ExportDataDialogViewModel", "Export", "Error exporting all data from model");
                await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error when attempting to export all data - {ex.Message}"));
            }
        }

        private bool _cancelRequested;
        public bool CancelRequested { get => _cancelRequested;
            set { 
                _cancelRequested = value;
                if (_cancelRequested)  _cancellationTokenSource?.Cancel();
            } 
        }

        public async Task ExportDataToCSV(string outputPath)
        {

            var exceptionFound = false;

            // TODO: Use async but to be well done need to apply async on the DBCommand & DBConnection
            // TODO: Show warning message?
            if (string.IsNullOrEmpty(Document.Connection.SelectedModelName))
            {
                return;
            }

            var selectedTables = Tables.Where(t => t.IsSelected).ToList();
            exceptionFound = await ExportDataToCsvFilesAsync(outputPath, selectedTables);
            await EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(_currentTable, true));
            Document.QueryStopWatch.Reset();
        }

        public async Task<bool> ExportDataToCsvFilesAsync(string outputPath, List<SelectedTable> selectedTables)
        {
            var exceptionFound = false;

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            await Task.Run(() =>
            {
                Document.QueryStopWatch.Start();


                var totalTables = selectedTables.Count;
                var tableCnt = 0;
                string decimalSep = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator;
                string isoDateFormat = string.Format(Constants.IsoDateMask, decimalSep);
                Encoding encoding = new UTF8Encoding(false);
                if (CsvEncoding == CsvEncoding.Unicode) encoding = new UnicodeEncoding();

                foreach (var table in selectedTables)
                {
                    EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(table));

                    tableCnt++;
                    try
                    {
                        table.Status = ExportStatus.Exporting;
                        var fileName = CleanNameOfIllegalChars(table.Caption);

                        var csvFilePath = Path.Combine(outputPath, $"{fileName}.csv");

                        var daxRowCount = $"EVALUATE ROW(\"RowCount\", COUNTROWS( {table.DaxName} ) )";

                        // get a count of the total rows in the table
                        var connRead = Document.Connection;
                        DataTable dtRows = connRead.ExecuteDaxQueryDataTable(daxRowCount);
                        var totalRows = dtRows.Rows[0].Field<long?>(0) ?? 0;
                        table.TotalRows = totalRows;

                        StreamWriter textWriter = null;
                        try
                        {
                            textWriter = new StreamWriter(csvFilePath, false, encoding);
                            // configure csv delimiter and culture
                            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture) { Delimiter = CsvDelimiter };
                            using (var csvWriter = new CsvHelper.CsvWriter(textWriter, config))
                            using (var statusMsg = new StatusBarMessage(Document, $"Exporting {table.Caption}"))
                            {
                                for (long batchRows = 0; batchRows < totalRows; batchRows += MaxBatchSize)
                                {

                                    var daxQuery = $"EVALUATE {table.DaxName}";

                                    // if the connection supports TOPNSKIP then use that to query batches of rows
                                    if (connRead.AllFunctions.Contains("TOPNSKIP"))
                                        daxQuery = $"EVALUATE TOPNSKIP({MaxBatchSize}, {batchRows}, {table.DaxName} )";

                                    using (var reader = connRead.ExecuteReader(daxQuery, null))
                                    {
                                        var rows = 0;

                                        // output dates using ISO 8601 format
                                        csvWriter.Context.TypeConverterOptionsCache.AddOptions(
                                            typeof(DateTime),
                                            new CsvHelper.TypeConversion.TypeConverterOptions() { Formats = new[] { isoDateFormat } });

                                        // if this is the first batch of rows 
                                        if (batchRows == 0)
                                        {
                                            // Write Header
                                            foreach (var colName in reader.CleanColumnNames())
                                            {
                                                csvWriter.WriteField(colName);
                                            }

                                            csvWriter.NextRecord();
                                        }
                                        // Write data
                                        while (reader.Read())
                                        {
                                            for (var fieldOrdinal = 0; fieldOrdinal < reader.FieldCount; fieldOrdinal++)
                                            {
                                                var fieldValue = reader[fieldOrdinal];

                                                // quote all string fields
                                                if (reader.GetFieldType(fieldOrdinal) == typeof(string))
                                                    csvWriter.WriteField(
                                                        reader.IsDBNull(fieldOrdinal) ? "" : fieldValue.ToString(),
                                                        this.CsvQuoteStrings);
                                                else
                                                    csvWriter.WriteField(fieldValue);

                                            }

                                            rows++;
                                            if (rows % 5000 == 0)
                                            {
                                                table.RowCount = rows + batchRows;
                                                statusMsg.Update($"Exporting Table {tableCnt} of {totalTables} : {table.DaxName} ({rows + batchRows:N0} rows)");
                                                Document.RefreshElapsedTime();

                                                // if cancel has been requested do not write any more records
                                                if (CancelRequested)
                                                {
                                                    table.Status = ExportStatus.Cancelled;
                                                    // break out of DataReader.Read() loop
                                                    break;
                                                }
                                            }
                                            csvWriter.NextRecord();

                                        }

                                        Document.RefreshElapsedTime();
                                        table.RowCount = rows + batchRows;

                                        // if cancel has been requested do not write any more files
                                        if (CancelRequested)
                                        {
                                            EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));

                                            MarkWaitingTablesAsSkipped();

                                            // break out of foreach table loop
                                            break;
                                        }
                                    }

                                    // do not loop around if the current connection does not support TOPNSKIP
                                    if (!connRead.AllFunctions.Contains("TOPNSKIP")) break;

                                    if (CancelRequested)
                                    {
                                        MarkWaitingTablesAsSkipped();
                                        break;

                                    }
                                } // end of batch

                                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, ExportTableMsg.Format(table.RowCount, table.RowCount == 1 ? "" : "s", table.DaxName + ".csv")));

                                if (CancelRequested)
                                {
                                    MarkWaitingTablesAsSkipped();
                                    break;

                                }
                            }
                        }
                        finally
                        {
                            textWriter.Dispose();
                        }

                        table.Status = ExportStatus.Done;
                    }
                    catch (Exception ex)
                    {
                        table.Status = ExportStatus.Error;
                        exceptionFound = true;
                        Log.Error(ex, "{class} {method} {message}", nameof(ExportDataWizardViewModel), nameof(ExportDataToCSV), "Error while exporting model to CSV");
                        EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Exporting '{table.DaxName}':  {ex.Message}"));
                        EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(_currentTable, true));
                    }

                }

                Document.QueryStopWatch.Stop();

                // export complete
                if (!exceptionFound)
                {
                    if (CancelRequested)
                    {
                        var completeCnt = Tables.Count(t => t.Status == ExportStatus.Done);
                        EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, ExportIncompleteMsg.Format(completeCnt, tableCnt), Document.QueryStopWatch.ElapsedMilliseconds));
                    }
                    else
                    {
                        EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, ExportCompleteMsg.Format(tableCnt), Document.QueryStopWatch.ElapsedMilliseconds));
                    }
                }
            });
            return exceptionFound;
        }

        private object CleanNameOfIllegalChars(string caption)
        {
            if (_illegalFileCharsRegex == null)
            {
                string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                _illegalFileCharsRegex = new Regex($"[{Regex.Escape(regexSearch)}]");
            }
            string newName =  _illegalFileCharsRegex.Replace(caption, "_");
            if (newName != caption)
            {
                var warning = $"Exporting table '{caption}' as '{newName}' due to characters that are illegal in a file name.";
                Log.Warning("{class} {method} {message}", "ExportDataWizardViewModel", "CleanNameOfIllegalChars", warning);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning,warning));
            }
            return newName;
        }


        

        public async Task ExportDataToSQLServer(string connStr, string schemaName, bool truncateTables)
        {
            var metadataPane = this.Document.MetadataPane as MetadataPaneViewModel;
            _cancellationTokenSource = new CancellationTokenSource();

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

            _currentTableIdx = 0;
            var selectedTables = Tables.Where(t => t.IsSelected).ToList();
            _totalTableCnt = selectedTables.Count;

            var connRead = Document.Connection;

            // no tables were selected so exit here
            if (_totalTableCnt == 0)
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
            await ExportDataToSqlTables(schemaName, truncateTables, sqlConnStr, selectedTables, connRead);
        }

        public async Task ExportDataToSqlTables(string schemaName, bool truncateTables, string sqlConnStr, List<SelectedTable> selectedTables, IConnectionManager connRead)
        {
            try
            {
                Document.QueryStopWatch.Start();
                using (var conn = new SqlConnection(sqlConnStr))
                {
                    conn.Open();

                    foreach (var table in selectedTables)
                    {
                        try
                        {
                            await EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(table));

                            _currentTable = table;
                            _currentTable.Status = ExportStatus.Exporting;
                            _currentTableIdx++;
                            var daxRowCount = $"EVALUATE ROW(\"RowCount\", COUNTROWS( {table.DaxName} ) )";

                            // get a count of the total rows in the table
                            DataTable dtRows = connRead.ExecuteDaxQueryDataTable(daxRowCount);
                            var totalRows = dtRows.Rows[0].Field<long>(0);
                            _currentTable.TotalRows = totalRows;

                            using (var _ = new StatusBarMessage(Document, $"Exporting {table.Caption}"))
                            {

                                for (long batchRows = 0; batchRows < totalRows; batchRows += MaxBatchSize)
                                {

                                    var daxQuery = $"EVALUATE {table.DaxName}";

                                    // if the connection supports TOPNSKIP then use that to query batches of rows
                                    if (connRead.AllFunctions.Contains("TOPNSKIP"))
                                        daxQuery = $"EVALUATE TOPNSKIP({MaxBatchSize}, {batchRows}, {table.DaxName} )";

                                    using (var reader = connRead.ExecuteReader(daxQuery, null))
                                    {
                                        _sqlTableName = $"[{schemaName}].[{table.Caption}]";
                                        _sqlBatchRows = batchRows;

                                        // if this is the first batch ensure the table exists
                                        if (batchRows == 0)
                                            EnsureSQLTableExists(conn, _sqlTableName, reader, truncateTables);

                                        // if truncate tables is false we assume that this is a second run and that
                                        // the table already exists with the correct structure.

                                        using (var transaction = conn.BeginTransaction())
                                        {

                                            using (var sqlBulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, transaction))
                                            {
                                                sqlBulkCopy.DestinationTableName = _sqlTableName;
                                                sqlBulkCopy.BatchSize = 5000;
                                                sqlBulkCopy.NotifyAfter = 5000;
                                                sqlBulkCopy.SqlRowsCopied += SqlBulkCopy_SqlRowsCopied;
                                                sqlBulkCopy.EnableStreaming = true;
                                                await sqlBulkCopy.WriteToServerAsync(reader,
                                                    _cancellationTokenSource.Token);

                                                //WaitForTaskPollingForCancellation(_cancellationTokenSource, task);

                                                // update the currentTable with the final row count
                                                _currentTable.RowCount = sqlBulkCopy.RowsCopiedCount() + batchRows;

                                                if (CancelRequested)
                                                {
                                                    transaction.Rollback();
                                                    _currentTable.Status = ExportStatus.Cancelled;
                                                }
                                                else
                                                {
                                                    transaction.Commit();
                                                    if (_currentTable.RowCount >= _currentTable.TotalRows)
                                                        _currentTable.Status = ExportStatus.Done;
                                                }
                                            } // end using sqlBulkCopy
                                        } // end transaction

                                    } // end using reader

                                    // exit the loop here if the connection does not support TOPNSKIP
                                    if (!connRead.AllFunctions.Contains("TOPNSKIP")) break;
                                } // end rowBatch
                            }
                            // jump out of table loop if we have been cancelled
                            if (CancelRequested)
                            {
                                await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                                // mark an tables not yet exported as skipped
                                MarkWaitingTablesAsSkipped();

                                break;
                            }

                            await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, ExportTableMsg.Format(table.RowCount, table.RowCount == 1 ? "" : "s", _sqlTableName)));
                            _currentTable.Status = ExportStatus.Done;
                        }
                        catch (TaskCanceledException)
                        {
                            _currentTable.Status = ExportStatus.Error;
                            var msg = $"Export Operation Cancelled for table: {table.Caption}";
                            Log.Warning(Constants.LogMessageTemplate, nameof(ExportDataWizardViewModel), nameof(ExportDataToSQLServer), msg);
                            await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                        }
                        catch (InvalidOperationException ex2)
                        {
                            // we get this exception if the SQL connection is closed
                            _currentTable.Status = ExportStatus.Error;
                            var innerEx = ex2.GetLeafException();
                            var msg = $"Error exporting data from {_currentTable.DaxName} to SQL Server Table: {innerEx.Message}";
                            Log.Error(innerEx, "{class} {method} {message}", nameof(ExportDataWizardViewModel), nameof(ExportDataToSQLServer), msg);
                            await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                            await EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(_currentTable, true));
                            MarkWaitingTablesAsSkipped();
                            break;
                        }
                        catch (Exception ex)
                        {
                            _currentTable.Status = ExportStatus.Error;
                            var innerEx = ex.GetLeafException();
                            string extraMessage = string.Empty;
                            Log.Error(innerEx, "{class} {method} {message}", nameof(ExportDataWizardViewModel), nameof(ExportDataToSQLServer), innerEx.Message);
                            if (!truncateTables) extraMessage = "\nIf you are inserting into an existing table the column names, the order of the column and the datatypes must match with those in the tabular model or you may get strange errors";
                            await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error exporting data to SQL Server Table: {innerEx.Message}{extraMessage}"));
                            await EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(_currentTable, true));
                        }

                        if (CancelRequested)
                        {
                            await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                            // mark an tables not yet exported as skipped
                            MarkWaitingTablesAsSkipped();

                            break;
                        }
                    } // end foreach table
                }
                Document.QueryStopWatch.Stop();
                await EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(_currentTable, true));
                if (CancelRequested)
                {
                    var completeCnt = Tables.Count(t => t.Status == ExportStatus.Done);
                    await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, ExportIncompleteMsg.Format(completeCnt, _currentTableIdx), Document.QueryStopWatch.ElapsedMilliseconds));
                }
                else
                {
                    await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, ExportCompleteMsg.Format(_currentTableIdx), Document.QueryStopWatch.ElapsedMilliseconds));
                }

                Document.QueryStopWatch.Reset();
            }
            catch (Exception ex)
            {
                Document.QueryStopWatch.Stop();
                if (_currentTable == null && _totalTableCnt > 0) { _currentTable = selectedTables.FirstOrDefault(); }
                if (_currentTable != null) { _currentTable.Status = ExportStatus.Error; }
                Log.Error(ex, "{class} {method} {message}", nameof(ExportDataWizardViewModel), nameof(ExportDataToSQLServer), ex.Message);
                await EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error exporting data to SQL Server: {ex.Message}"));
                await EventAggregator.PublishOnUIThreadAsync(new ExportStatusUpdateEvent(_currentTable, true));
            }
            finally
            {
                Document.QueryStopWatch.Stop();
            }
        }

        private void MarkWaitingTablesAsSkipped()
        {
            foreach (var tbl in Tables)
            {
                if (tbl.Status == ExportStatus.Ready || tbl.Status != ExportStatus.Exporting)
                {
                    tbl.Status = ExportStatus.Cancelled;
                }
            }
        }

        private void WaitForTaskPollingForCancellation(CancellationTokenSource cancellationTokenSource, Task task)
        {
            // poll every 1 second to see if the Cancel button has been clicked
            while (!task.Wait(1000))
            {
                if (CancelRequested)
                {
                    Log.Information(Constants.LogMessageTemplate,nameof(ExportDataWizardViewModel), nameof(WaitForTaskPollingForCancellation), "Cancelling data export");
                    cancellationTokenSource.Cancel();
                    try
                    {
                        task.Wait();
                    }
                    catch (AggregateException ex)
                    {
                        Log.Error(ex.InnerException, Constants.LogMessageTemplate,  nameof(ExportDataWizardViewModel), nameof( WaitForTaskPollingForCancellation), "Error during task cancellation");
                        
                        break;
                    }
                }
                if (task.IsCompleted || task.IsCanceled || task.IsFaulted) { break;  }
            }
        }

        private void SqlBulkCopy_SqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            //if (CancelRequested)
            //{
            //    e.Abort = true;
            //    cancellationTokenSource.Cancel();
            //}
            var _ = new StatusBarMessage(Document, $"Exporting Table {_currentTableIdx} of {_totalTableCnt} : {_sqlTableName} ({(e.RowsCopied + _sqlBatchRows ):N0} rows)");
            _currentTable.RowCount = e.RowsCopied + _sqlBatchRows;
            Document.RefreshElapsedTime();
        }

        private void EnsureSQLTableExists(SqlConnection conn, string sqlTableName, AdomdDataReader reader, bool truncateTable)
        {
            var strColumns = new StringBuilder();

            var schemaTable = reader.GetSchemaTable();

            if (schemaTable != null)
                foreach (DataRow row in schemaTable.Rows)
                {
                    var colName = row.Field<string>("ColumnName");

                    var regEx = Regex.Match(colName, @"[^\[]+\[(.+)\]");

                    if (regEx.Success)
                    {
                        colName = regEx.Groups[1].Value;
                    }

                    var fixedName = colName
                                    .Replace('|', '_')
                                    .Replace("]","]]");
                    
                    var sqlType = ConvertDotNetToSQLType(row);

                    strColumns.AppendLine($",[{fixedName}] {sqlType} NULL");
                }

            // ReSharper disable once StringLiteralTypo
            var cmdText = @"                
                declare @sqlCmd nvarchar(max)";

            if (truncateTable)
            {
                cmdText += @"

                IF object_id(@tableName, 'U') is not null
                BEGIN
                    raiserror('Droping Table ""%s""', 1, 1, @tableName)
                    set @sqlCmd = 'drop table ' + @tableName + char(13)
                    exec sp_executesql @sqlCmd
                END";
            }

            cmdText += @"

                IF object_id(@tableName, 'U') is null
                BEGIN
                    declare @schemaName varchar(20)
		            set @sqlCmd = ''
                    set @schemaName = parsename(@tableName, 2)

                    IF NOT EXISTS(SELECT * FROM sys.schemas WHERE name = @schemaName)
                    BEGIN
                        set @sqlCmd = 'CREATE SCHEMA ' + @schemaName + char(13)
                    END

                    set @sqlCmd = @sqlCmd + 'CREATE TABLE ' + @tableName + '(' + @columns + ');'

                    raiserror('Creating Table ""%s""', 1, 1, @tableName)

                    exec sp_executesql @sqlCmd
                END
                ELSE
                BEGIN
                    raiserror('Table ""%s"" already exists', 1, 1, @tableName)
                END
                ";

            using (var cmd = new SqlCommand(cmdText, conn))
            {
                cmd.Parameters.AddWithValue("@tableName", sqlTableName);
                cmd.Parameters.AddWithValue("@columns", strColumns.ToString().TrimStart(','));

                cmd.ExecuteNonQuery();
            }
        }

        private string ConvertDotNetToSQLType(DataRow row)
        {
            var dataType = row.Field<Type>("DataType").ToString();

            string dataTypeName = null;

            if (row.Table.Columns.Contains("DataTypeName"))
            {
                dataTypeName = row.Field<string>("DataTypeName");
            }

            switch (dataType)
            {
                case "System.Double":
                    {
                        return "float";
                    }
                case "System.Boolean":
                    {
                        return "bit";
                    }
                case "System.String":
                    {
                        var columnSize = row.Field<int?>("ColumnSize");

                        if (string.IsNullOrEmpty(dataTypeName))
                        {
                            dataTypeName = "nvarchar";
                        }

                        string columnSizeStr;

                        if (columnSize == null || columnSize <= 0 || (dataTypeName == "varchar" && columnSize > 8000) || (dataTypeName == "nvarchar" && columnSize > 4000))
                        {
                            columnSizeStr = "MAX";
                        }
                        else
                        {
                            columnSizeStr = columnSize.ToString();
                        }

                        return $"{dataTypeName}({columnSizeStr})";
                    }
                case "System.Decimal":
                    {
                        var numericScale = row.Field<int>("NumericScale");
                        var numericPrecision = row.Field<int>("NumericPrecision");

                        if (numericScale == 0)
                        {
                            if (numericPrecision < 10)
                            {
                                return "int";
                            }
                            else
                            {
                                return "bigint";
                            }
                        }

                        if (!string.IsNullOrEmpty(dataTypeName) && dataTypeName.EndsWith("*money",StringComparison.OrdinalIgnoreCase))
                        {
                            return dataTypeName;
                        }

                        if (numericScale != 255)
                        {
                            return $"decimal({numericPrecision}, {numericScale})";
                        }

                        return "decimal(38,4)";
                    }
                case "System.Byte":
                    {
                        return "tinyint";
                    }
                case "System.Int16":
                    {
                        return "smallint";
                    }
                case "System.Int32":
                    {
                        return "int";
                    }
                case "System.Int64":
                    {
                        return "bigint";
                    }
                case "System.DateTime":
                    {
                        return "datetime2(0)";
                    }
                case "System.Byte[]":
                    {
                        return "varbinary(max)";
                    }
                case "System.Xml.XmlDocument":
                    {
                        return "xml";
                    }
                default:
                    {
                        return "nvarchar(MAX)";
                    }
            }
        }
        #endregion

    }
}
