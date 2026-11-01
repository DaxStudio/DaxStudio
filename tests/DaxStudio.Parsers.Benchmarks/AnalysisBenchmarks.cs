using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Builds the xmSQL analysis of a batch of storage engine events, as the "Show in model diagram" action
    /// does. Like the application, the parsers read the formatted query text of each event.
    /// </summary>
    [MemoryDiagnoser]
    public class AnalysisBenchmarks
    {
        private string[] _queries;
        private readonly XmSqlParser _regex = new XmSqlParser();
        private readonly AntlrXmSqlParser _antlr = new AntlrXmSqlParser();
        private readonly ParlotXmSqlParser _parlot = new ParlotXmSqlParser();

        [ParamsSource(nameof(WorkloadNames))]
        public string Workload { get; set; }

        public static IEnumerable<string> WorkloadNames => Workloads.All;

        [GlobalSetup]
        public void Setup() => _queries = Workloads.Get(Workload).Select(q => TraceEventFormatter.FormatQuery(Engine.Parlot, q)).ToArray();

        [Benchmark(Baseline = true)]
        public int Regex() => Run(_regex);

        [Benchmark]
        public int Antlr() => Run(_antlr);

        [Benchmark]
        public int Parlot() => Run(_parlot);

        private int Run(IXmSqlParser parser)
        {
            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics { EstimatedRows = 1000, DurationMs = 12, CpuTimeMs = 30 };
            foreach (var query in _queries)
            {
                parser.ParseQueryWithMetrics(query, analysis, metrics);
            }

            return analysis.Tables.Count;
        }
    }
}
