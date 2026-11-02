# Comment Script: Script Variables (`SET` + `$(…)` expansion) — Draft Spec

> Status: **Implemented.** See `docs/CommentScriptSpecs.md` ("Variables") for user documentation and
> `comment-script-variables-implementation-plan.md` for the build breakdown. The expander lives at
> `DaxStudio.Parsers.CommentScript.ScriptVariableExpander`; expansion is applied via
> `ScriptVariableExpander.ExpandBatches` from both the UI (`DocumentViewModel`) and CLI
> (`FileCommand`, fresh per file).

## 1. Goal & motivation

Let a comment script define reusable named values and substitute them into command arguments — most
importantly **dynamic file paths** for CI/CD (`OUTPUT-FILE`, `METRICS EXPORT`, `ASSERT TABLE <file>`,
a future `SAVEAS`). Secondary uses: a shared database/connection value, an environment discriminator
(`prod`/`test`), and timestamped output names.

```
--> SET OutputDir = "C:\Reports"
--> SET Env = prod
--> METRICS EXPORT "$(OutputDir)\metrics-$(Env)-$(now:yyyy-MM-dd).vpax"
```

## 2. Syntax

### 2.1 Defining a variable — `SET`
```
--> SET <name> = <value>
```
- `<name>` — a bare identifier (`CS_IDENTIFIER`): letters, digits, underscore; case-insensitive.
  Deliberately **no leading `@`** — `@name` already denotes a DAX query parameter
  (`--> PARAMETER @name TYPE = value`) and reusing it would conflate the two concepts.
- `<value>` — mirrors the existing `parameter_scalar_values` rule so the lexer work is minimal:
  a quoted string (`CS_STRING_LITERAL`), a bare identifier (`CS_IDENTIFIER`), an integer, or a real.
  A quoted value may itself contain `$(…)` references — other variables **and built-ins** — which are
  expanded **eagerly, at the moment the `SET` executes**, and the resolved literal is stored (see §4.3).
  This makes a captured timestamp stable for the rest of the run:
  ```
  --> SET OutDir = "C:\Report\$(now:yyyy-MM-dd)"
  --> SET Stamp  = "$(utcnow:yyyyMMdd-HHmmss)"
  ```
  `$(OutDir)` then yields the same dated folder every time it is used, even in a later `--> GO` batch
  minutes afterwards.
- Redefining an existing name overwrites it (last write wins), re-evaluating any `$(…)` in the new value.

### 2.2 Using a variable — `$(name)`
`$(name)` in a command argument is replaced with the variable's current value. The parentheses give
explicit boundaries so names concatenate safely inside paths/filenames:
```
--> OUTPUT-FILE "$(OutputDir)\$(ReportName)-$(Env).csv"
```
Chosen over `@name` (collision, see above), bare `$name` (ambiguous end-of-name in
`$(year)$(month)`), and `%name%` (collides visually with Windows env-var syntax and the SAVEAS
`%date%` idea). `$(…)` matches the **sqlcmd** convention familiar to this SQL/BI audience and never
appears in a valid Windows path.

### 2.3 Built-in / namespaced variables
The same `$(…)` syntax supports built-in namespaces via a `namespace:argument` form:

| Reference | Expands to |
|-----------|-----------|
| `$(now:<fmt>)` | Local current time formatted with .NET format string `<fmt>` (e.g. `$(now:yyyy-MM-dd)`, `$(now:yyyy-MM-dd_HH-mm-ss)`) |
| `$(utcnow:<fmt>)` | UTC current time, same formatting |
| `$(env:<VAR>)` | The `<VAR>` environment variable (e.g. `$(env:USERPROFILE)`, `$(env:BUILD_ID)`) |

Resolution order for `$(x)`: if `x` contains a `:` and the prefix is a known namespace, resolve as a
built-in; otherwise treat `x` as a user variable name. A user `SET` name may not contain `:`.

This unifies the earlier ad-hoc `%yyyy-MM-dd%` SAVEAS idea into one scheme.

## 3. Semantics

- **Scope:** script-global. A single ordered dictionary of variables spans all batches in the run;
  there is no block scoping.
- **Ordering:** `SET` takes effect from the point it executes onward. Because commands (and batches
  separated by `--> GO`) execute top-to-bottom, the `$(…)` references **inside a `SET` value** are
  resolved when the `SET` runs and the literal result is stored; a later `$(name)` in a command
  argument then substitutes that already-resolved literal. A variable used before it is `SET`
  is an error (§5). (Built-ins like `$(now:…)` written directly in a command argument — not via a
  `SET` — are still evaluated at that command's run time.)
- **Case-insensitivity:** names compare case-insensitively (`$(OutputDir)` == `$(outputdir)`).

## 4. Where expansion applies

### 4.1 Applies to comment-script command **string arguments**
Expansion runs over the string-valued arguments of comment-script commands, primarily the
file/path-bearing ones:
- `ASSERT TABLE (CSV|TXT|MD|PARQUET) "<path>"` → `AssertTableCommand.FilePath`
- `METRICS EXPORT "<path>"` → `MetricsCommand` export path
- (future) `OUTPUT-FILE`, `OUTPUT-FOLDER`, `EXCEL-SHEET`, `SAVEAS`
- `CONNECT … "<target>"` and `USE "<db>"` targets (so a connection/db can be parameterised)

### 4.2 Does **not** (initially) apply to the DAX query body
The DAX text is left untouched in v1 to avoid surprises. (A later opt-in — e.g. `SET EXPAND QUERY ON`
— could enable query-body expansion; DAX has no native `$(…)`, so it is technically safe but is kept
out of scope here.)

### 4.3 Nested references
A variable value may reference other variables and built-ins. These are expanded **eagerly** when the
`SET` executes (capture-time), recursively, with a small depth cap (e.g. 16) to catch cycles. The
stored value is the fully-resolved literal, so a captured `$(now:…)`/`$(utcnow:…)` is frozen at the
`SET` and stays constant for the rest of the run:
```
--> SET Root  = "C:\ci"
--> SET Stamp = "$(utcnow:yyyyMMdd-HHmmss)"
--> SET Out   = "$(Root)\out-$(Stamp)"   // Out is a fixed literal from here on
```
Because expansion is eager, a variable can only reference names already `SET` above it (or built-ins);
forward references resolve to nothing and raise the undefined-variable error (§5).

## 5. Error handling

- **Undefined variable** (`$(missing)` with no matching `SET`, built-in, or env var): **hard error**
  that fails the run — the safe default for CI/CD (a silently-empty path is worse than a stop).
  Surfaced through the existing comment-script error channel
  (`CommentScriptCommandException` → `PreProcessResult.CommandErrors`).
- **Unknown built-in namespace** (`$(foo:bar)` where `foo` is not `now`/`utcnow`/`env`): hard error.
- **Bad format string** for `now`/`utcnow`: hard error with the offending token.
- **Cycle / depth exceeded** in nested expansion: hard error naming the variable.

## 6. Escaping

- `$$(` emits a literal `$(` (i.e. `$$` collapses to a single `$` only when immediately followed by
  `(`); every other `$` is literal. This keeps the common case (`$` in text/paths) untouched while
  giving an escape hatch for the rare literal `$(`.

## 7. Grammar / code changes

Small, because `$(…)` lives **inside already-extracted argument strings**, so it is a runtime
string-substitution step — not new lexer/parser tokens.

1. **Lexer** (`PreProcessorLexer.g4`): `CS_SET` already exists. No new tokens needed for `$(…)`.
2. **Parser** (`PreProcessorParser.g4`):
   - Add `set_variable: CS_SET CS_IDENTIFIER CS_EQUALS ( CS_STRING_LITERAL | parameter_scalar_values );`
   - Add `set_variable` to the `command` alternatives (line ~90-103).
3. **Listener** (`PreProcessorListener.cs`): add `ExitSet_variable` that creates a `VariableCommand`
   (new `ScriptCommand` subclass: `Name`, `RawValue`).
4. **New command class** `CommentScript/VariableCommand.cs`.
5. **New expander** (Core), e.g. `CommentScript/ScriptVariableExpander` in `DaxStudio.Core`:
   - Holds the ordered `Dictionary<string,string>` (case-insensitive).
   - `SetVariable(name, rawValue)` and `string Expand(string input)` (regex
     `\$\((?<ref>[^)]*)\)` with `$$(` un-escaping and recursive/depth-capped resolution).
6. **Execution wiring:** at run time, iterate commands in order; when a `VariableCommand` is hit, call
   `SetVariable` (expanding its own value first); for every path-bearing command, pass its argument
   through `Expand(...)` *before* the file/connection is used. Two call sites:
   - UI: `DocumentViewModel` command processing (same loops that already read `AssertTableCommand`,
     `MetricsCommand`, etc.).
   - CLI: `DaxStudio.CommandLine/Commands/FileCommand.cs` (`batch.Commands.OfType<…>()` loops).
   To avoid divergence, both call the shared `ScriptVariableExpander`.

## 8. Examples

```
// Dynamic, timestamped CI/CD output
--> CONNECT PBIX "C:\reports\Sales.pbix"
--> SET OutDir = "$(env:BUILD_ARTIFACTSTAGINGDIRECTORY)\dax"
--> SET Stamp  = "$(utcnow:yyyyMMdd-HHmmss)"

--> TRACE SERVERTIMINGS ON
--> METRICS EXPORT "$(OutDir)\model-$(Stamp).vpax"
EVALUATE ROW("ok", 1)

--> GO

--> ASSERT TABLE CSV "$(OutDir)\baselines\products.csv"
EVALUATE 'Product'
```

## 9. Testing

- Parser (`NewCommandTests`): `SET` with string / identifier / int / real values; redefinition;
  `SET` in an earlier batch visible in a later batch.
- Expander unit tests (Core): `$(var)`, nested `$(a)`→`$(b)`, `$(now:fmt)`/`$(utcnow:fmt)`,
  `$(env:VAR)`, undefined → error, unknown namespace → error, `$$(` escape, cycle/depth → error,
  concatenation in a path (`$(dir)\$(name)-$(env).csv`).
- **Capture-time built-ins:** `SET Stamp = "$(utcnow:...)"` stores a fixed literal; asserting the same
  value on two uses (and that it does not change when the clock is advanced via an injected time
  provider) proves eager capture. Same for `SET OutDir = "C:\Report\$(now:yyyy-MM-dd)"`.
- Integration: an `ASSERT TABLE CSV "$(dir)\f.csv"` / `METRICS EXPORT "$(…)"` resolves to the
  expected path before the file is read/written.

## 10. Open decisions (RESOLVED)

1. **Escape syntax** — **`$$(`** collapses to a literal `$(`; every other `$` is literal.
2. **Undefined variable** — **hard error** that fails the run (confirmed; no empty-string mode in v1).
3. **Built-in namespaces to ship** — **`now`, `utcnow`, `env`** in v1. (`guid`/`machine` deferred.)
4. **Query-body expansion** — **out of scope** for v1 (command string args only).
5. **CLI folder run** — variables **reset per file** (confirmed); no cross-file carry-over.
6. **`SET` value types** — quoted string / bare identifier / int / real (mirrors `PARAMETER`); only
   quoted strings undergo `$(…)` expansion.
7. **v1 expansion targets** — `ASSERT TABLE <file>`, `METRICS EXPORT <path>`, `CONNECT …` target,
   `USE <db>`; future path commands opt in as they are built.
8. **Placement** — shared `ScriptVariableExpander` in `DaxStudio.Core`, called from both the UI
   (`DocumentViewModel`) and CLI (`FileCommand`).

See `comment-script-variables-implementation-plan.md` for the build breakdown.
