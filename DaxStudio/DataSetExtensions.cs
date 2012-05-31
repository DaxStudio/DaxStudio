using System.Data;

namespace DaxStudio
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

    }
}
