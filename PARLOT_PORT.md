# Parlot for the xmSQL and DirectQuery SQL parsers

This branch adds a [Parlot](https://github.com/sebastienros/parlot) implementation of the grammar-based storage
engine parsers, next to the ANTLR one, and points the "Use New Parser for SE Queries" option and the query
grouping at it. On valid xmSQL and DirectQuery SQL the results are the same as ANTLR's, and it is faster than both ANTLR and the default regex
parser on every path measured. On a 500-event server timings batch, Parlot formats the events in **0.46x the time of
the regex path** that runs today, while ANTLR takes about 2x. Parsing alone is **about 10x faster than ANTLR** and
allocates **25x less**.

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

The Parlot package is the only new dependency of `DaxStudio.Parsers`, which stays C# 8 and still targets
net472. The branch uses `2.2.0` from nuget.org.

### What Parlot 2.2.0 is used for

- **Non-fatal errors.** Text the grammar skips is reported as a Parlot diagnostic (`.Warning(...)`), and
  `XmSqlQuery.Diagnostics` exposes them, with `HasErrors`/`ErrorOffset` derived from it. Two places skip text:
  a WHERE predicate that doesn't parse (kept as an `Unparsed` filter, as before) and text between statements
  that doesn't start one. The estimated size footer that the trace appends is read as such and is not an error.
  Collecting diagnostics makes every choice a little slower (about 8% on the server timings parse) even when
  nothing is reported, so the grammar parses without them first and re-parses with
  `CollectDiagnostics` only when it actually skipped something. Valid input pays nothing.
- **Keywords.** `Keyword(text, identifierPart)` with letters, digits and `_` as the identifier part, so that
  `COUNT` doesn't match in `COUNT_BIG` (this already existed in 2.1.0).
- **`Operators()`** is not used. It can't copy the ANTLR DirectQuery grammar, whose `IS [NOT] NULL` is a postfix
  looser than `AND`/`OR` that ANTLR's left-recursive rule still lets `OR ...` follow. A precedence table rightly
  can't: that ordering is a bug of the ANTLR grammar, which the Parlot grammar now fixes (see
  [below](#a-bug-of-the-antlr-directquery-grammar-fixed-here)). With the T-SQL precedences a table would fit, but the
  DirectQuery grammar reads each expression as a flat chain anyway, to split a join condition at its last `=` as
  ANTLR does, and keeps its own small precedence climber over that chain. Valued levels (a factory that
  receives the operator, such as `IS NOT NULL` against `IS NULL`) would have covered the rest. The xmSQL `expression`
  rule is a flat `atom (op atom)*` list with no precedence, so it has no use for a table either.
- **Bounded recursion**. Input nested too deeply for the thread's stack now fails the parse with a
  `ParseException` instead of overflowing the stack, which ended the process. The entry points catch it and return
  their fallback values (null text, a failed analysis). About 700 nested parentheses parse on a 1 MB stack, the
  default of a Windows thread; real queries nest a few levels. `XmSqlGrammarTests` checks 100,000 levels.
- **`WhiteSpaceSet.SpaceTabCrLf`** is exactly the `WS : [ \t\r\n]+` rule of both grammars, but it is not used.
  ANTLR lexes any other white space char (`\f`, a non-breaking space) as `ANY_CHAR` and its error recovery mostly
  drops it, which Parlot's default set, skipping it, reproduces better: with such a char injected into each corpus
  query, 117 of 336 formatted outputs differ from ANTLR's with the default set, and 228 with `SpaceTabCrLf`. The
  DirectQuery white space parser also skips the chars the lexer can't read, so a set can't replace it.
- **`CommentsBuilder`** no longer builds a list each time it skips white space, so it now allocates exactly
  what the hand-written xmSQL scanner of white space and `/* */` comments does (it added 320 KB per server timings
  batch before). It is still a few percent slower (1-4% on analysis and grouping, two A/B rounds), a choice between
  two parsers in a loop against one loop over the chars, so the scanner stays.
- **Pooled strings** (`ToPooledString()`) were tried for the table and column names, which the analysis and
  grouping walkers turn into strings (one `Substring` per reference). The AST took them as pooled strings, and the
  walkers read them. Allocations dropped by 335 KB per server timings batch for analysis and 268 KB for grouping
  (about 10%), but every run got slower: parsing alone by 13-15%, with 100 KB more for the new fields, analysis by
  about 5%, formatting by 2%. A pool lookup costs about what the short `Substring` it saves does, and the
  formatter pays it for names it doesn't need, so the AST keeps text spans. The analysis results dedupe the names
  in their own dictionaries, so pooling wouldn't shrink what they keep either.
- **`Ternary`** has no use: neither grammar has a conditional operator (`CASE` is only a DirectQuery token).

A 2.2.0 preview alone also made the parse faster and lighter than 2.1.0 with no grammar change (server timings,
net8.0: 1,749 µs and 1,907 KB before; 1,630 µs and 1,836 KB in an A/B run on the same machine; LongInLists
760 KB to 447 KB).

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
- **DirectQuery analysis:** identical on 70 of the 73 clean statements, alone and accumulated. The other 3 are the
  `IS NULL` precedence fix described below, where Parlot finds filters ANTLR loses; the test lists them and checks
  that they still differ. Parlot accepts and rejects exactly the statements ANTLR accepts and rejects.
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
quirks that the visitors rely on are reproduced, with one exception below. For example, a multi-column `ON` condition
gives no relationship, and parenthesized predicates are not looked into.

### A bug of the ANTLR DirectQuery grammar, fixed here

`DirectQuerySql.g4` lists `expression IS NOT? NULL_` and `NOT expression` after the `AND` and `OR` alternatives of its
left-recursive `expression` rule, which gives them the lowest precedence: `IS NULL` applies to everything before it
and `NOT` to everything after it. In T-SQL, `IS NULL` binds like a comparison, and `NOT` tighter than `AND`. So ANTLR
reads

```sql
WHERE [t4].[Email] IS NULL OR [t4].[Phone] IS NOT NULL
```

as `(([Email] IS NULL) OR [Phone]) IS NOT NULL`. `DirectQuerySqlAnalysisVisitor` then sees an `IS` whose operand is not
a column and records nothing: neither `Email` nor `Phone` is shown as a filtered column. This happens whenever an
`IS [NOT] NULL` ends a chain of `AND`/`OR`, even `[Age] = 40 AND [Email] IS NULL`, which loses both filters. The shipping
"Use New Parser for SE Queries" option has this bug (the default regex parser finds no table at all for these
statements).

The Parlot grammar uses the T-SQL precedences, so it records those filters, and 3 corpus statements analyse
differently from ANTLR, each with more filtered columns:

| Statement (WHERE clause) | ANTLR | Parlot |
|---|---|---|
| `[Email] IS NULL OR [Phone] IS NOT NULL` | no filter | `Email`, `Phone` |
| `[Age] = 40 AND [Email] IS NULL` | no filter | `Age` (`= 40`), `Email` |
| `[Price] * 2 > 100 AND ([Stock] = 0 OR [Stock] IS NULL) AND [Name] = NULL` | no `Stock` | `Stock` (`= 0`) |

`NOT` reading only a comparison changes the tree (`NOT a = 1 OR b = 2` is `(NOT a = 1) OR b = 2`), but not the
analysis, as the visitor looks inside `NOT` either way. The ANTLR grammar is left as it is, since it's the oracle of the
differential tests; the fix there is to move the `IS NOT? NULL_` alternative next to the comparison one, and `NOT expression` between
the comparison and `AND` alternatives, since ANTLR takes their precedence from their order.

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
application. Ratios are against the net8.0 baseline. Apple M4 Pro, macOS 15.8, BenchmarkDotNet 0.15.8, Parlot 2.2.0.
These tables are one quick pass of one process per benchmark (`--launchCount 1 --warmupCount 8 --iterationCount 5
--iterationTime 250`), so treat differences of a few percent as noise; the allocations are exact. The grouping table
uses `--warmupCount 15 --iterationCount 10 --iterationTime 500`: with the shorter job its net8.0 LongInLists rows, ANTLR
and Parlot alike, stayed about 50% above their settled values.

### Formatting every SE event (`TraceStorageEngineEvent.FormatQuery`)

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Regex | 378.5 μs | 0.88 | 593.81 KB | 1.00 |
| Corpus | net10.0 | Antlr | 2,901.5 μs | 6.72 | 8250.36 KB | 13.89 |
| Corpus | net10.0 | Parlot | 252.1 μs | 0.58 | 454.75 KB | 0.77 |
| Corpus | net8.0 | Regex | 432.0 μs | 1.00 | 593.81 KB | 1.00 |
| Corpus | net8.0 | Antlr | 3,201.0 μs | 7.41 | 8494.85 KB | 14.31 |
| Corpus | net8.0 | Parlot | 286.2 μs | 0.66 | 460.44 KB | 0.78 |
| LongInLists | net10.0 | Regex | 3,244.8 μs | 0.95 | 4849 KB | 1.00 |
| LongInLists | net10.0 | Antlr | 4,927.4 μs | 1.44 | 13306.57 KB | 2.74 |
| LongInLists | net10.0 | Parlot | 1,385.5 μs | 0.40 | 2068.82 KB | 0.43 |
| LongInLists | net8.0 | Regex | 3,433.6 μs | 1.00 | 4849.39 KB | 1.00 |
| LongInLists | net8.0 | Antlr | 5,542.0 μs | 1.61 | 13533.96 KB | 2.79 |
| LongInLists | net8.0 | Parlot | 1,528.3 μs | 0.45 | 2070.08 KB | 0.43 |
| ServerTimings | net10.0 | Regex | 9,000.5 μs | 0.84 | 27319.29 KB | 1.00 |
| ServerTimings | net10.0 | Antlr | 21,234.2 μs | 1.98 | 54929.74 KB | 2.01 |
| ServerTimings | net10.0 | Parlot | 4,892.7 μs | 0.46 | 9301.53 KB | 0.34 |
| ServerTimings | net8.0 | Regex | 10,744.3 μs | 1.00 | 27319.67 KB | 1.00 |
| ServerTimings | net8.0 | Antlr | 22,772.5 μs | 2.12 | 57444.38 KB | 2.10 |
| ServerTimings | net8.0 | Parlot | 4,940.2 μs | 0.46 | 9335.08 KB | 0.34 |

### Analysis ("Show in model diagram")

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Regex | 432.1 μs | 0.90 | 941.31 KB | 0.99 |
| Corpus | net10.0 | Antlr | 672.3 μs | 1.40 | 1322.13 KB | 1.40 |
| Corpus | net10.0 | Parlot | 171.3 μs | 0.36 | 323.77 KB | 0.34 |
| Corpus | net8.0 | Regex | 481.7 μs | 1.00 | 946.34 KB | 1.00 |
| Corpus | net8.0 | Antlr | 1,092.7 μs | 2.27 | 1535.73 KB | 1.62 |
| Corpus | net8.0 | Parlot | 202.6 μs | 0.42 | 347.88 KB | 0.37 |
| LongInLists | net10.0 | Regex | 1,461.7 μs | 0.95 | 2365.01 KB | 1.00 |
| LongInLists | net10.0 | Antlr | 4,124.4 μs | 2.68 | 11167.62 KB | 4.72 |
| LongInLists | net10.0 | Parlot | 1,187.6 μs | 0.77 | 2218.31 KB | 0.94 |
| LongInLists | net8.0 | Regex | 1,539.6 μs | 1.00 | 2368.06 KB | 1.00 |
| LongInLists | net8.0 | Antlr | 4,467.5 μs | 2.90 | 11233.13 KB | 4.74 |
| LongInLists | net8.0 | Parlot | 1,380.4 μs | 0.90 | 2225.07 KB | 0.94 |
| ServerTimings | net10.0 | Regex | 5,333.4 μs | 0.91 | 9659.41 KB | 0.99 |
| ServerTimings | net10.0 | Antlr | 10,967.2 μs | 1.87 | 17928.18 KB | 1.84 |
| ServerTimings | net10.0 | Parlot | 1,977.8 μs | 0.34 | 3249.98 KB | 0.33 |
| ServerTimings | net8.0 | Regex | 5,872.5 μs | 1.00 | 9745.76 KB | 1.00 |
| ServerTimings | net8.0 | Antlr | 19,048.0 μs | 3.24 | 20237.15 KB | 2.08 |
| ServerTimings | net8.0 | Parlot | 2,351.4 μs | 0.40 | 3495.88 KB | 0.36 |

### Query grouping (Server Timings "Group" column)

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Antlr | 2,757.8 μs | 0.82 | 7858.04 KB | 0.98 |
| Corpus | net10.0 | Parlot | 253.2 μs | 0.08 | 589.79 KB | 0.07 |
| Corpus | net8.0 | Antlr | 3,365.6 μs | 1.00 | 8047.3 KB | 1.00 |
| Corpus | net8.0 | Parlot | 282.1 μs | 0.08 | 608.43 KB | 0.08 |
| LongInLists | net10.0 | Antlr | 3,287.0 μs | 0.94 | 9180.82 KB | 0.99 |
| LongInLists | net10.0 | Parlot | 430.4 μs | 0.12 | 543.92 KB | 0.06 |
| LongInLists | net8.0 | Antlr | 3,493.2 μs | 1.00 | 9237.43 KB | 1.00 |
| LongInLists | net8.0 | Parlot | 476.4 μs | 0.14 | 548.67 KB | 0.06 |
| ServerTimings | net10.0 | Antlr | 22,278.4 μs | 0.70 | 54483.68 KB | 0.97 |
| ServerTimings | net10.0 | Parlot | 2,057.5 μs | 0.06 | 4917.62 KB | 0.09 |
| ServerTimings | net8.0 | Antlr | 31,659.7 μs | 1.00 | 56160.87 KB | 1.00 |
| ServerTimings | net8.0 | Parlot | 2,446.3 μs | 0.08 | 5052.81 KB | 0.09 |

### DirectQuery SQL analysis (86 statements, 28 KB)

| Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---:|---:|---:|---:|
| net10.0 | Regex | 7,445.9 μs | 1.13 | 11750.34 KB | 1.01 |
| net10.0 | Antlr | 1,797.2 μs | 0.27 | 4870.84 KB | 0.42 |
| net10.0 | Parlot | 184.8 μs | 0.03 | 408.02 KB | 0.04 |
| net8.0 | Regex | 6,596.8 μs | 1.01 | 11608.84 KB | 1.00 |
| net8.0 | Antlr | 2,220.5 μs | 0.34 | 5026.41 KB | 0.43 |
| net8.0 | Parlot | 219.9 μs | 0.03 | 412.01 KB | 0.04 |

### Parsing only (no tree walk; the regex parser has no separate parse step)

`Antlr` creates a lexer and parser per query, as `AntlrXmSqlFormatter` and `XmSqlQueryGrouper` do.
`AntlrReusedSll` reuses one in SLL mode, as `AntlrXmSqlParser` does.

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Antlr | 2,489.6 μs | 0.91 | 7767.16 KB | 1.00 |
| Corpus | net10.0 | AntlrReusedSll | 327.0 μs | 0.12 | 790.23 KB | 0.10 |
| Corpus | net10.0 | Parlot | 108.0 μs | 0.04 | 162.63 KB | 0.02 |
| Corpus | net8.0 | Antlr | 2,726.8 μs | 1.00 | 7776.02 KB | 1.00 |
| Corpus | net8.0 | AntlrReusedSll | 356.1 μs | 0.13 | 790.33 KB | 0.10 |
| Corpus | net8.0 | Parlot | 124.1 μs | 0.05 | 164.97 KB | 0.02 |
| LongInLists | net10.0 | Antlr | 3,410.1 μs | 1.03 | 8721.16 KB | 1.00 |
| LongInLists | net10.0 | AntlrReusedSll | 2,918.9 μs | 0.88 | 7378.9 KB | 0.85 |
| LongInLists | net10.0 | Parlot | 409.2 μs | 0.12 | 446.25 KB | 0.05 |
| LongInLists | net8.0 | Antlr | 3,317.2 μs | 1.00 | 8722.57 KB | 1.00 |
| LongInLists | net8.0 | AntlrReusedSll | 3,034.5 μs | 0.91 | 7378.9 KB | 0.85 |
| LongInLists | net8.0 | Parlot | 445.7 μs | 0.13 | 446.88 KB | 0.05 |
| ServerTimings | net10.0 | Antlr | 15,256.1 μs | 0.93 | 45565.81 KB | 1.00 |
| ServerTimings | net10.0 | AntlrReusedSll | 4,892.3 μs | 0.30 | 11019.95 KB | 0.24 |
| ServerTimings | net10.0 | Parlot | 1,559.9 μs | 0.09 | 1811.49 KB | 0.04 |
| ServerTimings | net8.0 | Antlr | 16,458.9 μs | 1.00 | 45637.32 KB | 1.00 |
| ServerTimings | net8.0 | AntlrReusedSll | 5,274.4 μs | 0.32 | 11027.75 KB | 0.24 |
| ServerTimings | net8.0 | Parlot | 1,677.5 μs | 0.10 | 1839.74 KB | 0.04 |

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
