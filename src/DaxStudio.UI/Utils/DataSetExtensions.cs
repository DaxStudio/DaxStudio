using System.Data;
using System.Text;
using System.Windows.Documents;

namespace DaxStudio.UI
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
                if ( dt.Columns[col].DataType.Name == "Byte[]" ) // TODO: there must be a better way to do this
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
                    sbData.Append(",");
                else
                    sbData.Append("\"" + col.ToString().Replace("\"", "\"\"") + "\",");
            }

            sbData.Replace(",", System.Environment.NewLine, sbData.Length - 1, 1);

            foreach (DataRow dr in dataTable.Rows)
            {
                foreach (var column in dr.ItemArray)
                {
                    if (column == null)
                        sbData.Append(",");
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
                    sbData.Append("\t");
                else
                    sbData.Append("\"" + col.ToString().Replace("\"", "\"\"") + "\",");
            }

            sbData.Replace("\t", System.Environment.NewLine, sbData.Length - 1, 1);

            foreach (DataRow dr in dataTable.Rows)
            {
                foreach (var column in dr.ItemArray)
                {
                    if (column == null)
                        sbData.Append("\t");
                    else
                        sbData.Append("\"" + column.ToString().Replace("\"", "\"\"") + "\",");
                }
                sbData.Replace("\t", System.Environment.NewLine, sbData.Length - 1, 1);
            }

            return sbData.ToString();
        }


        /*
Imports System.IO
Imports System.Text
Imports System.Web.UI
Imports System.Web.UI.WebControls
        
  
  /// <summary>
  /// Generate a string representation of an HTML table from a DataTable.  Add
  /// a column header row with cells for the text, and all data cells.
  /// </summary>
  /// <returns>A string that represents the HTML table.</returns>
  public static string GenerateFromDataTable(DataTable dt)
  {
   // Create a new HTML table object.
    var table = new Table();

    // Declare variables.
    TableRow row;
    TableCell cell;
    

    // Add column header cells.
    foreach (var dc in dt.Columns)
    {
      row = new TableRow();
      cell = new TableCell();
      cell. .Text = dc.ColumnName;
      row.Cells.Add(cell);

      table.Rows.Add(row)
    }

    // Add data rows and cells.
    foreach (var dr in dt.Rows)
    {
      row = new TableRow();
      foreach (dc In dt.Columns)
      {
        cell = new TableCell();
        cell.Text = dr(dc).ToString();
        row.Cells.Add(cell);
      }
      table.Rows.Add(row);
    }

    // Render the HTML table to a StringBuilder to get the string representation
    // for the table.
    var sb = new StringBuilder();
    using (var sw = new StringWriter(sb))
      {
      using (var tw = new HtmlTextWriter(sw))
      {
        table.RenderControl(tw);
      }
      }
    return sb.ToString();
  }
*/


    }
}
