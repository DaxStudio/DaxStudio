using DaxStudio.Interfaces;

namespace DaxStudio.UI.Model
{
    public class LinkedQueryResult : ILinkedQueryResult
    {
        public LinkedQueryResult(string daxQuery, string targetSheet, string connectionString)
        {
            DaxQuery = daxQuery;
            TargetSheet = targetSheet;
            ConnectionString = connectionString;
        }
        public string DaxQuery { get; set; }

        public string TargetSheet { get; set; }

        public string ConnectionString { get; set; }
    }
}
