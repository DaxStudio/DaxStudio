using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.Core.Events;
using DaxStudio.Core.Extensions;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using Microsoft.Data.SqlClient;
using Parquet;
using Parquet.Schema;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.Exports
{
    // Headless export engine extracted from the DaxStudio.UI ExportDataWizardViewModel so that
    // DaxStudio.CommandLine can drive CSV / Parquet / SQL exports without a reference to UI.
    // The UI ExportDataWizardViewModel keeps the Caliburn Conductor wizard navigation and
    // delegates the actual export work here via composition.
    public class ExportDataWizardModel : PropertyChangedBase, IDisposable
    {
        private string _sqlTableName = string.Empty;
        private long _sqlBatchRows;
        private int _currentTableIdx;
        private int _totalTableCnt;
        private SelectedTable _currentTable;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Regex _illegalFileCharsRegex;
        private const long MaxBatchSize = 10000;
        private const int ParquetMaxBatchSize = 100000;
        private const int bufferRowCount = 1000000;

        private const string ExportCompleteMsg = "Model Export Complete: {0} tables exported";
        private const string ExportIncompleteMsg = "Model Export Incomplete: {0} of {1} tables exported (last table may be partially populated)";
        private const string ExportTableMsg = "Exported {0:N0} row{1} to {2}";

        public ExportDataWizardModel(IEventAggregator eventAggregator, IDocumentToExport document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            EventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        public IEventAggregator EventAggregator { get; }
        public IDocumentToExport Document { get; }

        public string CsvDelimiter { get; set; } = ",";
        public bool CsvQuoteStrings { get; set; } = true;
        public CsvEncoding CsvEncoding { get; set; } = CsvEncoding.UTF8;
        public bool TrustServerCertificate { get; set; } = true;
        public ObservableCollection<SelectedTable> Tables { get; set; } = new ObservableCollection<SelectedTable>();

        private bool _cancelRequested;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set
            {
                _cancelRequested = value;
                if (_cancelRequested) _cancellationTokenSource?.Cancel();
            }
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
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        #region Parquet Export

        public async Task<bool> ExportDataToParquetFilesAsync(string outputPath, List<SelectedTable> selectedTables)
        {
            var exceptionFound = false;

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            await Task.Run(async () =>
            {
                Document.QueryStopWatch.Start();


                var totalTables = selectedTables.Count;
                var tableCnt = 0;


                foreach (var table in selectedTables)
                {
                    await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(table));

                    tableCnt++;
                    try
                    {
                        table.Status = ExportStatus.Exporting;
                        var fileName = CleanNameOfIllegalChars(table.Caption);

                        var parquetFilePath = Path.Combine(outputPath, $"{fileName}.parquet");

                        var daxRowCount = $"EVALUATE ROW(\"RowCount\", COUNTROWS( {table.DaxName} ) )";

                        // get a count of the total rows in the table
                        var connRead = Document.Connection;
                        using var reader = connRead.ExecuteReader(daxRowCount, null);
                        {
                            // read total rows in table
                            reader.Read();
                            var totalRows = reader.GetInt64(0);
                            table.TotalRows = totalRows;
                            reader.Close();
                        }

                        using (Stream fileStream = new FileStream(parquetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                        using (var statusMsg = new StatusBarMessage(Document, $"Exporting {table.Caption}"))
                        {
                            bool flowControl = await ExportTableToParquetAsync(totalTables, tableCnt, table, connRead, fileStream, statusMsg);
                            if (!flowControl)
                            {
                                break;
                            }
                        }

                        table.Status = ExportStatus.Done;
                    }
                    catch (Exception ex)
                    {
                        table.Status = ExportStatus.Error;
                        exceptionFound = true;
                        Log.Error(ex, "{class} {method} {message}", nameof(ExportDataWizardModel), nameof(ExportDataToParquetFilesAsync), "Error while exporting model to parquet");
                        await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error Exporting '{table.DaxName}':  {ex.Message}"));
                        await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(_currentTable, true));
                    }

                }

                Document.QueryStopWatch.Stop();

                // export complete
                if (!exceptionFound)
                {
                    if (CancelRequested)
                    {
                        var completeCnt = Tables.Count(t => t.Status == ExportStatus.Done);
                        await EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, ExportIncompleteMsg.Format(completeCnt, tableCnt), Document.QueryStopWatch.ElapsedMilliseconds));
                    }
                    else
                    {
                        await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, ExportCompleteMsg.Format(tableCnt), Document.QueryStopWatch.ElapsedMilliseconds));
                    }
                }
            });
            return exceptionFound;
        }

        private async Task<bool> ExportTableToParquetAsync(int totalTables, int tableCnt, SelectedTable table, IConnectionManager connRead, Stream fileStream, StatusBarMessage statusMsg)
        {

            List<DataField> fields = null;
            List<List<object>> rowBuffer = null;
            ParquetSchema parquetSchema = null;

            using (var schemaReader = connRead.ExecuteReader($"EVALUATE TOPN(0,{table.DaxName})", null))
            {
                fields = ParquetExporter.CreateDataFieldsFromReader(schemaReader);
                parquetSchema = new ParquetSchema(fields);
                rowBuffer = ParquetExporter.ResetBuffers(fields, bufferRowCount);
            }


            using (var parquetWriter = await ParquetWriter.CreateAsync(parquetSchema, fileStream))
            {
                for (long batchRows = 0; batchRows < table.TotalRows; batchRows += MaxBatchSize)
                {

                    var daxQuery = $"EVALUATE {table.DaxName}";

                    // if the connection supports TOPNSKIP then use that to query batches of rows
                    if (connRead.AllFunctions.Contains("TOPNSKIP"))
                        daxQuery = $"EVALUATE TOPNSKIP({MaxBatchSize}, {batchRows}, {table.DaxName} )";

                    Action<string> updateStatus = (s) => statusMsg.Update(s);
                    Action<long, bool> updateProgress = (rowCount, isCancelled) =>
                    {
                        table.RowCount = rowCount + batchRows;
                        table.Status = isCancelled ? ExportStatus.Cancelled : ExportStatus.Exporting;
                        statusMsg.Update($"Exporting Table {tableCnt} of {totalTables} : {table.DaxName} ({rowCount + batchRows:N0} rows)");
                        Document.RefreshElapsedTime();
                    };
                    Func<bool> isCancelRequested = () => CancelRequested;


                    using (var reader = connRead.ExecuteReader(daxQuery, null))
                    {

                        // Write data
                        await ParquetExporter.ExportDataReaderToBuffersAsync(parquetWriter, reader, updateStatus, updateProgress, isCancelRequested, rowBuffer, fields);

                        if (table.RowCount % bufferRowCount == 0 || table.RowCount == table.TotalRows)
                        {
                            await ParquetExporter.WriteRowGroupToParquet(parquetWriter, rowBuffer, fields);
                            rowBuffer = ParquetExporter.ResetBuffers(fields, bufferRowCount);
                        }

                        Document.RefreshElapsedTime();


                        // if cancel has been requested do not write any more files
                        if (CancelRequested)
                        {
                            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                            table.Status = ExportStatus.Cancelled;
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
                        table.Status = ExportStatus.Cancelled;
                        break;

                    }
                } // end of batch
            }

            rowBuffer = ParquetExporter.ResetBuffers(fields, bufferRowCount);

            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, ExportTableMsg.Format(table.RowCount, table.RowCount == 1 ? "" : "s", table.DaxName + ".parquet")));

            if (CancelRequested)
            {
                MarkWaitingTablesAsSkipped();
                return false;

            }


            return true;
        }

        #endregion

        #region CSV Export

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
                    EventAggregator.PublishAsync(new ExportStatusUpdateEvent(table));

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
                                            EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                                            table.Status = ExportStatus.Cancelled;
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

                                EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, ExportTableMsg.Format(table.RowCount, table.RowCount == 1 ? "" : "s", table.DaxName + ".csv")));

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
                        Log.Error(ex, "{class} {method} {message}", nameof(ExportDataWizardModel), nameof(ExportDataToCsvFilesAsync), "Error while exporting model to CSV");
                        EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error Exporting '{table.DaxName}':  {ex.Message}"));
                        EventAggregator.PublishAsync(new ExportStatusUpdateEvent(_currentTable, true));
                    }

                }

                Document.QueryStopWatch.Stop();

                // export complete
                if (!exceptionFound)
                {
                    if (CancelRequested)
                    {
                        var completeCnt = Tables.Count(t => t.Status == ExportStatus.Done);
                        EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, ExportIncompleteMsg.Format(completeCnt, tableCnt), Document.QueryStopWatch.ElapsedMilliseconds));
                    }
                    else
                    {
                        EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, ExportCompleteMsg.Format(tableCnt), Document.QueryStopWatch.ElapsedMilliseconds));
                    }
                }
            });
            return exceptionFound;
        }

        private string CleanNameOfIllegalChars(string caption)
        {
            if (_illegalFileCharsRegex == null)
            {
                string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                _illegalFileCharsRegex = new Regex($"[{Regex.Escape(regexSearch)}]");
            }
            string newName = _illegalFileCharsRegex.Replace(caption, "_");
            if (newName != caption)
            {
                var warning = $"Exporting table '{caption}' as '{newName}' due to characters that are illegal in a file name.";
                Log.Warning("{class} {method} {message}", nameof(ExportDataWizardModel), nameof(CleanNameOfIllegalChars), warning);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, warning));
            }
            return newName;
        }

        #endregion

        #region SQL Export

        public async Task ExportDataToSqlTables(string schemaName, bool truncateTables, string sqlConnStr, List<SelectedTable> selectedTables, IConnectionManager connRead)
        {
            try
            {
                // If the supplied connection string did not explicitly set TrustServerCertificate,
                // apply the wizard option. Microsoft.Data.SqlClient defaults Encrypt=true so connecting
                // to servers with self-signed certificates (e.g. default SQL Server 2022/2025 installs)
                // will otherwise fail with "The certificate chain was issued by an authority that is not trusted".
                if (TrustServerCertificate)
                {
                    var connBuilder = new SqlConnectionStringBuilder(sqlConnStr);
                    if (!connBuilder.ContainsKey("TrustServerCertificate"))
                    {
                        connBuilder.TrustServerCertificate = true;
                        sqlConnStr = connBuilder.ConnectionString;
                    }
                }

                _currentTableIdx = 0;
                _totalTableCnt = selectedTables.Count;

                Document.QueryStopWatch.Start();
                using (var conn = new SqlConnection(sqlConnStr))
                {
                    conn.Open();

                    foreach (var table in selectedTables)
                    {
                        try
                        {
                            await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(table));

                            _currentTable = table;
                            _currentTable.Status = ExportStatus.Exporting;
                            _currentTableIdx++;
                            var daxRowCount = $"EVALUATE ROW(\"RowCount\", COUNTROWS( {table.DaxName} ) )";

                            // get a count of the total rows in the table
                            DataTable dtRows = connRead.ExecuteDaxQueryDataTable(daxRowCount);
                            var totalRows = dtRows.Rows[0].Field<long>(0);
                            _currentTable.TotalRows = totalRows;

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
                                                sqlBulkCopy.SqlRowsCopied += (sender, e) =>
                                                {
                                                    if (CancelRequested)
                                                    {
                                                        e.Abort = true;
                                                    }
                                                    statusMsg.Update($"Exporting Table {_currentTableIdx} of {_totalTableCnt} : {_sqlTableName} ({(e.RowsCopied + _sqlBatchRows):N0} rows)");
                                                    _currentTable.RowCount = e.RowsCopied + _sqlBatchRows;
                                                    Document.RefreshElapsedTime();
                                                };
                                                sqlBulkCopy.EnableStreaming = true;
                                                await sqlBulkCopy.WriteToServerAsync(reader);

                                                // update the currentTable with the final row count
                                                _currentTable.RowCount = sqlBulkCopy.RowsCopied64 + batchRows;

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
                                await EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                                // mark an tables not yet exported as skipped
                                MarkWaitingTablesAsSkipped();

                                break;
                            }

                            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, ExportTableMsg.Format(table.RowCount, table.RowCount == 1 ? "" : "s", _sqlTableName)));
                            _currentTable.Status = ExportStatus.Done;
                        }
                        catch (TaskCanceledException)
                        {
                            _currentTable.Status = ExportStatus.Error;
                            var msg = $"Export Operation Cancelled for table: {table.Caption}";
                            Log.Warning(Constants.LogMessageTemplate, nameof(ExportDataWizardModel), nameof(ExportDataToSqlTables), msg);
                            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, msg));
                        }
                        catch (InvalidOperationException ex2)
                        {
                            // we get this exception if the SQL connection is closed
                            _currentTable.Status = ExportStatus.Error;
                            var innerEx = ex2.GetLeafException();
                            var msg = $"Error exporting data from {_currentTable.DaxName} to SQL Server Table: {innerEx.Message}";
                            Log.Error(innerEx, "{class} {method} {message}", nameof(ExportDataWizardModel), nameof(ExportDataToSqlTables), msg);
                            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, msg));
                            await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(_currentTable, true));
                            MarkWaitingTablesAsSkipped();
                            break;
                        }
                        catch (Exception ex)
                        {
                            _currentTable.Status = ExportStatus.Error;
                            var innerEx = ex.GetLeafException();
                            string extraMessage = string.Empty;
                            Log.Error(innerEx, "{class} {method} {message}", nameof(ExportDataWizardModel), nameof(ExportDataToSqlTables), innerEx.Message);
                            if (!truncateTables) extraMessage = "\nIf you are inserting into an existing table the column names, the order of the column and the datatypes must match with those in the tabular model or you may get strange errors";
                            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error exporting data to SQL Server Table: {innerEx.Message}{extraMessage}"));
                            await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(_currentTable, true));
                        }

                        if (CancelRequested)
                        {
                            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Data Export Cancelled"));
                            // mark an tables not yet exported as skipped
                            MarkWaitingTablesAsSkipped();

                            break;
                        }
                    } // end foreach table
                }
                Document.QueryStopWatch.Stop();
                await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(_currentTable, true));
                if (CancelRequested)
                {
                    var completeCnt = Tables.Count(t => t.Status == ExportStatus.Done);
                    await EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, ExportIncompleteMsg.Format(completeCnt, _currentTableIdx), Document.QueryStopWatch.ElapsedMilliseconds));
                }
                else
                {
                    await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, ExportCompleteMsg.Format(_currentTableIdx), Document.QueryStopWatch.ElapsedMilliseconds));
                }

                Document.QueryStopWatch.Reset();
            }
            catch (Exception ex)
            {
                Document.QueryStopWatch.Stop();
                if (_currentTable == null && _totalTableCnt > 0) { _currentTable = selectedTables.FirstOrDefault(); }
                if (_currentTable != null) { _currentTable.Status = ExportStatus.Error; }
                Log.Error(ex, "{class} {method} {message}", nameof(ExportDataWizardModel), nameof(ExportDataToSqlTables), ex.Message);
                await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error exporting data to SQL Server: {ex.Message}"));
                await EventAggregator.PublishAsync(new ExportStatusUpdateEvent(_currentTable, true));
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
                if (tbl.Status == ExportStatus.Ready || tbl.Status == ExportStatus.Exporting)
                {
                    tbl.Status = ExportStatus.Cancelled;
                }
            }
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
                                    .Replace("]", "]]");

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

                        if (!string.IsNullOrEmpty(dataTypeName) && dataTypeName.EndsWith("*money", StringComparison.OrdinalIgnoreCase))
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
