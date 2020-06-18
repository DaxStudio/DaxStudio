using System;
using System.Collections.Generic;
using System.Data;


namespace DaxStudio.Tests.Utils
{
    public static class DmvHelpers
    {
        public static DataTable ListToTable<T>(List<T> rows)
        {
            var dt = new DataTable();
            var props = typeof(T).GetProperties();
            Array.ForEach(props, p => dt.Columns.Add(p.Name, p.PropertyType));
            foreach (var r in rows)
            {
                object[] vals = new object[props.Length];
                for (int idx = 0; idx < vals.Length; idx++) { vals[idx] = props[idx].GetValue(r, null); }
                dt.Rows.Add(vals);
            }
            return dt;
        }
    }

    public class Function
    {
        public string FUNCTION_NAME { get; set; }
        public int ORIGIN { get; set; }
    }

    public class Dmv
    {
        public string SchemaName { get; set; }
    }
    public class Keyword
    {
        public string KEYWORD { get; set; }
    }

    public class MeasureMD
    {
        public string MEASURE_NAME { get; set; }
        public string MEASURE_CAPTION { get; set; }
        public string DESCRIPTION { get; set; }
        public bool MEASURE_IS_VISIBLE { get; set; }
        public string EXPRESSION { get; set; }    
    }

    public class TableTM
    {
        public UInt64 ID { get; set; }
        public string Name { get; set; }
    }

    public class MeasureTM
    {
        public UInt64 TableID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsHidden { get; set; }
        public string Expression { get; set; }
    }


    public class Cube {
        public string CUBE_NAME { get; set; }
        public string CUBE_CAPTION { get; set; }
        public string DESCRIPTION { get; set; }
        public string BASE_CUBE_NAME { get; set; }
    }

    public class CSDL_METADATA
    {
        public string Metadata { get; set; }
    }
}
