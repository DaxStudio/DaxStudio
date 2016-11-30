using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class DataSetExtensions
    {
        public static string RowCounts(this DataSet ds)
        {
            if (ds == null) return "";
            List<string> rowCounts = new List<string>();
            foreach (DataTable t in ds.Tables)
            {
                rowCounts.Add(t.Rows.Count.ToString( "#,###;n/a;0"));
            }
            return string.Join("\n", rowCounts.ToArray());
        }
    }
}
