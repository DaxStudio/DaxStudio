using System.Data;
using System.Text;

namespace DaxStudio.ExcelAddin
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

    public static class DataTableExtensions
    {

        public static object[,]  ToObjectArray(this DataTable dt)
        {

            // Copy the DataTable to an object array of object[,]
            var rawData = new object[dt.Rows.Count + 1, dt.Columns.Count];

            // Copy the column names to the first row of the object array
            for (int col = 0; col < dt.Columns.Count; col++)
            {
                rawData[0, col] = dt.Columns[col].ColumnName;
            }

            // Copy the values to the object array
            for (int col = 0; col < dt.Columns.Count; col++)
            {
                // I don't like comparing a type name to a string, but I have not found a better way of doing this
                // (taken from https://stackoverflow.com/questions/2114823/how-do-i-check-if-an-object-contains-a-byte-array)
                if ( dt.Columns[col].DataType.Name == "Byte[]" ) 
                    continue; // ignore this column 

                for (int row = 0; row < dt.Rows.Count; row++)
                {
                    if (dt.Rows[row].ItemArray[col] is System.Guid)
                    {
                        rawData[row + 1, col] = dt.Rows[row].ItemArray[col].ToString();
                    }
                    else
                    {
                        rawData[row + 1, col] = dt.Rows[row].ItemArray[col];
                    }
                }
            }
            return rawData;
        }

        public static string ToCsv(this DataTable dataTable)
        {
            var sbData = new StringBuilder();

            // Only return Null if there is no structure.
            if (dataTable.Columns.Count == 0)
                return null;

            foreach (var col in dataTable.Columns)
            {
                if (col == null)
                    sbData.Append(',');
                else
                    sbData.Append("\"" + col.ToString().Replace("\"", "\"\"") + "\",");
            }

            sbData.Replace(",", System.Environment.NewLine, sbData.Length - 1, 1);

            foreach (DataRow dr in dataTable.Rows)
            {
                foreach (var column in dr.ItemArray)
                {
                    if (column == null)
                        sbData.Append(',');
                    else
                        sbData.Append("\"" + column.ToString().Replace("\"", "\"\"") + "\",");
                }
                sbData.Replace(",", System.Environment.NewLine, sbData.Length - 1, 1);
            }

            return sbData.ToString();
        }

        public static string ToTabDelimitedString(this DataTable dataTable)
        {
            var sbData = new StringBuilder();

            // Only return Null if there is no structure.
            if (dataTable.Columns.Count == 0)
                return null;

            foreach (var col in dataTable.Columns)
            {
                if (col == null)
                    sbData.Append('\t');
                else
                {
                    sbData.Append('\"');
                    sbData.Append(col.ToString().Replace("\"", "\"\""));
                    sbData.Append("\",");
                }
            }

            sbData.Replace("\t", System.Environment.NewLine, sbData.Length - 1, 1);

            foreach (DataRow dr in dataTable.Rows)
            {
                foreach (var column in dr.ItemArray)
                {
                    if (column == null)
                        sbData.Append('\t');
                    else
                    {
                        sbData.Append('\"');
                        sbData.Append(column.ToString().Replace("\"", "\"\""));
                        sbData.Append( "\",");
                    }
                }
                sbData.Replace("\t", System.Environment.NewLine, sbData.Length - 1, 1);
            }

            return sbData.ToString();
        }
    }
}
