using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
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
        // Used by the Data Export feature
        public static async Task ExportDataReaderToParquetInChunksAsync(Stream fileStream, IDataReader reader, Action<String> StatusCallback, Action<long,bool> ProgressCallback, Func<bool> IsCancelled, int chunkSize = 1000000)
        {
            int resultSetIndex = 1;
            StatusCallback($"Starting export to parquet");


            List<DataField> fields = CreateDataFieldsFromReader(reader);
            var parquetSchema = new ParquetSchema(fields);


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

                        if (rowCount % 5000 == 0)
                        {
                                
                            if (IsCancelled?.Invoke() == true)
                            {
                                StatusCallback("Export cancelled by user");
                                ProgressCallback?.Invoke(outputRowCount + rowCount, true);
                                return;
                            }
                            else
                            {
                                ProgressCallback?.Invoke(outputRowCount + rowCount, false);
                            }
                        }
                    }

                    if (rowCount == 0)
                        break;

                    outputRowCount += rowCount;
                    StatusCallback($"Written {outputRowCount:n0} rows to the file output for query {resultSetIndex}");

                    var columns = new List<DataColumn>();
                    for (int i = 0; i < fields.Count; i++)
                    {

                        Array typedArray = ConvertToTypedArray(chunkData[i], fields[i].ClrType);
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

            await fileStream.FlushAsync();

            //update final row count
            ProgressCallback?.Invoke(outputRowCount, false);

            
        }



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

                List<DataField> fields = CreateDataFieldsFromReader( reader);
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

                                Array typedArray = ConvertToTypedArray(chunkData[i], fields[i].ClrType);
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

        private static List<DataField> CreateDataFieldsFromReader(IDataReader reader )
        {
            var fields = new List<DataField>();
            var cleanNames = reader.CleanColumnNames();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                fields.Add(new DataField(cleanNames[i], MakeNullable(reader.GetFieldType(i))));
            }

            return fields;
        }


        private static Type MakeNullable(Type type)
        {
            // If it's already nullable or a reference type, return as-is
            if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null)
                return type;

            // Wrap in Nullable<>
            return typeof(Nullable<>).MakeGenericType(type);
        }


        internal static Array ConvertToTypedArray(List<object> source, Type targetType)
        {
            int count = source.Count;

            // Handle common types with fast paths
            if (targetType == typeof(int))
            {
                var result = new int?[count];
                for (int i = 0; i < count; i++)
                    result[i] = source[i] == null ? (int?)null :
                                source[i] is int val ? val : Convert.ToInt32(source[i]);
                return result;
            }
            else if (targetType == typeof(long))
            {
                var result = new long?[count];
                for (int i = 0; i < count; i++)
                    result[i] = source[i] == null ? (long?)null :
                                source[i] is long val ? val : Convert.ToInt64(source[i]);
                return result;
            }
            else if (targetType == typeof(double))
            {
                var result = new double?[count];
                for (int i = 0; i < count; i++)
                    result[i] = source[i] == null ? (double?)null :
                                source[i] is double val ? val : Convert.ToDouble(source[i]);
                return result;
            }
            else if (targetType == typeof(string))
            {
                var result = new string[count];
                for (int i = 0; i < count; i++)
                    result[i] = source[i]?.ToString();
                return result;
            }
            else
            {
                // Generic fallback
                var elementType = targetType.IsValueType ? typeof(Nullable<>).MakeGenericType(targetType) : targetType;
                var result = Array.CreateInstance(elementType, count);
                for (int i = 0; i < count; i++)
                {
                    var value = source[i] == null ? null : Convert.ChangeType(source[i], targetType);
                    result.SetValue(value, i);
                }
                return result;
            }
        }

    }

}
