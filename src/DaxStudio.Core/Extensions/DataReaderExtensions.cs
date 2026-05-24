using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaxStudio.Core.Extensions
{
    public static class DataReaderExtensions
    {
        internal class DaxColumn
        {
            public string OriginalName { get; set; }
            public string NewName { get; set; }
            public bool UseOriginalName { get; set; }
        }

        public static string[] CleanColumnNames(this IDataReader reader)
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
            bool isMdxResult = (from col in columns
                                where mdxPattern.IsMatch(col)
                                select col).Any();

            var measuresColumns = (from col in columns
                                   where col.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase) >= 0
                                   select col);
            bool hasPlainMeasures = !(from col in measuresColumns
                                      where col.IndexOf("].[", col.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase) + MEASURES_MDX.Length, StringComparison.OrdinalIgnoreCase) > 0
                                      select col).Any();
            foreach (string columnName in columns)
            {
                bool removeCaption = false;
                string name = columnName;
                bool removeSquareBrackets = !isMdxResult;
                int measuresMdxPos = name.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase);
                if (isMdxResult)
                {
                    if ((measuresMdxPos >= 0))
                    {
                        if ((name.IndexOf("].[", measuresMdxPos + MEASURES_MDX.Length, StringComparison.OrdinalIgnoreCase) == -1)
                        && (name.IndexOf("].[", 0, StringComparison.OrdinalIgnoreCase) == MEASURES_MDX.Length - 2))
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
                }
                var dc = new DaxColumn()
                {
                    OriginalName = columnName,
                    NewName = name,
                };
                newColumnNames.Add(dc.OriginalName, dc);
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
            for (int i = 0; i < columns.Length; i++)
            {
                var c = newColumnNames.ElementAt(i).Value;
                newNames[i] = c.UseOriginalName ? c.OriginalName : c.NewName;
            }
            return newNames;
        }
    }
}
