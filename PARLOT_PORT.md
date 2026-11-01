# Parlot for the xmSQL and DirectQuery SQL parsers

This branch adds a [Parlot](https://github.com/sebastienros/parlot) implementation of the grammar-based storage
engine parsers, next to the ANTLR one, and points the "Use New Parser for SE Queries" option and the query
grouping at it. On valid xmSQL and DirectQuery SQL the results are the same as ANTLR's, and it is faster than both ANTLR and the default regex
parser on every path measured. On a 500-event server timings batch, Parlot formats the events in **0.47x the time of
the regex path** that runs today, while ANTLR takes 2.1x. Parsing alone is **10x faster than ANTLR** and allocates
**24x less**.

The ANTLR classes are unchanged (apart from `XmSqlQueryGrouper.ComputeFingerprint`, which is now virtual) and still build, so the change can land in steps. They are also the
oracle that the tests compare Parlot against.

## What changed

| Area | Files |
|---|---|
| xmSQL grammar | `XmSql/XmSqlLexer.cs` (the lexer rules of `xmSQL.g4`, exact), `XmSql/XmSqlGrammar.cs` (the parser rules), `XmSql/XmSqlAst.cs` (a tree whose nodes mirror the ANTLR rule contexts) |
| Tree walkers | `XmSql/XmSqlFormattingWalker.cs`, `XmSqlAnalysisWalker.cs`, `XmSqlFingerprintWalker.cs`: ports of the three ANTLR visitors, method for method |
| DirectQuery SQL | `DirectQuery/*`: lexer, grammar, tree and analysis walker for `DirectQuerySql.g4` |
| Entry points | `ParlotXmSqlFormatter` (same contract as `AntlrXmSqlFormatter`), `ParlotXmSqlParser : IXmSqlParser` (as `AntlrXmSqlParser`), `ParlotXmSqlQueryGrouper : XmSqlQueryGrouper` |
| Application | `TraceStorageEngineEvent`, `ServerTimesViewModel`, `ServerTimesModel` and `XmSqlErdViewModel` use the Parlot classes where they used the ANTLR ones. The option keeps its name and default (off). |
| Tests | `tests/DaxStudio.Parsers.Tests` (net8.0, runs on macOS/Linux): links the existing parser tests and adds the differential tests. `tests/DaxStudio.Parsers.ParlotTests` reruns `AntlrXmSqlParserTests`, `AntlrXmSqlFormatterTests` and `XmSqlFingerprintTests` with the ANTLR classes aliased to the Parlot ones. |
| Benchmarks | `tests/DaxStudio.Parsers.Benchmarks` (BenchmarkDotNet, net8.0 and net10.0) |

The Parlot package (2.1.0, netstandard2.0, net472, net8.0, net10.0) is the only new dependency of
`DaxStudio.Parsers`, which stays C# 8 and still targets net472.

## Correctness

Parlot is a PEG combinator library and ANTLR an ALL(*) generator, so the port is checked rather than assumed:

- **Corpus.** The 113 distinct xmSQL queries and 86 DirectQuery statements (73 valid for the ANTLR grammar)
  that the existing parser tests feed the parsers were collected into `tests/DaxStudio.Parsers.Tests/Corpus`.
  The xmSQL ones are used both as traced and as formatted by DAX Studio (218 inputs).
- **Formatter:** every query, with all 8 combinations of format / simplify / dates, with and without name
  remaps and date column ids: **1,808 of 1,808 outputs are identical to ANTLR's**, including the queries ANTLR
  reports syntax errors for.
- **Analysis:** each query with 5 metrics variants and `ParseQuery`, compared by dumping the whole
  `XmSqlAnalysis` graph: **identical on every input ANTLR parses without errors** (1,248 comparisons). The same
  holds for a single batch over all of them, and for `ParseQueries`.
- **Fingerprints and grouping:** identical on every input ANTLR parses without errors (435 comparisons).
- **DirectQuery analysis:** identical on the 73 clean statements, alone and accumulated. Parlot accepts and
  rejects exactly the statements ANTLR accepts and rejects.
- **Lexer:** the token scanners are compared with the ANTLR lexer on 250,000 random strings built from the
  characters that matter (bracketed names with nested parentheses and `]]` escapes, quoted names, strings,
  numbers).
- **Existing tests:** the 85 tests of `AntlrXmSqlParserTests`, `AntlrXmSqlFormatterTests` and
  `XmSqlFingerprintTests` also pass against the Parlot classes, through the aliased project.

Run them with `dotnet test tests/DaxStudio.Parsers.Tests` and `dotnet test tests/DaxStudio.Parsers.ParlotTests`.

### Where the results differ, on purpose

The analysis and the grouping read each event's **formatted** query (`evt.Query`), and DAX Studio's own
formatter writes things the xmSQL grammar doesn't have: `/* 2024-01-31 */` comments after dates, `PFCAST`
without its type, `CallbackDataID ( <DAX> ) ( PFDATAID ( ... ) )`, estimated sizes inside truncated lists and
after the query, and `, ` between statements when line breaks are off. ANTLR reports syntax errors on that
text and recovers with partial results; 12 of the formatted corpus queries get a different analysis. The Parlot grammar reads that text, so on those
queries it finds **more**. For example, it keeps all the values of a date `IN` list where ANTLR keeps the first
one. The tests check that Parlot reads every formatted corpus query without errors and finds at least what ANTLR
finds. On the benchmark workloads, the Parlot analysis finds the same tables and joins as both other parsers.
Its column count matches the regex parser on the corpus (92 against ANTLR's 85), and on the server timings
batch it finds 34 columns, where the regex parser finds 35 and ANTLR 31.

For anything else that doesn't parse, ANTLR recovers inside a rule and Parlot skips to the next WHERE
predicate or statement. The DirectQuery statements that use SQL the grammar doesn't cover (`CASE`,
`CAST ... AS`, `SELECT DISTINCT`, nested `TOP`) still differ in what each recovery keeps (5 of 14). The ANTLR
quirks that the visitors rely on are reproduced, not fixed. For example, a multi-column `ON` condition gives no
relationship, and parenthesized predicates are not looked into.

## Benchmarks

`dotnet run -c Release -f net8.0 --project tests/DaxStudio.Parsers.Benchmarks -- --filter "*" --runtimes net8.0 net10.0`
(`-- --check` describes the workloads and verifies that ANTLR and Parlot format them identically).

| Workload | Queries | Size | Description |
|---|---:|---:|---|
| Corpus | 111 | 19 KB | the queries from the parser tests, mostly small |
| ServerTimings | 500 | 349 KB | the SE events of a heavy report query: joins, `WITH` expressions, callbacks, `DEFINE TABLE` semi-join batches, date ranges, tuple filters, auto date tables, 2/3 with an estimated size footer |
| LongInLists | 20 | 173 KB | filters with 1,000 numbers and 250 strings, or 200 tuples |

The formatting, analysis and grouping benchmarks run the same code the application runs, with the default
options (format, simplify, ISO dates on). Formatting includes the post-processing shared by all paths
(`ExtractEstimatedSize`, `ContainsCallback`). The analysis and the grouping read the formatted text, as in the
application. Ratios are against the net8.0 baseline. Apple M4 Pro, macOS 15.8, BenchmarkDotNet 0.15.8 default
job. Two other single-threaded processes were running on the 14-core machine.

### Formatting every SE event (`TraceStorageEngineEvent.FormatQuery`)

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Regex | 399.9 μs | 0.88 | 593.81 KB | 1.00 |
| Corpus | net10.0 | Antlr | 3,002.2 μs | 6.62 | 8250.36 KB | 13.89 |
| Corpus | net10.0 | Parlot | 259.4 μs | 0.57 | 470.29 KB | 0.79 |
| Corpus | net8.0 | Regex | 453.6 μs | 1.00 | 593.81 KB | 1.00 |
| Corpus | net8.0 | Antlr | 3,458.3 μs | 7.62 | 8494.8 KB | 14.31 |
| Corpus | net8.0 | Parlot | 294.5 μs | 0.65 | 470.29 KB | 0.79 |
| LongInLists | net10.0 | Regex | 3,421.2 μs | 0.96 | 4848.97 KB | 1.00 |
| LongInLists | net10.0 | Antlr | 5,198.7 μs | 1.45 | 13306.56 KB | 2.74 |
| LongInLists | net10.0 | Parlot | 1,538.9 μs | 0.43 | 2383.13 KB | 0.49 |
| LongInLists | net8.0 | Regex | 3,574.0 μs | 1.00 | 4849.08 KB | 1.00 |
| LongInLists | net8.0 | Antlr | 5,756.0 μs | 1.61 | 13533.78 KB | 2.79 |
| LongInLists | net8.0 | Parlot | 1,637.5 μs | 0.46 | 2383.13 KB | 0.49 |
| ServerTimings | net10.0 | Regex | 9,562.9 μs | 0.85 | 27319.32 KB | 1.00 |
| ServerTimings | net10.0 | Antlr | 21,254.5 μs | 1.89 | 54920.73 KB | 2.01 |
| ServerTimings | net10.0 | Parlot | 4,829.6 μs | 0.43 | 9450.19 KB | 0.35 |
| ServerTimings | net8.0 | Regex | 11,241.1 μs | 1.00 | 27319.4 KB | 1.00 |
| ServerTimings | net8.0 | Antlr | 23,773.0 μs | 2.11 | 57443.52 KB | 2.10 |
| ServerTimings | net8.0 | Parlot | 5,325.3 μs | 0.47 | 9450.2 KB | 0.35 |

### Analysis ("Show in model diagram")

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Regex | 466.5 μs | 0.90 | 941.31 KB | 0.99 |
| Corpus | net10.0 | Antlr | 729.9 μs | 1.41 | 1322.13 KB | 1.40 |
| Corpus | net10.0 | Parlot | 176.3 μs | 0.34 | 341.74 KB | 0.36 |
| Corpus | net8.0 | Regex | 518.1 μs | 1.00 | 946.34 KB | 1.00 |
| Corpus | net8.0 | Antlr | 1,177.6 μs | 2.27 | 1535.73 KB | 1.62 |
| Corpus | net8.0 | Parlot | 214.8 μs | 0.41 | 360.88 KB | 0.38 |
| LongInLists | net10.0 | Regex | 1,532.7 μs | 0.93 | 2365.01 KB | 1.00 |
| LongInLists | net10.0 | Antlr | 4,416.2 μs | 2.69 | 11167.62 KB | 4.72 |
| LongInLists | net10.0 | Parlot | 1,332.6 μs | 0.81 | 2532.92 KB | 1.07 |
| LongInLists | net8.0 | Regex | 1,640.0 μs | 1.00 | 2368.03 KB | 1.00 |
| LongInLists | net8.0 | Antlr | 4,753.2 μs | 2.90 | 11233.13 KB | 4.74 |
| LongInLists | net8.0 | Parlot | 1,533.7 μs | 0.94 | 2538.74 KB | 1.07 |
| ServerTimings | net10.0 | Regex | 5,719.4 μs | 0.92 | 9659.41 KB | 0.99 |
| ServerTimings | net10.0 | Antlr | 12,129.3 μs | 1.95 | 17928.18 KB | 1.84 |
| ServerTimings | net10.0 | Parlot | 1,999.5 μs | 0.32 | 3384.43 KB | 0.35 |
| ServerTimings | net8.0 | Regex | 6,204.3 μs | 1.00 | 9745.76 KB | 1.00 |
| ServerTimings | net8.0 | Antlr | 21,621.7 μs | 3.48 | 20237.15 KB | 2.08 |
| ServerTimings | net8.0 | Parlot | 2,468.6 μs | 0.40 | 3599.49 KB | 0.37 |

### Query grouping (Server Timings "Group" column)

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Antlr | 2,863.8 μs | 0.80 | 7858.04 KB | 0.98 |
| Corpus | net10.0 | Parlot | 253.9 μs | 0.07 | 608.92 KB | 0.08 |
| Corpus | net8.0 | Antlr | 3,593.5 μs | 1.00 | 8047.23 KB | 1.00 |
| Corpus | net8.0 | Parlot | 293.2 μs | 0.08 | 619.05 KB | 0.08 |
| LongInLists | net10.0 | Antlr | 3,488.8 μs | 0.94 | 9180.82 KB | 0.99 |
| LongInLists | net10.0 | Parlot | 498.0 μs | 0.13 | 859.48 KB | 0.09 |
| LongInLists | net8.0 | Antlr | 3,720.7 μs | 1.00 | 9237.43 KB | 1.00 |
| LongInLists | net8.0 | Parlot | 587.0 μs | 0.16 | 862.04 KB | 0.09 |
| ServerTimings | net10.0 | Antlr | 24,758.3 μs | 0.70 | 54483.66 KB | 0.97 |
| ServerTimings | net10.0 | Parlot | 2,260.7 μs | 0.06 | 5109.85 KB | 0.09 |
| ServerTimings | net8.0 | Antlr | 35,216.2 μs | 1.00 | 56161.08 KB | 1.00 |
| ServerTimings | net8.0 | Parlot | 2,479.3 μs | 0.07 | 5190.38 KB | 0.09 |

### DirectQuery SQL analysis (86 statements, 28 KB)

| Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---:|---:|---:|---:|
| net10.0 | Regex | 5,958.7 μs | 0.91 | 11749.84 KB | 1.01 |
| net10.0 | Antlr | 1,934.5 μs | 0.30 | 4870.8 KB | 0.42 |
| net10.0 | Parlot | 204.1 μs | 0.03 | 421.76 KB | 0.04 |
| net8.0 | Regex | 6,526.7 μs | 1.00 | 11608.75 KB | 1.00 |
| net8.0 | Antlr | 2,381.1 μs | 0.36 | 5026.41 KB | 0.43 |
| net8.0 | Parlot | 246.6 μs | 0.04 | 422.62 KB | 0.04 |

### Parsing only (no tree walk; the regex parser has no separate parse step)

`Antlr` creates a lexer and parser per query, as `AntlrXmSqlFormatter` and `XmSqlQueryGrouper` do.
`AntlrReusedSll` reuses one in SLL mode, as `AntlrXmSqlParser` does.

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Antlr | 2,540.8 μs | 0.87 | 7767.16 KB | 1.00 |
| Corpus | net10.0 | AntlrReusedSll | 345.4 μs | 0.12 | 790.23 KB | 0.10 |
| Corpus | net10.0 | Parlot | 105.8 μs | 0.04 | 173.01 KB | 0.02 |
| Corpus | net8.0 | Antlr | 2,932.0 μs | 1.00 | 7776.02 KB | 1.00 |
| Corpus | net8.0 | AntlrReusedSll | 377.7 μs | 0.13 | 790.33 KB | 0.10 |
| Corpus | net8.0 | Parlot | 121.2 μs | 0.04 | 173.01 KB | 0.02 |
| LongInLists | net10.0 | Antlr | 3,313.2 μs | 0.98 | 8721.16 KB | 1.00 |
| LongInLists | net10.0 | AntlrReusedSll | 3,063.3 μs | 0.90 | 7378.9 KB | 0.85 |
| LongInLists | net10.0 | Parlot | 474.4 μs | 0.14 | 759.61 KB | 0.09 |
| LongInLists | net8.0 | Antlr | 3,395.0 μs | 1.00 | 8722.57 KB | 1.00 |
| LongInLists | net8.0 | AntlrReusedSll | 3,221.7 μs | 0.95 | 7378.9 KB | 0.85 |
| LongInLists | net8.0 | Parlot | 541.5 μs | 0.16 | 759.61 KB | 0.09 |
| ServerTimings | net10.0 | Antlr | 15,966.3 μs | 0.90 | 45565.81 KB | 1.00 |
| ServerTimings | net10.0 | AntlrReusedSll | 5,107.3 μs | 0.29 | 11019.95 KB | 0.24 |
| ServerTimings | net10.0 | Parlot | 1,554.1 μs | 0.09 | 1907.17 KB | 0.04 |
| ServerTimings | net8.0 | Antlr | 17,708.3 μs | 1.00 | 45637.32 KB | 1.00 |
| ServerTimings | net8.0 | AntlrReusedSll | 5,569.0 μs | 0.31 | 11027.75 KB | 0.24 |
| ServerTimings | net8.0 | Parlot | 1,748.9 μs | 0.10 | 1907.17 KB | 0.04 |

The formatting walker skips the name-cleaning regexes when a name can't match them (no `(`, `-`, `<`,
`RowNumber`), which accounts for part of the formatting gain over ANTLR. That is a separate commit
(`c43d063f`). The ANTLR visitor was left unchanged so it stays a faithful baseline.

## Windows

`DaxStudio.Tests` (net472/net8.0-windows) was **not run**: devbox1 was unreachable when this was written
(dev tunnel host not running). The changes outside `DaxStudio.Parsers` are the 4 call sites listed above.
The parser tests that test project shares with `DaxStudio.Parsers.Tests` pass on macOS, and the
ANTLR ones also pass against Parlot. `xmSqlProcessing.cs` expects the same formatter output, which Parlot produces. Run
`build.cmd` and the test project on Windows before merging.

## Possible next steps

1. Make the grammar-based parser the default: Parlot is faster than the regex path on every
   workload and reads the formatted text better than ANTLR.
2. Remove the ANTLR grammars, visitors, `Antlr4.Runtime.Standard` and the `Antlr4BuildTasks` build step
   (the ANTLR tool runs on Java at build time). The differential tests would then compare against
   recorded outputs instead.
3. Add the two cross-platform test projects to the solution and CI.
