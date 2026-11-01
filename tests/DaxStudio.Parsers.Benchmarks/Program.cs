using BenchmarkDotNet.Running;
using DaxStudio.Parsers.XmSql;
using System;
using System.Linq;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Usage:
    ///   dotnet run -c Release -f net8.0 --project tests/DaxStudio.Parsers.Benchmarks -- --filter "*"
    ///   dotnet run -c Release -f net8.0 --project tests/DaxStudio.Parsers.Benchmarks -- --runtimes net8.0 net10.0 --filter "*Format*"
    ///   dotnet run -c Release -f net8.0 --project tests/DaxStudio.Parsers.Benchmarks -- --check
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--check")
            {
                return Check();
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return 0;
        }

        /// <summary>Describes the workloads and verifies that ANTLR and Parlot format them identically.</summary>
        private static int Check()
        {
            var failures = 0;
            Console.WriteLine("| Workload | Queries | Total size | Largest | ANTLR syntax errors (without size footer) | Parlot errors | ANTLR = Parlot |");
            Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
            foreach (var name in Workloads.All)
            {
                var queries = Workloads.Get(name);
                var antlrErrors = queries.Count(q => SyntaxErrors(q) > 0);
                var antlrErrorsWithoutFooter = queries.Count(q => SyntaxErrors(SizeFooter.Replace(q, "")) > 0);
                var parlotErrors = queries.Count(q => XmSqlGrammar.Parse(q).HasErrors);
                var same = queries.Count(q => TraceEventFormatter.FormatQuery(Engine.Antlr, q) == TraceEventFormatter.FormatQuery(Engine.Parlot, q));
                failures += queries.Length - same;
                Console.WriteLine($"| {name} | {queries.Length} | {queries.Sum(q => q.Length) / 1024.0:N0} KB | {queries.Max(q => q.Length) / 1024.0:N1} KB | {antlrErrors} ({antlrErrorsWithoutFooter}) | {parlotErrors} | {same}/{queries.Length} |");
            }

            return failures == 0 ? 0 : 1;
        }

        private static readonly System.Text.RegularExpressions.Regex SizeFooter = new System.Text.RegularExpressions.Regex(
            @"[\r\n]+\s*\[Estimated\s+size\s*\([^)]*\)\s*:\s*\d+\s*,\s*\d+\s*\]");

        private static int SyntaxErrors(string query)
        {
            var lexer = new Grammars.Generated.xmSQLLexer(new Antlr4.Runtime.AntlrInputStream(query));
            lexer.RemoveErrorListeners();
            var parser = new Grammars.Generated.xmSQLParser(new Antlr4.Runtime.CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.query();
            return parser.NumberOfSyntaxErrors;
        }
    }
}
