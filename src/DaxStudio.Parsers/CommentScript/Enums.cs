using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{

    public enum ConnectionType
    {
        SERVER,
        DESKTOP,
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

    public enum ExportTarget
    {
        Metrics,
    }

    public enum AssertTableMode
    {
        Ordered,
        Unordered,
        Partial,
    }

    public enum AssertTableFormat
    {
        Inline,
        Csv,
        Txt,
        Md,
        Parquet,
    }

    public enum ShowType
    {
        Dependencies,
        LastUpdated,
        MaxUpdated,
        Diagram,
        Metrics,
        Delta,
    }

}
