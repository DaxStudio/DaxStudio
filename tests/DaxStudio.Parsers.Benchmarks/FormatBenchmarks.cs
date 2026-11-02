using DaxStudio.Parsers.StorageEngine;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Formats every storage engine event of a batch, as <c>TraceStorageEngineEvent</c> does when it is created.
    /// </summary>
    [MemoryDiagnoser]
    public class FormatBenchmarks
    {
        private readonly Dictionary<string, string> _remapColumns = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _remapTables = new Dictionary<string, string>();
        private string[] _queries;

        [ParamsSource(nameof(WorkloadNames))]
        public string Workload { get; set; }

        public static IEnumerable<string> WorkloadNames => Workloads.All;

        [GlobalSetup]
        public void Setup() => _queries = Workloads.Get(Workload);

        [Benchmark(Baseline = true)]
        public int Regex() => Run(Engine.Regex);

        [Benchmark]
        public int Antlr() => Run(Engine.Antlr);

        [Benchmark]
        public int Parlot() => Run(Engine.Parlot);

        private int Run(Engine engine)
        {
            var length = 0;
            foreach (var query in _queries)
            {
                length += TraceEventFormatter.FormatQuery(engine, query, _remapColumns, _remapTables).Length;
            }

            return length;
        }
    }
}
