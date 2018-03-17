using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using DaxStudio.UI.Extensions;
using ADOTabular.AdomdClientWrappers;
using System.IO;

namespace DaxStudio.UI.ViewModels
{

    [Export]
    public class ExportDataDialogViewModel : Screen
    {

        [ImportingConstructor]
        public ExportDataDialogViewModel()
        {
            this.IsCSVExport = true;
            this.TruncateTables = true;
        }

        // TODO: Any best way to instantiate this object with dependency injection?        
        public DocumentViewModel ActiveDocument { get; set; }

        private bool _isCSVExport = false;
        public bool IsCSVExport
        {
            get { return _isCSVExport; }
            set
            {
                _isCSVExport = value;
                NotifyOfPropertyChange(() => IsCSVExport);
            }
        }

        private bool _isSQLExport = false;
        public bool IsSQLExport
        {
            get { return _isSQLExport; }
            set
            {
                _isSQLExport = value;
                NotifyOfPropertyChange(() => IsSQLExport);
            }
        }
       
        public string OutputPath { get; set; }

        public string ConnectionString { get; set; }
        
        public string SchemaName { get; set; }

        public bool TruncateTables { get; set; }

        public void Export()
        {
            if (IsCSVExport)
            {              
                ExportDataToFolder(this.OutputPath);
            }
            else if (IsSQLExport)
            {
                ExportDataToSQLServer(this.ConnectionString, this.SchemaName, this.TruncateTables);
            }           

            TryClose(true);
        }       

        public void Cancel()
        {

            //TryClose(false);
        }

        private void ExportDataToFolder(string outputPath)
        {
            var metadataPane = this.ActiveDocument.MetadataPane;

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

            foreach (var table in metadataPane.SelectedModel.Tables)
            {
                var csvFilePath = System.IO.Path.Combine(outputPath, $"{table.Name}.csv");

                var daxQuery = $"EVALUATE('{table.Name}')";

                using (var textWriter = new System.IO.StreamWriter(csvFilePath, false, System.Text.Encoding.UTF8))
                {
                    using (var csvWriter = new CsvHelper.CsvWriter(textWriter))
                    {
                        var rows = 0;

                        using (var reader = metadataPane.Connection.ExecuteReader(daxQuery))
                        {
                            // Header

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

                                csvWriter.NextRecord();
                            }
                        }
                    }

                }
            }
        }

        private void ExportDataToSQLServer(string connStr, string schemaName, bool truncateTables)
        {
            var metadataPane = this.ActiveDocument.MetadataPane;

            // TODO: Use async but to be well done need to apply async on the DBCommand & DBConnection
            // TODO: Show warning message?
            if (metadataPane.SelectedModel == null)
            {
                return;
            }

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                foreach (var table in metadataPane.SelectedModel.Tables)
                {
                    var daxQuery = $"EVALUATE('{table.Name}')";

                    using (var reader = metadataPane.Connection.ExecuteReader(daxQuery))
                    {
                        var sqlTableName = $"[{schemaName}].[{table.Name}]";

                        EnsureSQLTableExists(conn, sqlTableName, reader);

                        using (var transaction = conn.BeginTransaction())
                        {
                            if(truncateTables)
                            {
                                using (var cmd = new SqlCommand($"truncate table {sqlTableName}", conn))
                                {
                                    cmd.Transaction = transaction;
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            using (var sqlBulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, transaction))
                            {
                                sqlBulkCopy.DestinationTableName = sqlTableName;
                                sqlBulkCopy.BatchSize = 1000;
                                sqlBulkCopy.NotifyAfter = 1000;

                                sqlBulkCopy.WriteToServer(reader);
                            }

                            transaction.Commit();
                        }

                    }
                }
            }
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
    }
}
