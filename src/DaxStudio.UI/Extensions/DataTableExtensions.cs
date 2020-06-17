using System.Collections.ObjectModel;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Linq;

namespace DaxStudio.UI.Extensions
{
    //static class DataSetExtensions
    //{
    //    //Object[] objectArray = DataSet.ToSpecifiedObject<Object[]>();

    //    public static T ToSpecifiedObject<T>(this DataSet value)
    //    {
    //        byte[] buf = System.Text.UTF8Encoding.UTF8.GetBytes(value.GetXml());
    //        MemoryStream ms = new MemoryStream(buf);
    //        XmlSerializer ser = new XmlSerializer(typeof(T));
    //        object obj = ser.Deserialize(ms);

    //        return (T)obj;
    //    }
    //}

    internal class DaxColumn
    {
        public string OriginalName;
        public string OriginalCaption;
        public string NewName;
        public string NewCaption;
        public bool UseOriginalName;
    }

    public static class DataTableExtensions
    {

        private static bool IsMdxQuery(string query)
        {
            var trimmedQuery = query.Trim().Substring(0, 6).ToUpperInvariant();
            return trimmedQuery.StartsWith("WITH") || trimmedQuery.StartsWith("SELECT");
        }

        public static void FixColumnNaming(this DataTable dataTable, string query)
        {
            var columnPattern = new Regex(@"\[(?<col>.*)]\d*$", RegexOptions.Compiled);

            const string MEASURES_MDX = "[Measures].";
            var newColumnNames = new Collection<DaxColumn>();
            // If at least one column has the Mdx syntax, identify the result as an MDX query (hoping the assumption is always true...)
            //bool isMdxResult = (from DataColumn col in dataTable.Columns
            //                    where col.ColumnName.IndexOf("].[") > 0
            //                    select col).Count() > 0;
            bool isMdxResult = IsMdxQuery(query);
            var measuresColumns = (from DataColumn col in dataTable.Columns
                                   where col.ColumnName.IndexOf(MEASURES_MDX) >= 0
                                   select col);
            bool hasPlainMeasures = measuresColumns.Count() > 0 && (from DataColumn col in measuresColumns
                                     where col.ColumnName.IndexOf("].[", col.ColumnName.IndexOf(MEASURES_MDX) + MEASURES_MDX.Length) > 0
                                     select col).Count() == 0;
            foreach (DataColumn col in dataTable.Columns)
            {
                bool removeCaption = false;
                string name = col.ColumnName;
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
                    OriginalCaption = col.Caption,
                    OriginalName = col.ColumnName,
                    NewCaption = (removeCaption) ? "" : name,
                    NewName = name.Replace(' ', '`').Replace(',', '`'),
                };
                newColumnNames.Add(dc);
                //col.Caption = (removeCaption) ? "" : name;
                //col.ColumnName = name.Replace(' ', '_');
            }
            // check for duplicate names

            for (var outerIdx = 0; outerIdx < newColumnNames.Count; outerIdx++)
            {
                for (var innerIdx = outerIdx + 1; innerIdx < newColumnNames.Count; innerIdx++)
                {
                    if (newColumnNames[outerIdx].NewName == newColumnNames[innerIdx].NewName)
                    {
                        newColumnNames[outerIdx].UseOriginalName = true;
                        newColumnNames[innerIdx].UseOriginalName = true;
                    }
                }
            }
            // Update names
            foreach (DaxColumn c in newColumnNames)
            {
                var dc = dataTable.Columns[c.OriginalName];
                dc.Caption = c.NewCaption;

                if (!c.UseOriginalName)
                {
                    //    var dc = dataTable.Columns[c.OriginalName];
                    //    dc.Caption = c.NewCaption;
                    dc.ColumnName = c.NewName;
                }
            }
        }

    }
}
