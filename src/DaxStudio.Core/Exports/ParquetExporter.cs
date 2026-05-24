using DaxStudio.Core.Extensions;
using Parquet;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using DataColumn = Parquet.Data.DataColumn;

namespace DaxStudio.Core.Exports
{
    public static class ParquetExporter
    {
        // Used by the Data Export feature
        public static async Task ExportDataReaderToBuffersAsync(ParquetWriter parquetWriter, IDataReader reader, Action<string> StatusCallback, Action<long, bool> ProgressCallback, Func<bool> IsCancelled, List<List<object>> rowBuffers, List<DataField> fields, int rowGroupSize = 1000000)
        {
            await Task.Yield();

            int outputRowCount = 0;

            var hasMoreRows = true;
            while (hasMoreRows)
            {
                int rowCount = 0;

                while (hasMoreRows && rowCount < rowGroupSize)
                {
                    hasMoreRows = reader.Read();
                    if (!hasMoreRows) break;

                    for (int i = 0; i < fields.Count; i++)
                    {
                        try
                        {
                            rowBuffers[i].Add(reader.GetValue(i));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                        }
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
            }

            //update final row count
            ProgressCallback?.Invoke(outputRowCount, false);
        }

        public static async Task WriteRowGroupToParquet(ParquetWriter parquetWriter, List<List<object>> chunkData, List<DataField> fields)
        {
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

            await Task.Yield();
        }

        public static List<List<object>> ResetBuffers(List<DataField> fields, int bufferSize)
        {
            List<List<object>> chunkData = new List<List<object>>(fields.Count);
            for (int i = 0; i < fields.Count; i++)
            {
                chunkData.Add(new List<object>(bufferSize));
            }

            return chunkData;
        }

        public static List<DataField> CreateDataFieldsFromReader(IDataReader reader)
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

        public static Array ConvertToTypedArray(List<object> source, Type targetType)
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
