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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

    public class ExportDataWizardViewModel : Conductor<IScreen>.Collection.OneActive, IDisposable
    {
        #region Private Fields
        Stack<IScreen> _previousPages = new Stack<IScreen>();
        #endregion

        #region Constructor
        public ExportDataWizardViewModel(IEventAggregator eventAggregator, DocumentViewModel document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            EventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            EventAggregator.Subscribe(this);

            // check connection state
            if (Document.Connection == null)
            {
                throw new ArgumentException("The current document is not connected to a data source", "Document");
            }

            if (Document.Connection.State != ConnectionState.Open)
            {
                throw new ArgumentException("The connection for the current document is not in an open state", "Document");
            }

            if (Document.Connection.Database.Models.Count == 0)
            {
                throw new ArgumentException("The connection for the current document does not have a data model", "Document");
            }

            PopulateTablesList();

            SetupWizardTransitionMap();

            ShowInitialWizardPage();
        }

        private void PopulateTablesList()
        {
            
            var tables = Document.Connection.Database.Models[Document.SelectedModel].Tables.Where(t=>t.Private == false); //exclude Private (eg Date Template) tables
            if (!tables.Any()) throw new ArgumentException("There are no visible tables to export in the current data model");

            foreach ( var t in tables)
            {
                if (t.Columns.Count > 0)
                {
                    Tables.Add(new SelectedTable(t.DaxName, t.Caption, t.IsVisible, t.Private, t.ShowAsVariationsOnly));
                }
                else
                {
                    EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"Skipping tables '{t.Caption}' as it has no columns to export"));
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


        private void ShowInitialWizardPage()
        {
            var chooseExportType = new ExportDataWizardChooseTypeViewModel(this);

            ActivateItem(chooseExportType);
        }

        #endregion

        protected override IScreen DetermineNextItemToActivate(IList<IScreen> list, int lastIndex)
        {
            var theScreenThatJustClosed = list[lastIndex] as ExportDataWizardBasePageViewModel;
            
            object nextScreen;
            if (!theScreenThatJustClosed.BackClicked)
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
        public DocumentViewModel Document { get; }

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

        public ObservableCollection<SelectedTable> Tables { get; } = new ObservableCollection<SelectedTable>();
        public TransitionMap TransitionMap { get; } = new TransitionMap();
        public bool TruncateTables { get; internal set; } = true;

        #endregion

        #region Methods
        public void Cancel()
        {
            TryClose(true);
        }

        public void Close()
        {
            TryClose(true);
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

        public void Export()
        {
            Task.Run(async () =>
            {
                Document.IsQueryRunning = true;
                try
                {
                    switch (ExportType)
                    {
                        case ExportDataType.CsvFolder:
                            ExportDataToFolder(this.CsvFolder);
                            break;
                        case ExportDataType.SqlTables:
                            await ExportDataToSQLServer(this.SqlConnectionString, this.Schema, this.TruncateTables);
                            break;
                        default:
                            throw new ArgumentException("Unknown ExportType requested");
                    }
                    
                }
                finally
                {
                    Document.IsQueryRunning = false;
                }
            })
            .ContinueWith(handleFaults, TaskContinuationOptions.OnlyOnFaulted)
            .ContinueWith(prevTask => {
                //TryClose(true);
            });


            void handleFaults(Task t)
            {
                var ex = t.Exception.GetBaseException();

                Log.Error(ex, "{class} {method} {message}", "ExportDataDialogViewModel", "Export", "Error exporting all data from model");
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error when attempting to export all data - {ex.Message}"));
            }
        }

        public bool CancelRequested { get; set; }

        private void ExportDataToFolder(string outputPath)
        {
            var metadataPane = Document.MetadataPane;
            var exceptionFound = false;


            // TODO: Use async but to be well done need to apply async on the DBCommand & DBConnection
            // TODO: Show warning message?
            if (metadataPane.SelectedModel == null)
            {
                return;
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            Document.QueryStopWatch.Start();

            var selectedTables = Tables.Where(t => t.IsSelected);
            var totalTables = selectedTables.Count();
            var tableCnt = 0;
            string decimalSep = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
            string isoDateFormat = string.Format(Constants.IsoDateMask, decimalSep);
            var encoding = new UTF8Encoding(false);

            foreach (var table in  selectedTables)
            {
                EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(table));

                var rows = 0;

                try
                {
                    table.Status = ExportStatus.Exporting;
                    var fileName = CleanNameOfIllegalChars(table.Caption);
                    
                    var csvFilePath = System.IO.Path.Combine(outputPath, $"{fileName}.csv");
                    var daxQuery = $"EVALUATE {table.DaxName}";

                    StreamWriter textWriter = null;
                    try { 
                        textWriter = new StreamWriter(csvFilePath, false, encoding);

                        using (var csvWriter = new CsvHelper.CsvWriter(textWriter, CultureInfo.InvariantCulture))
                        using (var statusMsg = new StatusBarMessage(Document, $"Exporting {table.Caption}"))
                        using (var reader = Document.Connection.ExecuteReader(daxQuery))
                        {
                            rows = 0;
                            tableCnt++;

                            // configure delimiter
                            csvWriter.Configuration.Delimiter = CsvDelimiter;

                            // output dates using ISO 8601 format
                            csvWriter.Configuration.TypeConverterOptionsCache.AddOptions(
                                typeof(DateTime),
                                new CsvHelper.TypeConversion.TypeConverterOptions() { Formats = new string[] { isoDateFormat } });

                            // Write Header
                            foreach (var colName in reader.CleanColumnNames())
                            {
                                csvWriter.WriteField(colName);
                            }

                            csvWriter.NextRecord();

                            // Write data
                            while (reader.Read())
                            {
                                for (var fieldOrdinal = 0; fieldOrdinal < reader.FieldCount; fieldOrdinal++)
                                {
                                    var fieldValue = reader[fieldOrdinal];
                                    csvWriter.WriteField(fieldValue);
                                    
                                }

                                rows++;
                                if (rows % 5000 == 0)
                                {
                                    table.RowCount = rows;
                                    new StatusBarMessage(Document, $"Exporting Table {tableCnt} of {totalTables} : {table.DaxName} ({rows:N0} rows)");
                                    Document.RefreshElapsedTime();

                                    // if cancel has been requested do not write any more records
                                    if (CancelRequested)
                                    {
                                        table.Status = ExportStatus.Cancelled;
                                        break;
                                    }
                                }
                                csvWriter.NextRecord();

                            }

                            Document.RefreshElapsedTime();
                            table.RowCount = rows;
                            EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Exported {rows:N0} rows to {table.DaxName}.csv"));

                            // if cancel has been requested do not write any more files
                            if (CancelRequested)
                            {
                                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                                break;
                            }
                        }
                    }
                    finally
                    {
                        textWriter?.Dispose();
                    }
                    table.Status = ExportStatus.Done;
                }
                catch (Exception ex)
                {
                    table.Status = ExportStatus.Error;
                    exceptionFound = true;
                    Log.Error(ex, "{class} {method} {message}", "ExportDataDialogViewModel", "ExportDataToFolder", "Error while exporting model to CSV");
                    EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Exporting '{table.DaxName}':  {ex.Message}"));
                    EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(currentTable, true));
                    continue; // skip to the next table if we have caught an exception 
                }

            }

            Document.QueryStopWatch.Stop();
            // export complete
            if (!exceptionFound)
            {
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Model Export Complete: {tableCnt} tables exported", Document.QueryStopWatch.ElapsedMilliseconds));
            }
            EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(currentTable, true));
            Document.QueryStopWatch.Reset();
        }

        private object CleanNameOfIllegalChars(string caption)
        {
            if (_illegalFileCharsRegex == null)
            {
                string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                _illegalFileCharsRegex = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            }
            string newName =  _illegalFileCharsRegex.Replace(caption, "_");
            if (newName != caption)
            {
                var warning = $"Exporting table '{caption}' as '{newName}' due to characters that are illegal in a file name.";
                Log.Warning("{class} {method} {message}", "ExportDataWizardViewModel", "CleanNameOfIllegalChars", warning);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning,warning));
            }
            return newName;
        }

        private Regex _illegalFileCharsRegex = null;
        

        private string sqlTableName = string.Empty;
        private int currentTableIdx = 0;
        private int totalTableCnt = 0;
        private SelectedTable currentTable = null;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private async Task ExportDataToSQLServer(string connStr, string schemaName, bool truncateTables)
        {
            var metadataPane = this.Document.MetadataPane;

            var builder = new SqlConnectionStringBuilder(connStr);
            builder.ApplicationName = "DAX Studio Table Export";

            currentTableIdx = 0;
            var selectedTables = Tables.Where(t => t.IsSelected);
            totalTableCnt = selectedTables.Count();

            // no tables were selected so exit here
            if (totalTableCnt == 0)
            {
                return;
            }

            // TODO: Use async but to be well done need to apply async on the DBCommand & DBConnection
            // TODO: Show warning message?
            if (metadataPane.SelectedModel == null)
            {
                return;
            }
            try
            {
                Document.QueryStopWatch.Start();
                using (var conn = new SqlConnection(builder.ToString()))
                {
                    conn.Open();

                    foreach (var table in selectedTables)
                    {
                        try
                        {
                            EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(table));

                            currentTable = table;
                            currentTable.Status = ExportStatus.Exporting;
                            currentTableIdx++;
                            var daxQuery = $"EVALUATE {table.DaxName}";

                            using (var statusMsg = new StatusBarMessage(Document, $"Exporting {table.Caption}"))
                            using (var reader = metadataPane.Connection.ExecuteReader(daxQuery))
                            {
                                sqlTableName = $"[{schemaName}].[{table.Caption}]";

                                EnsureSQLTableExists(conn, sqlTableName, reader);

                                using (var transaction = conn.BeginTransaction())
                                {
                                    if (truncateTables)
                                    {
                                        using (var cmd = new SqlCommand($"truncate table {sqlTableName}", conn))
                                        {
                                            cmd.Transaction = transaction;
                                            cmd.ExecuteNonQuery();
                                        }
                                    }

                                    var sqlBulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, transaction); //)//, transaction))
                                
                                    sqlBulkCopy.DestinationTableName = sqlTableName;
                                    sqlBulkCopy.BatchSize = 5000;
                                    sqlBulkCopy.NotifyAfter = 5000;
                                    sqlBulkCopy.SqlRowsCopied += SqlBulkCopy_SqlRowsCopied;
                                    sqlBulkCopy.EnableStreaming = true;
                                    sqlBulkCopy.WriteToServer(reader);
                                    // update the currentTable with the final rowcount
                                    currentTable.RowCount = sqlBulkCopy.RowsCopiedCount();
                                

                                    transaction.Commit();
                                    currentTable.Status = ExportStatus.Done;
                                }

                            }

                            // jump out of table loop if we have been cancelled
                            if (CancelRequested)
                            {
                                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                                break;
                            }

                            EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Exported {table.Caption} to {sqlTableName}"));
                            currentTable.Status = ExportStatus.Done;
                        }
                        catch (Exception ex)
                        {
                            currentTable.Status = ExportStatus.Error;
                            Log.Error(ex, "{class} {method} {message}", "ExportDataWizardViewModel", "ExportDataToSQLServer", ex.Message);
                            EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error exporting data to SQL Server Table: {ex.Message}"));
                            EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(currentTable, true));
                            continue; // skip to next table on error
                        }
                    }
                }
                Document.QueryStopWatch.Stop();
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Model Export Complete: {currentTableIdx} tables exported", Document.QueryStopWatch.ElapsedMilliseconds));
                EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(currentTable, true));
                Document.QueryStopWatch.Reset();
            }
            catch (Exception ex)
            {
                Document.QueryStopWatch.Stop();
                if (currentTable == null && totalTableCnt > 0) { currentTable = selectedTables.FirstOrDefault(); }
                if (currentTable != null) { currentTable.Status = ExportStatus.Error; }
                Log.Error(ex, "{class} {method} {message}", "ExportDataWizardViewModel", "ExportDataToSQLServer", ex.Message);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error exporting data to SQL Server: {ex.Message}"));
                EventAggregator.PublishOnUIThread(new ExportStatusUpdateEvent(currentTable, true));
            }
        }

        private void SqlBulkCopy_SqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            if (CancelRequested)
            {
                e.Abort = true;
                cancellationTokenSource.Cancel();
            }
            new StatusBarMessage(Document, $"Exporting Table {currentTableIdx} of {totalTableCnt} : {sqlTableName} ({e.RowsCopied:N0} rows)");
            currentTable.RowCount = e.RowsCopied;
            Document.RefreshElapsedTime();
        }

        private void EnsureSQLTableExists(SqlConnection conn, string sqlTableName, AdomdDataReader reader)
        {
            var strColumns = new StringBuilder();

            var schemaTable = reader.GetSchemaTable();

            foreach (System.Data.DataRow row in schemaTable.Rows)
            {
                var colName = row.Field<string>("ColumnName");

                var regEx = System.Text.RegularExpressions.Regex.Match(colName, @".+\[(.+)\]");

                if (regEx.Success)
                {
                    colName = regEx.Groups[1].Value;
                }
                colName.Replace('|', '_');
                var sqlType = ConvertDotNetToSQLType(row);

                strColumns.AppendLine($",[{colName}] {sqlType} NULL");
            }

            var cmdText = @"                
                declare @sqlCmd nvarchar(max)

                IF object_id(@tableName, 'U') is not null
                BEGIN
                    raiserror('Droping Table ""%s""', 1, 1, @tableName)
                    set @sqlCmd = 'drop table ' + @tableName + char(13)
                    exec sp_executesql @sqlCmd
                END

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

        private string ConvertDotNetToSQLType(System.Data.DataRow row)
        {
            var dataType = row.Field<System.Type>("DataType").ToString();

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
                    };
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

                        string columnSizeStr = "MAX";

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

                        if (!string.IsNullOrEmpty(dataTypeName) && dataTypeName.EndsWith("*money"))
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
