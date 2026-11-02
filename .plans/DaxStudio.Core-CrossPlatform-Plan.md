# Plan: Cross-Platform `dscmd` — Phase 2 (net8.0, no `-windows`)

## Status of the original DaxStudio.Core extraction

The original 5-phase plan (`DaxStudio.Core-Plan.md`) is **complete**, and scope grew
well beyond it:

- `DaxStudio.Core` exists (~118 source files): Enums, Interfaces, Events, Model, Trace,
  Exports, ResultsTargets, Settings, Vpax, DeltaAnalyzer, Options, Connections.
- Parsers were extracted further into their **own** `DaxStudio.Parsers` project, which
  already targets plain **`net8.0`**.
- `DaxStudio.CommandLine` (`dscmd`) **no longer references `DaxStudio.UI`** — it references
  only ADOTabular, Common, **Core**, Interfaces.
- The whole `dscmd` dependency chain multi-targets `net472;net8.0-windows`, and
  `dscmd` **builds clean on `net8.0-windows`** (verified 2026-07-21).

**Deviations from the original plan (accepted):** Core uses the full `Caliburn.Micro`
package (not `Caliburn.Micro.Core`) and absorbed `Screen`-based ViewModels
(`OptionsModel`, `ServerTimingDetailsViewModel`) plus `IToolWindow` / `ITraceWatcher`,
which the original plan intended to keep in UI.

## Remaining goal

Everything targets **`net8.0-windows` with `UseWPF=true`**, so `dscmd` cannot run on
Linux/macOS. Phase 2 gets the `dscmd` dependency chain onto a genuine **`net8.0`**
(no `-windows`, no WPF) target, so the CLI can be published cross-platform.

Strategy: flip **one leaf project at a time**, bottom-up, keeping the build green after
each step. Keep the existing `net472` and `net8.0-windows` targets working for the
desktop app; add `net8.0` alongside them (multi-target) rather than replacing.

## Dependency chain & per-project blockers

```
SqlFormatter  Interfaces  Parsers(net8.0 ✔)
      \           |          /
        Common  QueryTrace
             \     |
            ADOTabular
                 |
               Core  ── Caliburn.Micro(WPF), Screen, Registry, WMI
                 |
            CommandLine (dscmd)
```

| Project | Blocker | Difficulty |
|---|---|---|
| DaxStudio.Parsers | none — already `net8.0` | ✅ done |
| DaxStudio.SqlFormatter | none found | trivial |
| DaxStudio.Interfaces | ~~stray `using System.Drawing;`~~ resolved — none | trivial |
| DaxStudio.QueryTrace | one `OleDbConnectionStringBuilder` (conn-string parse) | low |
| DaxStudio.Common | System.Drawing.Common already guarded to `-windows`; no blocker | trivial |
| ADOTabular | 4× `OleDbConnectionStringBuilder` in `ADOTabularConnection.cs` | medium |
| DaxStudio.Core | full `Caliburn.Micro` + `UseWPF`; `Screen`; Registry; WMI | medium-high |
| DaxStudio.CommandLine | add `net8.0` target; verify Spectre/Serilog/Adomd | low-medium |

## Ordered steps (small, independently buildable)

1. **`sqlformatter-net8`** — add `net8.0` to `TargetFrameworks`. Build.
2. **`interfaces-net8`** — remove the unused `using System.Drawing;` from
   `IGlobalOptions.cs`; add `net8.0`. Build.
3. **`querytrace-net8`** — replace `System.Data.OleDb.OleDbConnectionStringBuilder`
   in `QueryTraceEngine.cs:201` with `System.Data.Common.DbConnectionStringBuilder`;
   add `net8.0`. Build.
4. **`common-net8`** — confirm `System.Drawing.Common` is unused; remove the package
   reference (or guard it to `-windows`); add `net8.0`. Build.
5. **`adotabular-net8`** — introduce a small cross-platform connection-string helper
   to replace `OleDbConnectionStringBuilder`; add `net8.0`. Build.
6. **Core sub-steps** (parallelizable, each keeps `net8.0-windows` green):
   - **`core-caliburn-core`** — conditional package: `Caliburn.Micro.Core` for the
     `net8.0` target, full `Caliburn.Micro` for `net472`/`net8.0-windows`.
   - **`core-screen-refactor`** — remove `Screen` base from `OptionsModel` and
     `ServerTimingDetailsViewModel` (use `PropertyChangedBase` + minimal shim).
   - **`core-registry-guard`** — guard `RegistrySettingProvider` behind
     `OperatingSystem.IsWindows()` / `[SupportedOSPlatform("windows")]`.
   - **`core-wmi-guard`** — Windows-guard `ProcessExtensions.GetParent` (WMI);
     return `null` (or `/proc` fallback) off-Windows.
7. **`core-net8-target`** — add `net8.0`, drop `UseWPF` for that target. Build Core.
8. **`commandline-net8`** — add `net8.0`; verify Spectre.Console, Serilog,
   AdomdClient resolve for `net8.0`. Build `dscmd`.
9. **`xplat-smoke-test`** — run `dscmd` against an XMLA endpoint from a `net8.0`
   build (ideally Linux/RID) to validate an end-to-end query path.

## How the non-trivial dependencies are handled

### 1. WMI (`ProcessExtensions.GetParent`, `System.Management`) — DONE (2026-07-21)
Used only to walk up to a parent process during the **local Power BI Desktop scan**
(`PowerBIHelper`), a Windows-only scenario. Instead of a runtime `OperatingSystem.IsWindows()`
guard (which would still link `System.Management` into the cross-platform binary), the scan
was **inverted behind an abstraction and split by TFM** so the Windows dependencies are
compiled out of a future plain `net8.0` build entirely:
- New `IPowerBIInstanceScanner` (portable) + `NullPowerBIInstanceScanner` (portable, returns
  an empty list — correct off-Windows, where there is no Power BI Desktop).
- `WindowsPowerBIInstanceScanner` (Windows TFMs only) holds the relocated scan: WMI
  (`ProcessExtensions.GetParent`), `ManagedIpHelper` (iphlpapi TCP table), `WindowTitle`
  (user32) and the `WindowsPrincipal` admin check.
- `PowerBIHelper` keeps only the portable cache/throttle façade and delegates the raw scan to
  a settable `IPowerBIInstanceScanner Scanner` (also makes the previously-untestable scan
  testable). The default is chosen at compile time via `PowerBIScannerFactory.Windows.cs`
  vs `PowerBIScannerFactory.Stub.cs` (`<Compile Remove>` gated) — no `#if` in any source file.
- `PowerBIInstance`/`EmbeddedSSASIcon` moved to their own portable file; public API unchanged
  so UI + CLI callers are untouched.
- `Core.csproj`: `System.Management` PackageReference scoped to `net8.0-windows`; forward-looking
  `<Compile Remove>` excludes `ProcessExtensions.cs`, `ManagedIpHelper.cs`,
  `WindowsPowerBIInstanceScanner.cs`, `PowerBIScannerFactory.Windows.cs` from `net8.0` (and
  the Stub factory from the Windows TFMs).
- Added `PowerBIHelperTests` (4 cases: delegation/sort, throttle, cache, null-scanner-empty);
  pass on net472 + net8.0-windows. Whole dscmd chain builds 0 errors on both TFMs.
- **Note:** the `net8.0` TFM is *not* added to Core yet (still blocked by Caliburn.Micro/WPF,
  `Screen`, Registry); the gating is forward-looking so that flip becomes a one-line change.
  `DaxStudio.Common\WindowTitle.cs` (user32) will need analogous gating when Common adds net8.0.

### 2. `System.Drawing` / `System.Drawing.Common` — RESOLVED (2026-07-21)
No GDI+ / `System.Drawing.Common` blocker remains in the `dscmd` chain:
- `IGlobalOptions.cs` — stray `using System.Drawing;` **already removed**.
- Common `System.Drawing.Common` PackageReference is already
  `Condition="'$(TargetFramework)' == 'net8.0-windows'"`, so it never affects a plain
  `net8.0` build. It is effectively unused (only a false-positive `color: red` HTML
  string in `MailUtility.cs`) and could be dropped entirely, but this is optional.
- The **only** real `System.Drawing` use in the chain is
  `ResultsTargets\ResultsTargetExcelFile.cs` (`Color`, `Color.FromArgb`, `Color.White`
  passed to LargeXlsx). These live in **`System.Drawing.Primitives`**, which is part of
  the cross-platform shared framework — NOT the Windows-only `System.Drawing.Common`
  (GDI+: Bitmap/Graphics/Font/Brush/Pen/Icon, none of which are used). Safe cross-platform.
- Conclusion: nothing further needed for System.Drawing.

### 3. `System.Data.OleDb` (`OleDbConnectionStringBuilder`) — DONE (2026-07-21)
`System.Data.OleDb` is Windows-only on .NET 8 and was used across the `dscmd` chain
(not just ADOTabular/QueryTrace) purely to parse/edit AS connection strings:
`ADOTabularConnection.cs` (×4), `Core\Connections\ConnectionManager.cs` (×2),
`CommandLine\Helpers\AccessTokenHelper.cs` (×3, incl. `.DataSource`),
`CommandLine\Commands\CommandSettingsRawBase.cs` (×2), `QueryTraceEngine.cs` (×1),
plus an unused `using` in `CommandLine\UIStubs\QueryRunner.cs`.

Implemented a shared cross-platform helper
`ADOTabular\Utils\ConnectionStringBuilderExtensions.cs` (referenced by every chain
project via ADOTabular):
- `string.ToConnectionStringBuilder()` → a `System.Data.Common.DbConnectionStringBuilder`
  (null/empty-safe), replacing `new OleDbConnectionStringBuilder(...)`.
- `DbConnectionStringBuilder.GetDataSource()` → replaces the OleDb-only `.DataSource`
  property and the unsafe `builder["Data Source"]` indexer (which throws on a missing
  key, unlike the OleDb known-keyword behaviour).

**Verification:** empirically confirmed `DbConnectionStringBuilder` produces identical
value quoting/escaping to `OleDbConnectionStringBuilder` for AS connection strings
(incl. embedded `;`, `=`, and `"`); the only difference is parsed keys are lower-cased,
which is inconsequential (AS keys are case-insensitive; DAX Studio's own `Properties`
dict is `OrdinalIgnoreCase` and parsed from the original string). Added
`ConnectionStringBuilderExtensionsTests` (13 cases, round-trip + `HasRlsParameters` +
`IsPbiXmlaEndpoint`); pass on both `net472` and `net8.0-windows`. Whole `dscmd` chain
builds clean (0 errors) on both TFMs.

**Package fully removed:** the `System.Data.OleDb` PackageReference was deleted from
`ADOTabular` and the `PackageVersion` from `Directory.Packages.props`. `DaxStudio.UI`'s
3 OleDb usages (in `Model\ConnectionManager.cs` and `ViewModels\DocumentViewModel.cs`)
were migrated to the helper, so no project depends on the package. The entire graph
(dscmd chain + `DaxStudio.UI`) compiles for `net8.0-windows` with **zero** OleDb/CS0246
errors — verified via `dotnet build` of `DaxStudio.Standalone` (the only remaining error
is a pre-existing WPF `_wpftmp` `WorkingAnimation.InitializeComponent` markup-compile
quirk that reproduces on `net472` too and is unrelated to OleDb). The only remaining
OleDb references in `src` are the framework (not-package) `System.Data.OleDb` in the
`DaxStudio.ExcelAddin` VSTO project (net472 only, intentionally left as-is) and a
doc-comment mention in the helper.

## Notes / risks

- **AdomdClient on net8.0**: `Microsoft.AnalysisServices.AdomdClient` ships a `net8.0`
  build (already used via `AdomdClientTfmFolder`). MSOLAP/native providers remain
  Windows-only, but XMLA-over-HTTP works cross-platform — that is the target scenario
  for a cross-platform `dscmd`.
- **Keep desktop green**: every step preserves `net472` and `net8.0-windows`; `net8.0`
  is additive. The WPF app (`DaxStudio.UI`) is untouched.
- **`dscmd` full-command coverage** for `net8.0` is a follow-up: some commands may still
  assume Windows paths/registry; the smoke test targets the core query path first.
