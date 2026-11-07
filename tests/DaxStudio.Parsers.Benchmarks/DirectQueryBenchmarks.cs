using DaxStudio.Parsers.StorageEngine;
using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Analyses the DirectQuery SQL corpus (the SQL that DirectQuery events carry), as "Show in model diagram" does.
    /// </summary>
    [MemoryDiagnoser]
    public class DirectQueryBenchmarks
    {
        private string[] _queries;
        private readonly XmSqlParser _regex = new XmSqlParser();
        private readonly AntlrXmSqlParser _antlr = new AntlrXmSqlParser();
        private readonly ParlotXmSqlParser _parlot = new ParlotXmSqlParser();

        [GlobalSetup]
        public void Setup()
        {
            _queries = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Corpus", "directquery.jsonl"))
                .Where(l => l.Length > 0)
                .Select(l => JsonSerializer.Deserialize<string>(l))
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToArray();
        }

        [Benchmark(Baseline = true)]
        public int Regex() => Run(_regex);

        [Benchmark]
        public int Antlr() => Run(_antlr);

        [Benchmark]
        public int Parlot() => Run(_parlot);

        private int Run(IXmSqlParser parser)
        {
            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics { DurationMs = 40 };
            foreach (var query in _queries)
            {
                parser.ParseDirectQuerySql(query, analysis, metrics);
            }

            return analysis.Tables.Count;
        }
    }
}
