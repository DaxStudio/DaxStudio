# Comment Script Variables — Implementation Plan

> Companion to `comment-script-variables-spec.md` (design). Decisions in spec §10 are RESOLVED.
> Scope: `--> SET <name> = <value>` plus `$(…)` expansion in command string arguments, with
> **eager (capture-time)** expansion of `$(…)` inside a `SET` value.

## 0. Confirmed decisions (from spec §10)

| # | Decision |
|---|----------|
| 1 | Undefined variable → **hard error** (fails run). No empty-string mode in v1. |
| 2 | Escape: **`$$(`** → literal `$(`. |
| 3 | Built-ins: **`now`, `utcnow`, `env`** only. |
| 4 | DAX query body **not** expanded in v1. |
| 5 | CLI folder run: variables **reset per file**. |
| 6 | `SET` value types: quoted string / identifier / int / real; only strings get `$(…)` expansion. |
| 7 | v1 expansion targets: `ASSERT TABLE <file>`, `METRICS EXPORT <path>`, `CONNECT` target, `USE <db>`. |
| 8 | Shared expander — **`DaxStudio.Parsers.CommentScript.ScriptVariableExpander`** (refinement of spec's "Core"; Parsers has no deps and holds the command classes, so it is the shared root both UI and CLI reference). |

Extra: `$(…)` written **directly** in a command arg (not via `SET`) is expanded at that command's
run time (so `now`/`utcnow` there reflect that moment). `$(…)` captured **in a `SET`** is frozen at
the `SET`.

## 1. Grammar (`src/DaxStudio.Parsers/PreProcessor/`)

Regen is automatic (Antlr4BuildTasks) — edit `.g4`, rebuild.

- **Lexer** (`PreProcessorLexer.g4`): none needed — `CS_SET: 'SET'` (line 197) already exists and is
  currently unused. `CS_EQUALS`, `CS_IDENTIFIER`, `CS_STRING_LITERAL`, `CS_INTEGER_LITERAL`,
  `CS_REAL_LITERAL` all exist.
- **Parser** (`PreProcessorParser.g4`):
  - Add rule (near the other command rules, ~line 116):
    ```
    set_variable: CS_SET CS_IDENTIFIER CS_EQUALS ( CS_STRING_LITERAL | CS_INTEGER_LITERAL | CS_REAL_LITERAL | CS_IDENTIFIER );
    ```
    (Deliberately does **not** reuse `parameter_scalar_values` verbatim, but the same 4 alternatives —
    keeps `SET` independent of PARAMETER changes.)
  - Add `set_variable` to the `command` alternatives (list at lines 90-103).
- **Ambiguity check:** `command` already dispatches on the leading keyword token; `CS_SET` is distinct
  from `CS_SET_PARAMETER` (`'PARAMETER'`), so no conflict with `script_parameter`.

## 2. Command class

- **New** `src/DaxStudio.Parsers/CommentScript/VariableCommand.cs`:
  ```csharp
  public class VariableCommand : ScriptCommand
  {
      public VariableCommand(string name, string rawValue) { Name = name; RawValue = rawValue; }
      public string Name { get; }
      // The value exactly as written (still contains any $(...) refs). Expanded eagerly at run time.
      public string RawValue { get; }
  }
  ```
  `RawValue` is the **un-expanded** text; expansion (with the live variable table) happens during
  execution so ordering/`now` semantics hold and the parser stays side-effect-free.

## 3. Listener (`PreProcessorListener.cs`)

- Add `ExitSet_variable([NotNull] Set_variableContext context)`:
  - `context.children[1]` = name (`CS_IDENTIFIER`), `context.children[3]` = value node.
  - For a quoted value use the un-quoted terminal text (same handling as existing string args); for
    int/real/identifier use `GetText()`.
  - `var cmd = new VariableCommand(name, rawValue); _currentBatch.Commands.Add(cmd);`
  - `OutputCommand(_currentBatch.Output, context);` then `base.ExitSet_variable(context);`
  - Follow the `ExitScript_parameter` pattern (lines 159-193) for shape.

## 4. Expander (`src/DaxStudio.Parsers/CommentScript/ScriptVariableExpander.cs`)

Pure string component, no external deps. Injectable clock for tests.

```csharp
public sealed class ScriptVariableExpander
{
    private readonly Dictionary<string,string> _vars =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTime> _nowLocal;   // default () => DateTime.Now
    private readonly Func<DateTime> _nowUtc;      // default () => DateTime.UtcNow
    private const int MaxDepth = 16;

    public ScriptVariableExpander(Func<DateTime> nowLocal = null, Func<DateTime> nowUtc = null) { ... }

    // Eagerly expands rawValue and stores the resolved literal (capture-time semantics).
    public void SetVariable(string name, string rawValue)
        => _vars[name] = Expand(rawValue);

    // Replaces $(...) refs; $$( -> literal $(; undefined/unknown-namespace/cycle -> throws
    // CommentScriptCommandException.
    public string Expand(string input) { ... }

    public void Reset() => _vars.Clear();   // used by CLI per-file
}
```

Implementation notes:
- Scan for `$$(` first → placeholder, restore to literal `$(` at the end (so an escaped token is not
  re-scanned).
- Match refs with `\$\((?<ref>[^)]*)\)`; resolve each ref via `ResolveRef`, recursively `Expand` the
  result, guarded by a depth counter (throw on `> MaxDepth`, naming the ref → cycle detection).
- `ResolveRef(ref)`:
  - If `ref` contains `:` and prefix ∈ {`now`,`utcnow`,`env`} → built-in:
    - `now:<fmt>` → `_nowLocal().ToString(<fmt>, InvariantCulture)` (wrap `FormatException` → hard error).
    - `utcnow:<fmt>` → same with `_nowUtc()`.
    - `env:<VAR>` → `Environment.GetEnvironmentVariable(VAR)`; **null → hard error** (undefined).
  - Else if `ref` contains `:` (unknown prefix) → hard error "unknown built-in namespace".
  - Else user var: `_vars.TryGetValue(ref, …)`; miss → hard error "undefined variable '<ref>'".
- All errors → `throw new CommentScriptCommandException(message)` (there is an existing overload; if a
  line/col-less ctor is missing, add one) so they flow through the existing error channel.

## 5. Execution wiring (two call sites, one shared expander)

At the start of a file's run, create one `ScriptVariableExpander`. Iterate commands **in order**;
`VariableCommand` → `expander.SetVariable(cmd.Name, cmd.RawValue)`. For each expansion target, pass the
arg through `expander.Expand(...)` **before** the file/connection is used.

Because command properties are get-only and some are computed (`ConnectCommand.FilePath` derives from
`ConnectionName`), expand **at the point of use** rather than mutating commands:

| Target | Site |
|--------|------|
| `AssertTableCommand.FilePath` | before `AssertTableFileLoader` reads it |
| `MetricsCommand.FileName` | before the VPAX export path is used |
| `ConnectCommand.ConnectionName` | before connecting (feed expanded value into the connect path) |
| `UseCommand.DatabaseName` | before selecting the database |

- **UI — `DaxStudio.UI/ViewModels/DocumentViewModel.cs`:** the command-processing region (~2618 flatten;
  connect/use ~2620-2641; per-batch asserts ~2860-2925). Instantiate the expander where the flattened
  `commands`/batches are first processed; apply `SetVariable` as `VariableCommand`s are encountered in
  order; wrap the four targets above.
- **CLI — `DaxStudio.CommandLine/Commands/FileCommand.cs`:** the per-file run (~140-199). Create a fresh
  expander **per file** (decision #5), seed nothing across files, apply `SetVariable`, wrap the targets.
- Keep a single ordering pass so a `SET` is visible only to commands after it (matches spec §3).

> If per-site wrapping proves too scattered, an alternative is a single
> `expander.ApplyTo(IEnumerable<ScriptCommand>)` pre-pass that returns expanded copies — but that needs
> the command props made settable (or `With…` clones). Point-of-use wrapping avoids touching the
> immutable command classes and is preferred for v1.

## 6. Docs

- `docs/CommentScriptSpecs.md`: add a "Script variables (`SET` / `$(…)`)" section — syntax, built-ins,
  capture-time semantics (with the `SET OutDir = "C:\Report\$(now:yyyy-MM-dd)"` example), escaping,
  undefined-var error, CLI per-file reset.
- Remove the "Proposed / not implemented" banner from the spec once shipped (or link it as the design
  record).

## 7. Tests

Follow the multitarget/vstest workaround (net472 output is locked): build test projects on
`net8.0` (`DaxStudio.Parsers.Tests`) / `net8.0-windows` (`DaxStudio.Tests`); DLLs land in
`src\bin\Debug\net8.0[-windows]\`.

- **Parser** (`tests/DaxStudio.Parsers.Tests/CommentScript/NewCommandTests.cs`):
  - `SET` with string / identifier / int / real → `VariableCommand` with expected `Name`/`RawValue`.
  - Redefinition (last write wins) — verify order preserved in `Commands`.
  - `SET` in an earlier batch present before a later-batch command (batch flattening order).
- **Expander** (new `tests/DaxStudio.Parsers.Tests/CommentScript/ScriptVariableExpanderTests.cs`):
  - `$(var)`, nested `$(a)`→`$(b)`, concatenation `$(dir)\$(name)-$(env).csv`.
  - `$(now:fmt)` / `$(utcnow:fmt)` with an **injected fixed clock**; assert exact formatted output.
  - **Capture-time:** `SetVariable("Stamp","$(utcnow:HHmmss)")`, advance the injected clock, `Expand("$(Stamp)")`
    twice → identical, and different from a fresh direct `$(utcnow:HHmmss)` after the advance.
  - `SetVariable("OutDir","C:\\Report\\$(now:yyyy-MM-dd)")` → stored literal has the date baked in.
  - `$(env:VAR)` (set a temp env var); undefined var → throws; unknown namespace `$(foo:bar)` → throws;
    bad format `$(now:zzzz-nonsense)` → throws; `$$(` → literal `$(`; cycle `a→b→a` → throws (depth).
- **Integration (optional):** an `ASSERT TABLE CSV "$(dir)\f.csv"` / `METRICS EXPORT "$(…)"` resolves to
  the expected absolute path before file access (can be asserted at the expander boundary without real IO).

## 8. Build order / checklist

1. Add `set_variable` rule + wire into `command` (`.g4`), rebuild to regen parser/listener bases.
2. `VariableCommand.cs`.
3. `ExitSet_variable` in `PreProcessorListener.cs`.
4. `ScriptVariableExpander.cs` (+ ensure a message-only `CommentScriptCommandException` ctor exists).
5. Wire expander into `DocumentViewModel` (UI) and `FileCommand` (CLI, per-file reset).
6. Parser + expander unit tests; run targeted suites (net8 / net8-windows).
7. Update `docs/CommentScriptSpecs.md`.
8. Full targeted regression on the comment-script test files.

## 9. Risks / notes

- **Immutable commands:** point-of-use expansion keeps command classes untouched; don't add setters
  unless the pre-pass alternative is chosen.
- **Two execution paths can drift:** both must call the same `ScriptVariableExpander`; a shared unit
  test on the expander guards behavior even if wiring differs slightly.
- **`GO` batches:** because expansion is at run time in command order across the flattened batch list,
  a `SET` before a `--> GO` is visible after it — verify in an integration/parse-order test.
- **Grammar regen:** `.g4` edits require a clean rebuild of `DaxStudio.Parsers`; the net472 lock only
  affects the *test/exe* output, not the library build used for regen.
