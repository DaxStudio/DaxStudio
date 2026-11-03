# Parlot for the xmSQL and DirectQuery SQL parsers

This branch adds a [Parlot](https://github.com/sebastienros/parlot) implementation of the grammar-based storage
engine parsers, next to the ANTLR one, and points the "Use New Parser for SE Queries" option and the query
grouping at it. On valid xmSQL and DirectQuery SQL the results are the same as ANTLR's, and it is faster than both ANTLR and the default regex
parser on every path measured. On a 500-event server timings batch, Parlot formats the events in **0.48x the time of
the regex path** that runs today, while ANTLR takes about 2.6x. Parsing alone is **about 12x faster than ANTLR** and
allocates **31x less**.

The ANTLR classes are unchanged (apart from `XmSqlQueryGrouper.ComputeFingerprint`, which is now virtual) and still build, so the change can land in steps. They are also the
oracle that the tests compare Parlot against.

## What changed

| Area | Files |
|---|---|
| xmSQL grammar | `XmSql/XmSqlLexer.cs` (the lexer rules of `xmSQL.g4`, as parsers), `XmSql/XmSqlGrammar.cs` (the parser rules), `XmSql/XmSqlAst.cs` (a tree whose nodes mirror the ANTLR rule contexts) |
| Tree walkers | `XmSql/XmSqlFormattingWalker.cs`, `XmSqlAnalysisWalker.cs`, `XmSqlFingerprintWalker.cs`: ports of the three ANTLR visitors, method for method |
| DirectQuery SQL | `DirectQuery/*`: lexer, grammar, tree and analysis walker for `DirectQuerySql.g4` |
| Entry points | `ParlotXmSqlFormatter` (same contract as `AntlrXmSqlFormatter`), `ParlotXmSqlParser : IXmSqlParser` (as `AntlrXmSqlParser`), `ParlotXmSqlQueryGrouper : XmSqlQueryGrouper` |
| Application | `TraceStorageEngineEvent`, `ServerTimesViewModel`, `ServerTimesModel` and `XmSqlErdViewModel` use the Parlot classes where they used the ANTLR ones. The option keeps its name and default (off). |
| Tests | `tests/DaxStudio.Parsers.Tests` gets the differential tests (net8.0 only, as they read their corpus with System.Text.Json). `tests/DaxStudio.Parsers.ParlotTests` reruns `AntlrXmSqlParserTests`, `AntlrXmSqlFormatterTests` and `XmSqlFingerprintTests` with the ANTLR classes aliased to the Parlot ones. |
| Benchmarks | `tests/DaxStudio.Parsers.Benchmarks` (BenchmarkDotNet, net8.0 and net10.0) |

The Parlot package is the only new dependency of `DaxStudio.Parsers`, which stays C# 8 and still targets
net472. The branch uses `2.2.1` from nuget.org.

### What Parlot 2.2.1 is used for

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
- **Combinators for the tokens.** The tokens are built from `AnyOf`, `NoneOf`, `Text` and `Capture`, and the recovery,
  footers and lookaheads from `WhenNotFollowedBy`, `Deferred` and `Eof`. Four things stay scanned by hand: xmSQL
  `BRACKETED_NAME`, a longest match over an ambiguous rule; `NUMBER`, because a chain of captures made the 1,000-number
  lists 86% slower to parse against 10% for all the changes with the scanner; and the DirectQuery trivia with the two
  tokens it needs, as it skips the tokens the lexer fails to read.
- **`WithComments`** skips xmSQL's `/* */` comments, with a comment parser of its own since `WithMultiLine` fails the
  parse when a comment doesn't close. It only runs on text containing `/*`, so queries without comments don't pay for it.
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

- **Corpus.** The 118 distinct xmSQL queries and 86 DirectQuery statements (73 valid for the ANTLR grammar)
  that the existing parser tests feed the parsers were collected into `tests/DaxStudio.Parsers.Tests/Corpus`.
  The xmSQL ones are used both as traced and as formatted by DAX Studio.
- **Formatter:** every query, with all 8 combinations of format / simplify / dates, with and without name
  remaps and date column ids: **1,888 of 1,888 outputs are identical to ANTLR's**, including the queries ANTLR
  reports syntax errors for.
- **Analysis:** each query with 5 metrics variants and `ParseQuery`, compared by dumping the whole
  `XmSqlAnalysis` graph: **identical on every input ANTLR parses without errors**. The same
  holds for a single batch over all of them, and for `ParseQueries`.
- **Fingerprints and grouping:** identical on every input ANTLR parses without errors.
- **DirectQuery analysis:** identical on 70 of the 73 clean statements, alone and accumulated. The other 3 are the
  `IS NULL` precedence fix described below, where Parlot finds filters ANTLR loses; the test lists them and checks
  that they still differ. Parlot accepts and rejects exactly the statements ANTLR accepts and rejects.
- **Lexer:** the tokens are compared with the ANTLR lexer on 250,000 random strings built from the
  characters that matter (bracketed names with nested parentheses and `]]` escapes, quoted names, strings,
  numbers).
- **Existing tests:** the 87 tests of `AntlrXmSqlParserTests`, `AntlrXmSqlFormatterTests` and
  `XmSqlFingerprintTests` also pass against the Parlot classes, through the aliased project.

Run them with `dotnet test tests/DaxStudio.Parsers.Tests` and `dotnet test tests/DaxStudio.Parsers.ParlotTests`.

### Where the results differ, on purpose

The analysis and the grouping read each event's **formatted** query (`evt.Query`), and DAX Studio's own
formatter writes things the xmSQL grammar doesn't have: `/* 2024-01-31 */` comments after dates, `PFCAST`
without its type, `CallbackDataID ( <DAX> ) ( PFDATAID ( ... ) )`, estimated sizes inside truncated lists and
after the query, and `, ` between statements when line breaks are off. ANTLR reports syntax errors on that
text and recovers with partial results, so some of the formatted corpus queries get a different analysis. The Parlot grammar reads that text, so on those
queries it finds **more**. For example, it keeps all the values of a date `IN` list where ANTLR keeps the first
one. The tests check that Parlot reads every formatted corpus query without errors and finds at least what ANTLR
finds. On the benchmark workloads, the Parlot analysis finds the same tables and joins as both other parsers.
Its column count matches the regex parser on the corpus (95 against ANTLR's 89), and on the server timings
batch it finds 34 columns, where the regex parser finds 35 and ANTLR 33.

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
| Corpus | 116 | 20 KB | the queries from the parser tests, mostly small |
| ServerTimings | 500 | 349 KB | the SE events of a heavy report query: joins, `WITH` expressions, callbacks, `DEFINE TABLE` semi-join batches, date ranges, tuple filters, auto date tables, 2/3 with an estimated size footer |
| LongInLists | 20 | 173 KB | filters with 1,000 numbers and 250 strings, or 200 tuples |

The formatting, analysis and grouping benchmarks run the same code the application runs, with the default
options (format, simplify, ISO dates on). Formatting includes the post-processing shared by all paths
(`ExtractEstimatedSize`, `ContainsCallback`). The analysis and the grouping read the formatted text, as in the
application. Ratios are against the net8.0 baseline. Apple M4 Pro, macOS 15.8, BenchmarkDotNet 0.15.8, Parlot 2.2.1.
These tables are one quick pass of one process per benchmark (`--launchCount 1 --warmupCount 8 --iterationCount 5
--iterationTime 250`), so treat differences of a few percent as noise; the allocations are exact. The grouping table
uses `--warmupCount 15 --iterationCount 10 --iterationTime 500`: with the shorter job its net8.0 LongInLists rows, ANTLR
and Parlot alike, stayed about 50% above their settled values.

Merging `develop` made ANTLR slower: its only change to `xmSQL.g4` lets a `WITH` definition drop its parentheses, and
the ANTLR parse of the server timings batch went from 45.6 MB to 58.3 MB. The likely cause is that an atom can also
start with `(`, so ANTLR has to look further ahead there. The Parlot grammar reads one expression and checks its shape
afterwards, so its allocations didn't change.

### Formatting every SE event (`TraceStorageEngineEvent.FormatQuery`)

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Regex | 418.0 μs | 0.87 | 664.57 KB | 1.00 |
| Corpus | net10.0 | Antlr | 3,654.6 μs | 7.60 | 9362.78 KB | 14.09 |
| Corpus | net10.0 | Parlot | 281.8 μs | 0.59 | 485.83 KB | 0.73 |
| Corpus | net8.0 | Regex | 480.6 μs | 1.00 | 664.58 KB | 1.00 |
| Corpus | net8.0 | Antlr | 3,893.2 μs | 8.10 | 9621.5 KB | 14.48 |
| Corpus | net8.0 | Parlot | 328.0 μs | 0.68 | 491.68 KB | 0.74 |
| LongInLists | net10.0 | Regex | 3,353.4 μs | 0.93 | 4848.99 KB | 1.00 |
| LongInLists | net10.0 | Antlr | 5,220.9 μs | 1.45 | 13306.57 KB | 2.74 |
| LongInLists | net10.0 | Parlot | 1,443.5 μs | 0.40 | 2068.82 KB | 0.43 |
| LongInLists | net8.0 | Regex | 3,610.9 μs | 1.00 | 4849.39 KB | 1.00 |
| LongInLists | net8.0 | Antlr | 5,885.3 μs | 1.63 | 13533.94 KB | 2.79 |
| LongInLists | net8.0 | Parlot | 1,574.9 μs | 0.44 | 2070.1 KB | 0.43 |
| ServerTimings | net10.0 | Regex | 9,758.3 μs | 0.88 | 27319.31 KB | 1.00 |
| ServerTimings | net10.0 | Antlr | 25,304.8 μs | 2.28 | 67546.16 KB | 2.47 |
| ServerTimings | net10.0 | Parlot | 5,431.9 μs | 0.49 | 9327.23 KB | 0.34 |
| ServerTimings | net8.0 | Regex | 11,091.9 μs | 1.00 | 27319.56 KB | 1.00 |
| ServerTimings | net8.0 | Antlr | 29,182.2 μs | 2.63 | 70110.18 KB | 2.57 |
| ServerTimings | net8.0 | Parlot | 5,277.9 μs | 0.48 | 9360.78 KB | 0.34 |

### Analysis ("Show in model diagram")

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Regex | 484.1 μs | 0.94 | 970.87 KB | 0.99 |
| Corpus | net10.0 | Antlr | 693.3 μs | 1.34 | 1313.34 KB | 1.35 |
| Corpus | net10.0 | Parlot | 194.8 μs | 0.38 | 343.98 KB | 0.35 |
| Corpus | net8.0 | Regex | 517.7 μs | 1.00 | 975.9 KB | 1.00 |
| Corpus | net8.0 | Antlr | 1,009.9 μs | 1.95 | 1542.34 KB | 1.58 |
| Corpus | net8.0 | Parlot | 228.7 μs | 0.44 | 369.63 KB | 0.38 |
| LongInLists | net10.0 | Regex | 1,485.2 μs | 0.93 | 2365.01 KB | 1.00 |
| LongInLists | net10.0 | Antlr | 4,187.9 μs | 2.62 | 11167.62 KB | 4.72 |
| LongInLists | net10.0 | Parlot | 1,219.9 μs | 0.76 | 2218.31 KB | 0.94 |
| LongInLists | net8.0 | Regex | 1,601.6 μs | 1.00 | 2368.06 KB | 1.00 |
| LongInLists | net8.0 | Antlr | 4,535.1 μs | 2.83 | 11233.13 KB | 4.74 |
| LongInLists | net8.0 | Parlot | 1,393.3 μs | 0.87 | 2225.07 KB | 0.94 |
| ServerTimings | net10.0 | Regex | 5,629.1 μs | 0.94 | 9659.98 KB | 0.99 |
| ServerTimings | net10.0 | Antlr | 9,426.4 μs | 1.57 | 17375.56 KB | 1.78 |
| ServerTimings | net10.0 | Parlot | 2,021.0 μs | 0.34 | 3276.58 KB | 0.34 |
| ServerTimings | net8.0 | Regex | 5,990.9 μs | 1.00 | 9746.32 KB | 1.00 |
| ServerTimings | net8.0 | Antlr | 17,957.2 μs | 3.00 | 19933.34 KB | 2.05 |
| ServerTimings | net8.0 | Parlot | 2,439.2 μs | 0.41 | 3540.31 KB | 0.36 |

### Query grouping (Server Timings "Group" column)

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Antlr | 3,263.0 μs | 0.85 | 9023.76 KB | 0.98 |
| Corpus | net10.0 | Parlot | 277.1 μs | 0.07 | 615.02 KB | 0.07 |
| Corpus | net8.0 | Antlr | 3,856.0 μs | 1.00 | 9216.4 KB | 1.00 |
| Corpus | net8.0 | Parlot | 305.6 μs | 0.08 | 634.43 KB | 0.07 |
| LongInLists | net10.0 | Antlr | 3,301.2 μs | 0.91 | 9180.82 KB | 0.99 |
| LongInLists | net10.0 | Parlot | 429.4 μs | 0.12 | 543.92 KB | 0.06 |
| LongInLists | net8.0 | Antlr | 3,611.9 μs | 1.00 | 9237.44 KB | 1.00 |
| LongInLists | net8.0 | Parlot | 487.9 μs | 0.14 | 548.67 KB | 0.06 |
| ServerTimings | net10.0 | Antlr | 23,142.6 μs | 0.68 | 58177.23 KB | 0.97 |
| ServerTimings | net10.0 | Parlot | 2,131.0 μs | 0.06 | 4948.74 KB | 0.08 |
| ServerTimings | net8.0 | Antlr | 33,916.8 μs | 1.00 | 59950.04 KB | 1.00 |
| ServerTimings | net8.0 | Parlot | 2,659.6 μs | 0.08 | 5083.94 KB | 0.08 |

### DirectQuery SQL analysis (86 statements, 28 KB)

| Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---:|---:|---:|---:|
| net10.0 | Regex | 7,404.8 μs | 1.11 | 11750.34 KB | 1.01 |
| net10.0 | Antlr | 1,896.9 μs | 0.28 | 4870.8 KB | 0.42 |
| net10.0 | Parlot | 203.5 μs | 0.03 | 408.02 KB | 0.04 |
| net8.0 | Regex | 6,681.0 μs | 1.00 | 11608.86 KB | 1.00 |
| net8.0 | Antlr | 2,496.9 μs | 0.37 | 5026.41 KB | 0.43 |
| net8.0 | Parlot | 230.1 μs | 0.03 | 412.01 KB | 0.04 |

### Parsing only (no tree walk; the regex parser has no separate parse step)

`Antlr` creates a lexer and parser per query, as `AntlrXmSqlFormatter` and `XmSqlQueryGrouper` do.
`AntlrReusedSll` reuses one in SLL mode, as `AntlrXmSqlParser` does.

| Workload | Runtime | Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---|---|---:|---:|---:|---:|
| Corpus | net10.0 | Antlr | 3,419.4 μs | 1.02 | 8850.67 KB | 1.00 |
| Corpus | net10.0 | AntlrReusedSll | 372.4 μs | 0.11 | 837.36 KB | 0.09 |
| Corpus | net10.0 | Parlot | 128.9 μs | 0.04 | 174.79 KB | 0.02 |
| Corpus | net8.0 | Antlr | 3,354.5 μs | 1.00 | 8860.38 KB | 1.00 |
| Corpus | net8.0 | AntlrReusedSll | 394.0 μs | 0.12 | 837.5 KB | 0.09 |
| Corpus | net8.0 | Parlot | 134.5 μs | 0.04 | 177.2 KB | 0.02 |
| LongInLists | net10.0 | Antlr | 3,257.5 μs | 0.90 | 8721.16 KB | 1.00 |
| LongInLists | net10.0 | AntlrReusedSll | 3,090.1 μs | 0.85 | 7378.9 KB | 0.85 |
| LongInLists | net10.0 | Parlot | 548.4 μs | 0.15 | 446.25 KB | 0.05 |
| LongInLists | net8.0 | Antlr | 3,633.7 μs | 1.00 | 8722.57 KB | 1.00 |
| LongInLists | net8.0 | AntlrReusedSll | 3,131.7 μs | 0.86 | 7378.9 KB | 0.85 |
| LongInLists | net8.0 | Parlot | 457.9 μs | 0.13 | 446.88 KB | 0.05 |
| ServerTimings | net10.0 | Antlr | 19,434.6 μs | 0.90 | 58191.13 KB | 1.00 |
| ServerTimings | net10.0 | AntlrReusedSll | 4,947.9 μs | 0.23 | 11019.95 KB | 0.19 |
| ServerTimings | net10.0 | Parlot | 1,550.5 μs | 0.07 | 1836.24 KB | 0.03 |
| ServerTimings | net8.0 | Antlr | 21,475.4 μs | 1.00 | 58269.38 KB | 1.00 |
| ServerTimings | net8.0 | AntlrReusedSll | 5,411.0 μs | 0.25 | 11027.75 KB | 0.19 |
| ServerTimings | net8.0 | Parlot | 1,711.8 μs | 0.08 | 1864.49 KB | 0.03 |

The tables above were measured before the tokens moved to combinators. This pass compares the code before and after, on
net8.0 with the same job (`--job short`), which is noisier than the tables, so only the parsing and DirectQuery differences
stand out. Allocations only change for DirectQuery.

| Benchmark | Workload | Before | After | Change |
|---|---|---:|---:|---:|
| Parse | Corpus | 142.6 μs | 150.0 μs | +5% |
| Parse | LongInLists | 457.4 μs | 502.3 μs | +10% |
| Parse | ServerTimings | 1,703.4 μs | 1,810.9 μs | +6% |
| Format | Corpus | 323.7 μs | 325.3 μs | none visible |
| Format | LongInLists | 1,683.3 μs | 1,643.1 μs | none visible |
| Format | ServerTimings | 5,308.4 μs | 5,185.0 μs | none visible |
| DirectQuery analysis | | 223.3 μs, 412.01 KB | 246.9 μs, 423.04 KB | +11%, +11 KB |

The formatting walker skips the name-cleaning regexes when a name can't match them (no `(`, `-`, `<`,
`RowNumber`), which accounts for part of the formatting gain over ANTLR. That is a separate commit
(`c43d063f`). The ANTLR visitor was left unchanged so it stays a faithful baseline.

## Windows

`DaxStudio.Tests` (net472/net8.0-windows) was **not run**, as this was developed on macOS. The changes outside
`DaxStudio.Parsers` are the 4 call sites listed above. `DaxStudio.Parsers.Tests` passes on net8.0 on macOS, except 5
tests that fail there on `develop` as well (`RdlCustomParameterWithScriptParameter`,
`RdlCustomParameterWithScriptParameter2`, `ParameterArrayAndQueryTest`, `ParameterArrayWithUnquotedValuesAndQueryTest`,
`ConnectPbixWithFullPathTest`). Its net472 target compiles but wasn't run. `xmSqlProcessing.cs` expects the same
formatter output, which Parlot produces. Run `build.cmd` and the test projects on Windows before merging.

## Possible next steps

1. Make the grammar-based parser the default: Parlot is faster than the regex path on every
   workload and reads the formatted text better than ANTLR.
2. Remove the two storage engine grammars and their visitors. The differential tests would then compare against
   recorded outputs instead. `Antlr4.Runtime.Standard` and the `Antlr4BuildTasks` build step (the ANTLR tool runs on
   Java at build time) stay as long as the DAX and preprocessor grammars of `develop` use them.
3. Add `DaxStudio.Parsers.ParlotTests` and `DaxStudio.Parsers.Benchmarks` to the solution and CI.
