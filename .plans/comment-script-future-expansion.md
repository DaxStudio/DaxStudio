# Comment Script: Future Expansion & Lessons from Similar DSLs

## Executive Summary

The Comment Script spec already covers a solid set of connection, output, tracing, assertion, and batch-separation commands. However, comparing it against three mature analogous DSLs — **sqlcmd directives** (Microsoft's SQL scripting preprocessor), **dbt** (data build tool with Jinja macros and tests), and **pgTAP / tSQLt** (SQL unit testing frameworks) — reveals several categories of missing or under-developed capability: **variables & templating**, **control flow**, **error handling**, **file inclusion**, **richer assertion patterns**, and **CI/CD integration**. These are not all equally important, but they represent the proven expansion vectors from DSLs that serve a similar audience.

## Analysis of the Current Spec

### What's Implemented & Spec'd

| Category | Commands | Status |
|----------|----------|--------|
| Connection | `CONNECT`, `USE` | Implemented (a `CONNECT PBIX "<full path>"` supersedes the old `OPEN`) |
| Parameters | `PARAMETER @name TYPE = value` | Implemented (scalars + arrays) |
| Batch control | `GO` | Implemented |
| Output | `OUTPUT`, `OUTPUT-FOLDER`, `OUTPUT-FILE`, `EXCEL-SHEET` | Spec'd, partially implemented |
| Tracing | `TRACE SERVERTIMINGS/QUERYPLAN/ALLQUERIES ON/OFF` | Implemented |
| Cache | `CLEAR CACHE` | Implemented |
| Metrics | `METRICS EXPORT/VIEW` | Implemented |
| Assertions | `ASSERT DURATION/ROWCOUNT/VALUE/COLUMN/RESULTS/TABLE` | DURATION/ROWCOUNT/TABLE implemented; VALUE/COLUMN/RESULTS spec'd |
| Testing | `TEST PERFORMANCE` | Implemented |
| Save | `SAVEAS` | Implemented |

### What's Mentioned but Not Fully Developed

- **LOOP** — Referenced in the spec ("which is already used by the LOOP command for multi-line constructs")[^1] but no grammar or spec exists.
- **OPEN** — **Removed — superseded by `CONNECT PBIX "<full path to .pbix>"`**, which opens the file in
  Power BI Desktop (if not already running) and connects.
- **SAVEAS** — **Implemented.** `--> SAVEAS "<path>"` saves a post-query snapshot: `.daxx` writes a full
  package (query + trace watchers + `--> SHOW` output, with Server Timings embedded when active), any
  other extension writes the query text. Timestamp / dynamic paths use `$(now:...)` / `$(...)` variable
  expansion (the earlier `%yyyy-MM-dd%` idea is superseded). Works in both the UI and the `dscmd` CLI.
- **ASSERT VALUE / COLUMN / RESULTS** — **Removed from the spec, superseded by `ASSERT TABLE`** (which
  covers single-value, single-column, and full result-set comparison, inline or from a `CSV/TXT/MD/PARQUET`
  baseline file).

---

## Gap Analysis: Lessons from Comparable DSLs

### 1. Variables & Templating (from sqlcmd & dbt)

**sqlcmd** has a full variable system with `:setvar`, `$(variable)` expansion, environment variable support, and scoping rules[^2]. **dbt** goes further with Jinja templating — `{% set %}` variables, `{{ ref() }}` model references, and macro definitions[^3].

Comment Script currently has `PARAMETER` for query parameters, but lacks:

| Feature | sqlcmd | dbt | Comment Script |
|---------|--------|-----|----------------|
| Script variables | `:setvar name value` | `{% set x = ... %}` | ❌ Missing |
| Variable expansion in commands | `$(varname)` | `{{ var }}` | ❌ Missing |
| Environment variables | `$(SQLCMDSERVER)` | `{{ env_var('KEY') }}` | ❌ Missing |
| Variable in filenames | `:out $(file)` | Jinja in paths | ❌ Partial (SAVEAS mentions `%tokens%`) |

**Recommendation**: Add a `SET` command and `$(variable)` expansion:
```
--> SET @OutputDir = "C:\Reports"
--> SET @Today = %yyyy-MM-dd%
--> OUTPUT-FOLDER $(@OutputDir)
--> SAVEAS "report-$(@Today).dax"
```
This would be distinct from `PARAMETER` (which sets DAX query parameters). The `SET` token already exists in the lexer (`CS_SET`)[^4] but is unused.

### 2. Control Flow (from dbt & sqlcmd)

**dbt** offers `{% if %}` / `{% for %}` via Jinja[^3]. **sqlcmd** has conditional execution with `:on error` and `:exit`[^2]. The LOOP command referenced in the spec suggests iteration was already being considered[^1].

| Feature | sqlcmd | dbt | Comment Script |
|---------|--------|-----|----------------|
| Conditional execution | `:on error [exit/ignore]` | `{% if ... %}` | ❌ Missing |
| Loops | N/A | `{% for ... %}` | ❌ (LOOP mentioned but undefined) |
| Early exit | `:exit` | N/A | ❌ Missing |

**Recommendation**: Start with conditional execution rather than loops — it's the most useful for CI/CD scenarios:
```
--> ON ERROR EXIT          // stop executing batches on first error
--> ON ERROR CONTINUE      // log error but keep going (default)
--> IF CONNECTED            // only run this batch if connected
```

### 3. Error Handling & Severity (from dbt & sqlcmd)

**dbt** has a sophisticated severity system: tests can be `error` or `warn`, with conditional thresholds (`error_if: ">1000"`, `warn_if: ">10"`)[^5]. **sqlcmd** has `:on error [exit|ignore]` for controlling execution flow on errors[^2].

Comment Script has no error handling at all. If an ASSERT fails, there's no way to control whether execution stops or continues.

| Feature | dbt | sqlcmd | Comment Script |
|---------|-----|--------|----------------|
| Test severity (warn/error) | `severity: warn` | N/A | ❌ Missing |
| Conditional thresholds | `error_if: ">100"` | N/A | ❌ Missing |
| On error behavior | `--warn-error` | `:on error exit` | ❌ Missing |

**Recommendation**: Add severity to assertions and an error handling directive:
```
--> ASSERT ROWCOUNT > 0 SEVERITY WARN     // warning, not failure
--> ASSERT DURATION < 5000 SEVERITY ERROR  // hard failure (default)
--> ON ERROR EXIT                          // stop on first error
--> ON ERROR CONTINUE                      // continue past errors
```

### 4. File Inclusion & Script Composition (from sqlcmd)

**sqlcmd** has `:r filename` to include/execute another script file[^2]. This is powerful for building reusable script libraries.

| Feature | sqlcmd | dbt | Comment Script |
|---------|--------|-----|----------------|
| Include file | `:r script.sql` | `{{ ref('model') }}` | ❌ Missing |
| Output redirect | `:out filename` | N/A | Partial (OUTPUT-FILE) |

**Recommendation**: Add an `INCLUDE` command for composing scripts:
```
--> INCLUDE "setup.dax"           // include and execute another .dax file
--> INCLUDE "common-asserts.dax"  // reusable assertion libraries
```
This would be especially useful for CI/CD scenarios where a common setup (CONNECT, USE, CLEAR CACHE) could be shared across many test files.

### 5. Richer Assertion Patterns (from pgTAP & dbt)

**pgTAP** has an exceptionally rich assertion library organized around result-set comparison[^6]:

| pgTAP Function | Purpose | Comment Script Equivalent |
|----------------|---------|---------------------------|
| `results_eq()` | Exact row-by-row match | `ASSERT TABLE` ✅ |
| `set_eq()` | Same rows, any order | `ASSERT TABLE UNORDERED` ✅ |
| `set_has()` | Result contains these rows | `ASSERT TABLE PARTIAL` ✅ |
| `set_hasnt()` | Result does NOT contain rows | ❌ Missing |
| `bag_eq()` | Same rows with duplicates | ❌ Missing (would need ASSERT TABLE with dup awareness) |
| `is_empty()` | Result has zero rows | ❌ Missing (ASSERT ROWCOUNT = 0 works but verbose) |
| `isnt_empty()` | Result has at least one row | ❌ Missing (ASSERT ROWCOUNT > 0 works) |
| `row_eq()` | Single row matches | ✅ `ASSERT TABLE` with a single expected row |
| `performs_ok()` | Query completes within N ms | ASSERT DURATION ✅ |
| `throws_ok()` | Query produces an error | ❌ Missing |

**dbt** takes a different approach — tests are SQL queries that return failing rows. The built-in generic tests are `unique`, `not_null`, `accepted_values`, and `relationships`[^7].

**Recommendations**:

a) **ASSERT EMPTY / ASSERT NOT EMPTY** — Shorthand for common rowcount checks:
```
--> ASSERT EMPTY              // query should return 0 rows
--> ASSERT NOT EMPTY          // query should return >= 1 row
```

b) **ASSERT TABLE EXCLUDES** — Inverse of PARTIAL (pgTAP's `set_hasnt`):
```
--> ASSERT TABLE EXCLUDES
-->> | Status    |
-->> | Cancelled |
```

c) **ASSERT ERROR** — Expect the query to fail (pgTAP's `throws_ok`):
```
--> ASSERT ERROR                          // any error is OK
--> ASSERT ERROR "Cannot find table"      // error message contains text
```

d) **ASSERT SCHEMA** — Verify column structure without caring about data:
```
--> ASSERT SCHEMA
-->> | ColumnName | DataType |
-->> | Color      | STRING   |
-->> | Count      | INT64    |
```

### 6. Named Tests & Test Organization (from dbt & tSQLt)

**dbt** unit tests have names, descriptions, and tags[^7]. **tSQLt** organizes tests into test classes[^8]. Comment Script currently has `TEST PERFORMANCE "name"` but it's narrowly scoped.

**Recommendation**: Generalize the naming / description capability:
```
--> TEST "Verify product count by color"
--> ASSERT ROWCOUNT = 5
EVALUATE COUNTROWS('Product')

--> GO

--> TEST "Check for orphaned orders" TAG ci-nightly
--> ASSERT EMPTY
EVALUATE FILTER('Orders', ISBLANK(RELATED('Customer'[ID])))
```

The `TAG` keyword would enable selective test execution (e.g., run only tests tagged `ci-nightly`), similar to dbt's `--select "tag:nightly"`.

### 7. Output & Reporting (from dbt & CI/CD practices)

**dbt** generates test result artifacts (JSON manifests, run results) that integrate with CI/CD pipelines. Comment Script's `SAVEAS` hints at CI/CD use but lacks structured reporting.

**Recommendations**:

a) **REPORT** command for structured test output:
```
--> REPORT JUNIT "test-results.xml"   // JUnit XML for CI integration
--> REPORT JSON "test-results.json"   // JSON for custom tooling
--> REPORT MARKDOWN "results.md"      // Human-readable summary
```

b) **TIMING** for benchmarking:
```
--> TIMING ON               // log execution time for each batch
--> TIMING BASELINE "baseline.json"  // compare against a saved baseline
```

### 8. DAX-Specific Enhancements

Some features are unique to the DAX / Power BI ecosystem and don't have direct DSL parallels:

a) **REFRESH** — Trigger a table/partition refresh:
```
--> REFRESH 'Sales'                    // refresh a specific table
--> REFRESH 'Sales' PARTITION "2024"   // refresh a specific partition
```

b) **IMPERSONATE** — Run as a different user (for RLS testing):
```
--> IMPERSONATE "user@domain.com"
--> ASSERT ROWCOUNT < 100        // verify RLS limits results
EVALUATE 'Sales'
```

c) **MEASURE** — Define an inline measure for testing without modifying the model:
```
--> MEASURE 'Sales'[TestCalc] = SUMX('Sales', [Quantity] * [Price])
EVALUATE { [TestCalc] }
```

d) **FORMAT** — Control output formatting:
```
--> FORMAT DECIMALS 2          // round numeric output to 2 decimal places
--> FORMAT DATETIME "yyyy-MM-dd"  // standardize date output format
```

### 9. Documentation & Metadata (from dbt)

**dbt** encourages describing every model, column, and test[^7]. This metadata powers a documentation site and data lineage graphs.

**Recommendation**: Add a `DESCRIPTION` or `NOTE` command for in-script documentation:
```
--> NOTE "This test validates the fiscal year calculation"
--> NOTE "Expected to fail until FY2025 data is loaded"
```
Notes would be captured in test reports and would be useful for CI/CD visibility.

---

## Priority Ranking

Based on impact, implementation complexity, and alignment with CI/CD use cases:

| Priority | Feature | Rationale |
|----------|---------|-----------|
| **P1** | Variables & `SET` command | Enables SAVEAS tokens, reusable scripts, DRY principles |
| **P1** | Error handling (`ON ERROR`) | Essential for CI/CD — must control what happens on failure |
| ~~P1~~ | ~~`ASSERT VALUE` / `ASSERT COLUMN` / `ASSERT RESULTS`~~ | **Dropped — superseded by `ASSERT TABLE`** (single-value, single-column, and result-set comparison, inline or from a baseline file) |
| **P2** | File inclusion (`INCLUDE`) | Key for script reusability and test suites |
| **P2** | Test naming & tags | Enables selective test execution in CI/CD |
| **P2** | `ASSERT EMPTY` / `ASSERT NOT EMPTY` | Simple, high-value assertion shorthand |
| **P2** | Severity levels (WARN/ERROR) | Matches dbt's proven model for test flexibility |
| **P3** | Structured reporting (JUNIT/JSON) | CI/CD integration — needed for production use |
| **P3** | `ASSERT ERROR` (expect failure) | Niche but useful for negative testing |
| **P3** | `ASSERT TABLE EXCLUDES` | Completes the set-comparison matrix |
| **P3** | IMPERSONATE | Unique to Power BI RLS testing |
| **P4** | REFRESH / MEASURE / FORMAT | Nice-to-have DAX-specific features |
| **P4** | Full LOOP / IF control flow | Complex, low priority until basics are solid |

---

## Comparable DSL Summary

| DSL | Domain | Key Pattern | What to Learn |
|-----|--------|-------------|---------------|
| **sqlcmd** | SQL Server scripting | `:command` directives in SQL scripts | Variables (`:setvar`), file include (`:r`), error control (`:on error`), output redirect (`:out`) |
| **dbt** | Data transformation & testing | YAML-defined tests + Jinja templating | Generic test patterns, severity levels, test naming/tagging, CI/CD artifacts, snapshot testing |
| **pgTAP** | PostgreSQL unit testing | TAP-emitting SQL assertions | Rich result-set comparison (`set_eq`, `set_has`, `bag_eq`), schema assertions, performance tests |
| **tSQLt** | SQL Server unit testing | Test classes + mock tables | Test organization, FakeTable for isolation, ApplyConstraint |
| **Verify** (.NET) | Snapshot testing | Serialize → compare against saved `.verified.` files | Baseline comparison model — relevant to `ASSERT TABLE` with a `CSV/TXT/MD/PARQUET` baseline file |

---

## Confidence Assessment

- **High confidence**: The gap analysis against sqlcmd, dbt, and pgTAP is based on current official documentation and matches well-established patterns in the data tooling ecosystem.
- **High confidence**: The existing parser/lexer state was verified by reading the actual grammar files[^4][^9].
- **Medium confidence**: Priority rankings are subjective and based on my assessment of typical CI/CD needs for BI query testing. The actual priorities depend on DAX Studio's user base and roadmap.
- **Low confidence**: The LOOP and IMPERSONATE features are speculative — they may not align with the Analysis Services permission model or DAX Studio's architecture.

---

## Footnotes

[^1]: `DaxParser/docs/CommentScriptSpecs.md:125` — "which is already used by the LOOP command for multi-line constructs"
[^2]: Microsoft sqlcmd documentation — `:setvar`, `:r`, `:on error`, variable expansion with `$(varname)`: https://learn.microsoft.com/en-us/sql/ssms/scripting/sqlcmd-use-with-scripting-variables
[^3]: dbt Jinja macros documentation — `{% set %}`, `{{ ref() }}`, macro definitions: https://docs.getdbt.com/docs/build/jinja-macros
[^4]: `DaxParser/PreProcessorLexer.g4:197` — `CS_SET: 'SET';` token is defined but unused in the parser
[^5]: dbt severity configuration — `severity: warn/error`, `error_if`, `warn_if`: https://docs.getdbt.com/reference/resource-configs/severity
[^6]: pgTAP documentation — result set comparison functions (`results_eq`, `set_eq`, `set_has`, `bag_eq`, etc.): https://pgtap.org/documentation.html
[^7]: dbt data tests & unit tests — `unique`, `not_null`, `accepted_values`, `relationships`, naming, tags: https://docs.getdbt.com/docs/build/tests and https://docs.getdbt.com/docs/build/unit-tests
[^8]: tSQLt user guide — test classes, FakeTable, ApplyConstraint: https://tsqlt.org/user-guide/
[^9]: `DaxParser/PreProcessorParser.g4:78-89` — current `command` rule with all implemented alternatives
