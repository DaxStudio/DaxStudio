using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{

    public enum ConnectionType
    {
        SERVER,
        PBIX,
        SSDT
    }

    public enum PerformanceProperty
    {
        Duration,
        SE_CPU,
        SE_QUERIES,
    }

    public enum TraceType
    {
        ServerTimings,
        QueryPlan,
        AllQueries,
    }

    public enum MetricsAction
    {
        Export,
        View,
    }

    public enum AssertTableMode
    {
        Ordered,
        Unordered,
        Partial,
    }

    public enum ShowType
    {
        Dependencies,
        LastUpdated,
        MaxUpdated,
    }

}
