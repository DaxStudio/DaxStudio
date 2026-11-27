using DaxStudio.Parsers.StorageEngine;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Groups the storage engine events of a batch by structure, as the Server Timings "Group" column does.
    /// Like the application, the grouper reads the formatted query text of each event. There is no regex grouper.
    /// </summary>
    [MemoryDiagnoser]
    public class GroupingBenchmarks
    {
        private (int, string)[] _queries;

        [ParamsSource(nameof(WorkloadNames))]
        public string Workload { get; set; }

        public static IEnumerable<string> WorkloadNames => Workloads.All;

        [GlobalSetup]
        public void Setup() => _queries = Workloads.Get(Workload).Select((q, i) => (i, TraceEventFormatter.FormatQuery(Engine.Parlot, q))).ToArray();

        [Benchmark(Baseline = true)]
        public int Antlr() => new XmSqlQueryGrouper().GroupQueries(_queries).StructuralGroups.Count;

        [Benchmark]
        public int Parlot() => new ParlotXmSqlQueryGrouper().GroupQueries(_queries).StructuralGroups.Count;
    }
}
