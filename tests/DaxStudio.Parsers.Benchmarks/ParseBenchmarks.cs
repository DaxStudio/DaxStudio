using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using BenchmarkDotNet.Attributes;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.StorageEngine.XmSql;
using System.Collections.Generic;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Parses every query of a batch into a tree, without walking it. The regex parser has no separate parse
    /// step, so it is not part of this comparison.
    /// </summary>
    [MemoryDiagnoser]
    public class ParseBenchmarks
    {
        private string[] _queries;
        private xmSQLLexer _lexer;
        private CommonTokenStream _tokens;
        private xmSQLParser _parser;

        [ParamsSource(nameof(WorkloadNames))]
        public string Workload { get; set; }

        public static IEnumerable<string> WorkloadNames => Workloads.All;

        [GlobalSetup]
        public void Setup()
        {
            _queries = Workloads.Get(Workload);

            // The reused, SLL-mode set-up of AntlrXmSqlParser
            _lexer = new xmSQLLexer(new AntlrInputStream(string.Empty));
            _lexer.RemoveErrorListeners();
            _tokens = new CommonTokenStream(_lexer);
            _parser = new xmSQLParser(_tokens);
            _parser.RemoveErrorListeners();
            _parser.Interpreter.PredictionMode = PredictionMode.SLL;
        }

        /// <summary>A new lexer and parser per query, as AntlrXmSqlFormatter and XmSqlQueryGrouper do.</summary>
        [Benchmark(Baseline = true)]
        public int Antlr()
        {
            var count = 0;
            foreach (var query in _queries)
            {
                var lexer = new xmSQLLexer(new AntlrInputStream(query));
                lexer.RemoveErrorListeners();
                var parser = new xmSQLParser(new CommonTokenStream(lexer));
                parser.RemoveErrorListeners();
                count += parser.query().ChildCount;
            }

            return count;
        }

        /// <summary>One lexer and parser reused in SLL mode, as AntlrXmSqlParser does.</summary>
        [Benchmark]
        public int AntlrReusedSll()
        {
            var count = 0;
            foreach (var query in _queries)
            {
                _lexer.SetInputStream(new AntlrInputStream(query));
                _tokens.SetTokenSource(_lexer);
                _parser.TokenStream = _tokens;
                _parser.Reset();
                count += _parser.query().ChildCount;
            }

            return count;
        }

        [Benchmark]
        public int Parlot()
        {
            var count = 0;
            foreach (var query in _queries)
            {
                count += XmSqlGrammar.Parse(query).Statements.Count;
            }

            return count;
        }
    }
}
