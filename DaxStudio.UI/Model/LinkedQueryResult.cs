using DaxStudio.Interfaces;

namespace DaxStudio.UI.Model
{
    public class LinkedQueryResult : ILinkedQueryResult
    {
        public LinkedQueryResult(string daxQuery, string targetSheet)
        {
            DaxQuery = daxQuery;
            TargetSheet = targetSheet;
        }
        public string DaxQuery { get; set; }

        public string TargetSheet { get; set; }
    }
}
