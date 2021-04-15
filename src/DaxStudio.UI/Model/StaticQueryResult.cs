using System.Data;
using DaxStudio.Common.Interfaces;

namespace DaxStudio.UI.Model
{
    public class StaticQueryResult : IStaticQueryResult
    {
        public StaticQueryResult(string targetSheet, DataTable data)
        {
            QueryResults = data;
            TargetSheet = targetSheet;
        }

        public DataTable QueryResults { get; set; }

        public string TargetSheet { get; set; }
    }
}
