namespace DaxStudio.Core.Trace
{
    public class QueryGroupSummary
    {
        public int GroupId { get; set; }
        public string GroupType { get; set; }
        public int EventCount { get; set; }
        public int CacheHits { get; set; }
        public long TotalDuration { get; set; }
        public long TotalCpu { get; set; }
        public long TotalRows { get; set; }
        public long TotalKB { get; set; }
    }
}
