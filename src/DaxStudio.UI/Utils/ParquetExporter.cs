using DaxStudio.Core.Exports;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using Parquet;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DataColumn = Parquet.Data.DataColumn;

namespace DaxStudio.UI.Utils
{
    public static class ParquetExporter
    {
        public static async Task ExportDataReaderToParquetInChunksAsync(IQueryRunner runner, string outputPath, IDataReader reader, IStatusBarMessage statusProgress, int chunkSize = 1000000)
        {
            int resultSetIndex = 1;
            statusProgress.Update($"Starting export to parquet");

            do
            {
                string fileSuffix = resultSetIndex == 1 ? string.Empty : $"_{resultSetIndex}";

                string filePath = Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory,
                    $"{Path.GetFileNameWithoutExtension(outputPath)}{fileSuffix}.parquet");

                List<DataField> fields = Core.Exports.ParquetExporter.CreateDataFieldsFromReader(reader);
                var parquetSchema = new ParquetSchema(fields);

                using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    int outputRowCount = 0;
                    using (var parquetWriter = await ParquetWriter.CreateAsync(parquetSchema, fileStream))
                    {
                        var hasMoreRows = true;
                        while (hasMoreRows)
                        {
                            var chunkData = new List<List<object>>();
                            for (int i = 0; i < fields.Count; i++)
                            {
                                chunkData.Add(new List<object>());
                            }

                            int rowCount = 0;

                            while (hasMoreRows && rowCount < chunkSize)
                            {
                                hasMoreRows = reader.Read();
                                if (!hasMoreRows) break;

                                for (int i = 0; i < fields.Count; i++)
                                {
                                    chunkData[i].Add(reader.GetValue(i));
                                }
                                rowCount++;
                            }

                            if (rowCount == 0)
                                break;

                            outputRowCount += rowCount;
                            statusProgress.Update($"Written {outputRowCount:n0} rows to the file output for query {resultSetIndex}");

                            var columns = new List<DataColumn>();
                            for (int i = 0; i < fields.Count; i++)
                            {
                                Array typedArray = Core.Exports.ParquetExporter.ConvertToTypedArray(chunkData[i], fields[i].ClrType);
                                columns.Add(new DataColumn(fields[i], typedArray));
                            }

                            using (var rowGroupWriter = parquetWriter.CreateRowGroup())
                            {
                                foreach (var column in columns)
                                {
                                    await rowGroupWriter.WriteColumnAsync(column);
                                }
                            }

                            await Task.Yield(); // cooperative multitasking
                        }
                    }

                    runner.OutputMessage(
                        string.Format("Query {2} Completed ({0:N0} row{1} returned)"
                                    , outputRowCount
                                    , outputRowCount == 1 ? "" : "s", resultSetIndex)
                        );

                    runner.RowCount = outputRowCount;
                    await fileStream.FlushAsync();
                }

                resultSetIndex++;

            } while (reader.NextResult());
        }
    }
}
