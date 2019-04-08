using DaxStudio.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Extensions
{
    public static class DataReaderExtensions
    {
        

        internal class DaxColumn
        {
            public string OriginalName { get; set; }
            public string NewName { get; set; }
            public bool UseOriginalName { get; set; }
        }

        public static string[] CleanColumnNames(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader)
        {
            string[] columns = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns[i] = reader.GetName(i);
            }

            var columnPattern = new Regex(@"\[(?<col>.*)]\d*$", RegexOptions.Compiled);
            var mdxPattern = new Regex(@"\[[^\]]*\].\[[^\]]*\]");
            const string MEASURES_MDX = "[Measures].";
            var newColumnNames = new Dictionary<string, DaxColumn>();

            // If at least one column has the Mdx syntax, identify the result as an MDX query (hoping the assumption is always true...)
            //bool isMdxResult = (from DataColumn col in dataTable.Columns
            //                    where col.ColumnName.IndexOf("].[") > 0
            //                    select col).Count() > 0;
            bool isMdxResult = (from col in columns
                                where mdxPattern.IsMatch(col)
                                select col).Count() > 0;

            var measuresColumns = (from col in columns
                                   where col.IndexOf(MEASURES_MDX) >= 0
                                   select col);
            bool hasPlainMeasures = (from col in measuresColumns
                                     where col.IndexOf("].[", col.IndexOf(MEASURES_MDX) + MEASURES_MDX.Length) > 0
                                     select col).Count() == 0;
            foreach (string columnName in columns)
            {
                bool removeCaption = false;
                string name = columnName;
                bool removeSquareBrackets = !isMdxResult;
                int measuresMdxPos = name.IndexOf(MEASURES_MDX);// + MEASURES_MDX.Length;
                if (isMdxResult)
                {
                    if ((measuresMdxPos >= 0))
                    {
                        if ((name.IndexOf("].[", measuresMdxPos + MEASURES_MDX.Length) == -1)
                        && (name.IndexOf("].[", 0) == MEASURES_MDX.Length - 2))
                        {
                            removeSquareBrackets = true;
                        }
                        name = name.Replace(MEASURES_MDX, measuresMdxPos > 0 ? "\n" : "");
                    }
                    else
                    {
                        removeCaption = hasPlainMeasures;
                    }
                }

                if (removeSquareBrackets)
                {
                    var m = columnPattern.Match(name);
                    if (m.Success)
                    {
                        name = m.Groups["col"].Value;
                    }
                    // Format column naming for DAX result or if it is a measure name
                    //int firstBracket = name.IndexOf('[') + 1;
                    //name = firstBracket == 0 ? name : name.Substring(firstBracket, name.Length - firstBracket - 1);
                }
                var dc = new DaxColumn()
                {
                    //OriginalCaption = col.Caption,
                    OriginalName = columnName,
                    //NewCaption = (removeCaption) ? "" : name,
                    NewName = name.Replace(' ', '`').Replace(',', '`'),
                };
                newColumnNames.Add(dc.OriginalName, dc);
                //col.Caption = (removeCaption) ? "" : name;
                //col.ColumnName = name.Replace(' ', '_');
            }
            // check for duplicate names

            for (var outerIdx = 0; outerIdx < newColumnNames.Count; outerIdx++)
            {
                for (var innerIdx = outerIdx + 1; innerIdx < newColumnNames.Count; innerIdx++)
                {
                    if (newColumnNames.ElementAt(outerIdx).Value.NewName == newColumnNames.ElementAt(innerIdx).Value.NewName)
                    {
                        newColumnNames.ElementAt(outerIdx).Value.UseOriginalName = true;
                        newColumnNames.ElementAt(innerIdx).Value.UseOriginalName = true;
                    }
                }
            }
            string[] newNames = new string[columns.Length];
            // Update names
            for (int i = 0; i < columns.Length; i++)
            {
                var c = newColumnNames.ElementAt(i).Value;
                newNames[i] = c.UseOriginalName ? c.OriginalName : c.NewName;
            }
            return newNames;
        }




        public static DataSet ConvertToDataSet(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, bool autoFormat = false)
        {
            ADOTabular.ADOTabularColumn daxCol;
            DataSet ds = new DataSet();
            bool moreResults = true;
            int tableIdx = 1;
            int localeId = reader.Connection.LocaleIdentifier;
            while (moreResults)
            {
                DataTable dtSchema = reader.GetSchemaTable();
                DataTable dt = new DataTable(tableIdx.ToString());
                // You can also use an ArrayList instead of List<>
                List<DataColumn> listCols = new List<DataColumn>();
                if (dtSchema != null)
                {
                    foreach (DataRow row in dtSchema.Rows)
                    {
                        string columnName = Convert.ToString(row["ColumnName"]);
                        Type columnType = (Type)row["DataType"];
                        if (columnType.Name == "XmlaDataReader") columnType = typeof(string);
                        DataColumn column = new DataColumn(columnName, columnType); // (Type)(row["DataType"]));
                        column.Unique = (bool)row[Constants.IS_UNIQUE];
                        column.AllowDBNull = (bool)row[Constants.ALLOW_DBNULL];
                        daxCol = null;
                        reader.Connection.Columns.TryGetValue(columnName, out daxCol);
                        if (daxCol != null) {
                            column.ExtendedProperties.Add(Constants.FORMAT_STRING, daxCol.FormatString);
                            if (localeId != 0) column.ExtendedProperties.Add(Constants.LOCALE_ID, localeId);
                        }
                        else if (autoFormat) {
                            string formatString;
                            switch (column.DataType.Name)
                            {
                                case "Decimal":
                                case "Double":
                                    if (column.Caption.Contains(@"%") || column.Caption.Contains("Pct")) {
                                        formatString = "0.00%";
                                    }
                                    else {
                                        formatString = "#,0.00";
                                    }
                                    break;
                                case "Int64":
                                    formatString = "#,0";
                                    break;
                                default:
                                    formatString = null;
                                    break;
                            }
                            if (formatString != null) {
                                column.ExtendedProperties.Add(Constants.FORMAT_STRING, formatString);
                                if (localeId != 0) column.ExtendedProperties.Add(Constants.LOCALE_ID, localeId);
                            }
                        }
                        listCols.Add(column);
                        dt.Columns.Add(column);
                    }
                }

                // Read rows from DataReader and populate the DataTable
                while (reader.Read())
                {
                    DataRow dataRow = dt.NewRow();
                    for (int i = 0; i < listCols.Count; i++)
                    {
                        if (reader.IsDataReader(i))
                            dataRow[((DataColumn)listCols[i])] = reader.GetDataReaderValue(i); 
                        else
                            dataRow[((DataColumn)listCols[i])] = reader[i] ?? DBNull.Value;
                    }
                    dt.Rows.Add(dataRow);
                }
                dt.FixColumnNaming(reader.CommandText);
                ds.Tables.Add(dt);
                moreResults = reader.NextResult();
                tableIdx++;
            }
            return ds;

        }
    }
}
