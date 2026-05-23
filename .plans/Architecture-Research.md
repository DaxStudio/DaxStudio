# DAX Studio Architecture Investigation: .NET Core Migration, Parser Extraction & Cross-Platform CLI

## Executive Summary

This report investigates three interrelated architectural questions about the DAX Studio codebase: (1) moving the Standalone and CommandLine projects to .NET Core while reducing the Excel Addin's dependency footprint, (2) extracting parser logic from `DaxStudio.UI` to simplify the WPF build, and (3) making the CommandLine tool cross-platform. The findings reveal that all three goals are **feasible but interconnected** — they share a common prerequisite of extracting non-UI business logic out of `DaxStudio.UI` into a new shared project. The Excel Addin already does NOT reference `DaxStudio.UI` and uses an IPC model, so it would not be directly affected by a .NET Core migration of the other entry points. The parser code in `DaxStudio.UI` is largely WPF-free and is a strong candidate for extraction. The CommandLine project uses ~20 types from `DaxStudio.UI`, most of which have no inherent WPF dependency and could be relocated. The key blocker for cross-platform CLI is not the code itself but ensuring all NuGet dependencies (ADOMD, Dax.Vpax, etc.) support .NET Core — which they now do.

---

## Architecture Overview: Current State

### Project Dependency Graph

```
                    ┌──────────────────────┐
                    │  DaxStudio.Standalone │  (.NET 4.7.2, WinExe)
                    │     (DaxStudio.exe)   │
                    └──────────┬───────────┘
                               │
                    ┌──────────▼───────────┐
                    │    DaxStudio.UI       │  (.NET 4.7.2, WPF Library)
                    │ (ViewModels, Views,   │
                    │  Models, Parsers,     │
                    │  Export Logic, Utils)  │ ◄──── 31 NuGet pkgs incl.
                    └──┬───┬───┬───┬───┬──┘       Caliburn.Micro, ANTLR4,
                       │   │   │   │   │          Fluent.Ribbon, AvalonEdit,
                       │   │   │   │   │          CsvHelper, Parquet.Net...
    ┌──────────────────┘   │   │   │   └──────────────────┐
    ▼                      ▼   │   ▼                      ▼
┌──────────┐  ┌───────────┐   │ ┌───────────┐  ┌─────────────────┐
│ADOTabular│  │DAXEditor   │   │ │QueryTrace │  │DaxStudio.Common │
│          │  │(AvalonEdit)│   │ │(SignalR)  │  │(WPF refs!)      │
└──────────┘  └───────────┘   │ └───────────┘  └─────────────────┘
                               │                        ▲
                    ┌──────────▼───────────┐            │
                    │DaxStudio.Interfaces  │────────────┘
                    └──────────────────────┘

    ┌──────────────────────┐         ┌──────────────────────┐
    │DaxStudio.CommandLine │         │DaxStudio.ExcelAddin  │
    │    (dscmd.exe)       │         │    (VSTO Add-in)     │
    │  .NET 4.7.2, Exe     │         │  .NET 4.7.2, Library │
    │  Refs: UI, Common,   │         │  Refs: ADOTabular,   │
    │  Interfaces, ADOTab  │         │  Common, Interfaces, │
    │  Spectre.Console     │         │  QueryTrace.Excel    │
    └──────────────────────┘         │  *** NO UI ref ***   │
                                     └──────────────────────┘
                                              │
                                    IPC (HTTP/SignalR) ──► DaxStudio.exe
```

### Key Facts

- **All 19 projects** target .NET Framework 4.7.2, all using old-style `.csproj` format[^1]
- **DaxStudio.UI** is the monolithic center — contains ViewModels, Views, Models, Parsers, Export Logic, and Utilities all in one assembly[^2]
- **ExcelAddin does NOT reference DaxStudio.UI** — it communicates with the Standalone app via HTTP/SignalR over localhost[^3]
- **CommandLine references DaxStudio.UI** and uses ~20 types from it, gaining a transitive WPF dependency[^4]
- **DaxStudio.Common** has WPF references (PresentationCore, PresentationFramework, WindowsBase) for only a few files[^5]

---

## Question 1: Moving Standalone & CommandLine to .NET Core + Reducing Excel Addin Dependencies

### 1.1 .NET Core Migration Feasibility

**The good news:** All critical NuGet dependencies now support .NET Core/.NET 6+:

| Dependency | .NET Core Support | Notes |
|---|---|---|
| Microsoft.AnalysisServices.AdomdClient 19.x | ✅ .NET 6+ and .NET Framework 4.7.2 | Multi-target since v19.48[^6] |
| Caliburn.Micro 5.x | ✅ .NET 8/9 WPF | Also supports Avalonia, MAUI[^7] |
| Dax.Model.Extractor / Dax.Vpax 1.12 | ✅ .NET 6+ and .NET Framework 4.7.2 | Dual-target[^8] |
| Spectre.Console | ✅ .NET Standard 2.0 | Already cross-platform[^9] |
| Serilog | ✅ .NET Standard 2.0 | Already cross-platform |
| ANTLR4 Runtime | ✅ .NET Standard 2.0 | Already cross-platform |
| CsvHelper | ✅ .NET Standard 2.0 | Already cross-platform |
| Parquet.Net | ✅ .NET 6+ | Cross-platform |

**The challenges for Standalone:**

The WPF Standalone app can move to .NET Core's WPF support (net8.0-windows), but this is still Windows-only. Key friction points:
- **Old-style `.csproj` → SDK-style migration** for all projects in the chain — this is a significant mechanical effort for 19 projects[^1]
- **Fluent.Ribbon 9.x** supports .NET Core WPF but requires testing[^10]
- **AvalonDock** (Dirkster) supports .NET Core WPF[^10]
- **MEF (`System.ComponentModel.Composition`)** — works differently in .NET Core; may require migrating to `Microsoft.Extensions.DependencyInjection` or `System.Composition`

**The challenges for CommandLine:**

The CommandLine project is much simpler to migrate since it doesn't use WPF directly. Its main blockers are the transitive WPF dependencies it pulls from `DaxStudio.UI`[^4].

### 1.2 Excel Addin Dependency Analysis

The Excel Addin is **already well-isolated**[^3]. It does NOT reference DaxStudio.UI. Its dependency chain is:

```
ExcelAddin → ADOTabular, Common, Interfaces, QueryTrace, QueryTrace.Excel
```

The Addin launches DaxStudio.exe as a separate process and communicates via:
1. **WM_COPYDATA** — sends the localhost port number to DaxStudio.exe[^11]
2. **OWIN self-hosted HTTP server** on port 9000-9999 — serves XMLA queries and workbook info[^12]
3. **SignalR hub** — for real-time query trace event streaming[^13]

**Therefore the Excel Addin is NOT affected by moving Standalone/CommandLine to .NET Core.** The Addin must remain .NET Framework 4.7.2 because VSTO requires it. It doesn't carry the weight of DaxStudio.UI.

**Potential size reduction for the Addin:**

The Addin's biggest dependencies are:
- `Microsoft.AnalysisServices` + `AdomdClient` — required, cannot remove
- `Microsoft.Identity.Client` suite (MSAL) — required for Entra auth
- `Microsoft.PowerBI.Api` — in `DaxStudio.Common`
- `Caliburn.Micro` — pulled in by `DaxStudio.QueryTrace`
- `SignalR` libraries — required for IPC

The main opportunity to reduce Addin size would be to **remove the Caliburn.Micro dependency from QueryTrace**. Currently `DaxStudio.QueryTrace` references Caliburn.Micro for `IEventAggregator`[^14]. If `QueryTrace` could use a lightweight event aggregator or interface abstraction instead, the Addin wouldn't need to ship Caliburn.Micro at all.

### 1.3 Recommended Approach: New Shared Project

The most impactful change would be creating a **new `DaxStudio.Core` (or `DaxStudio.Services`) project** to hold the non-UI business logic currently trapped in `DaxStudio.UI`. This would:

1. **Unblock CommandLine from DaxStudio.UI** — the CLI only needs export logic, connection management, and settings, not WPF ViewModels
2. **Enable the CommandLine to target .NET Core** without pulling in WPF transitively
3. **Keep Standalone on .NET Framework (or migrate to net8.0-windows WPF later)** without disrupting the main app

Classes that should move to this new project (all have no inherent WPF dependency):

| Class | Current Location | WPF Dependency | Notes |
|---|---|---|---|
| `ConnectionManager` | `DaxStudio.UI.Model` | None[^15] | Pure data access, already clean |
| `SettingsProviderFactory` | `DaxStudio.UI.Model` | None[^16] | Factory pattern, already clean |
| `JsonSettingProviderBase/Portable/AppData` | `DaxStudio.UI.Utils` | None | Settings persistence |
| `RegistrySettingProvider` | `DaxStudio.UI.Utils` | None | Settings persistence |
| `ModelAnalyzer` | `DaxStudio.UI.Utils` | Superficial[^17] | Only file dialog is WPF; extract core logic |
| Export engine methods from `ExportDataWizardViewModel` | `DaxStudio.UI.ViewModels` | Via base class[^18] | Extract I/O logic to service class |
| `SelectedTable` | `DaxStudio.UI.ViewModels` | Via `PropertyChangedBase`[^19] | Convert to POCO or use INotifyPropertyChanged |
| `OutputMessage` / `MessageType` | `DaxStudio.UI.Events` | Minimal[^20] | Base class is clean; subclasses are WPF |
| `IQueryRunner` | `DaxStudio.UI.Interfaces` | None[^21] | Already a clean interface |
| `ISettingProvider` | `DaxStudio.UI.Interfaces` | None | Already a clean interface |
| `IDocumentToExport` | `DaxStudio.UI.Interfaces` | None | Already a clean interface |
| `IDaxDocument` | `DaxStudio.UI.Interfaces` | None | Already a clean interface |

---

## Question 2: Extracting Parser Logic from DaxStudio.UI

### 2.1 Parser Inventory

DaxStudio.UI contains a sophisticated parser infrastructure for two distinct languages[^22]:

**ANTLR4 Grammar-Based Parsers:**

| Component | File | Purpose | WPF Deps |
|---|---|---|---|
| xmSQL Grammar | `src\DaxStudio.UI\Grammars\xmSQL.g4` | VertiPaq Storage Engine query language | None |
| DirectQuery Grammar | `src\DaxStudio.UI\Grammars\DirectQuerySql.g4` | T-SQL subset for DirectQuery events | None |
| ANTLR xmSQL Parser | `src\DaxStudio.UI\Utils\AntlrXmSqlParser.cs` | Formal grammar parser implementation | None |
| xmSQL Analysis Visitor | `src\DaxStudio.UI\Utils\XmSqlAnalysisVisitor.cs` | Populates XmSqlAnalysis from parse tree | None |
| DirectQuery Visitor | `src\DaxStudio.UI\Utils\DirectQuerySqlAnalysisVisitor.cs` | Handles DirectQuery parse trees | None |
| xmSQL Formatting Visitor | `src\DaxStudio.UI\Utils\XmSqlFormattingVisitor.cs` | Query formatting/simplification | None |
| xmSQL Fingerprint Visitor | `src\DaxStudio.UI\Utils\XmSqlFingerprintVisitor.cs` | Query grouping via MD5 fingerprints | None |
| xmSQL Formatter | `src\DaxStudio.UI\Utils\AntlrXmSqlFormatter.cs` | Static formatting entry point | None |
| xmSQL Query Grouper | `src\DaxStudio.UI\Utils\XmSqlQueryGrouper.cs` | Groups structurally similar queries | None |

**Regex-Based Parsers (Legacy/Fallback):**

| Component | File | Purpose | WPF Deps |
|---|---|---|---|
| Regex xmSQL Parser | `src\DaxStudio.UI\Utils\XmSqlParser.cs` | Multi-pass regex parser (older) | None |
| DirectQuery SQL Parser | `src\DaxStudio.UI\Utils\DirectQuerySqlParser.cs` | Regex-based DirectQuery parser | None |
| SQL Block Parser | `src\DaxStudio.UI\Utils\SqlBlockParser.cs` | Hierarchical nested subquery parser | None |
| Parser Interface | `src\DaxStudio.UI\Utils\IXmSqlParser.cs` | Strategy interface for parser switching | None |

**Data Models:**

| Component | File | Purpose | WPF Deps |
|---|---|---|---|
| XmSqlAnalysis | `src\DaxStudio.UI\Model\XmSqlAnalysis.cs` | Top-level analysis result container | None |
| XmSqlQueryFingerprint | `src\DaxStudio.UI\Model\XmSqlQueryFingerprint.cs` | Fingerprint data model | None |
| XmSqlQueryGroup | `src\DaxStudio.UI\Model\XmSqlQueryGroup.cs` | Query group model | None |

**Parser-Adjacent (WPF-Coupled):**

| Component | File | Purpose | WPF Deps |
|---|---|---|---|
| DaxLineParser | `src\DaxStudio.UI\Utils\Intellisense\DaxLineParser.cs` | DAX intellisense state machine | **Yes** — AvalonEdit[^23] |
| SyntaxHighlightingHelper | `src\DaxStudio.UI\Utils\SyntaxHighlightingHelper.cs` | xmSQL/DirectQuery highlighting | **Yes** — AvalonEdit, ModernWpf[^24] |

### 2.2 Build Impact of ANTLR in DaxStudio.UI

The ANTLR integration uses `Antlr4BuildTasks` (a build-time MSBuild task) to compile `.g4` grammars into C# lexer/parser classes at build time[^25]. This means:

1. **Every build of DaxStudio.UI** runs the ANTLR code generation step
2. The generated code goes into `DaxStudio.UI.Grammars.Generated` namespace
3. This adds build-time overhead and complexity to the already-large WPF project
4. ANTLR build errors can be confusing when mixed with WPF XAML compilation errors

### 2.3 Extraction Recommendation

**Create a `DaxStudio.QueryAnalysis` project** (or similar name) containing:

```
DaxStudio.QueryAnalysis/
├── Grammars/
│   ├── xmSQL.g4
│   ├── DirectQuerySql.g4
│   └── Generated/  (auto-generated by Antlr4BuildTasks)
├── Parsers/
│   ├── IXmSqlParser.cs
│   ├── AntlrXmSqlParser.cs
│   ├── XmSqlParser.cs  (regex fallback)
│   ├── DirectQuerySqlParser.cs
│   └── SqlBlockParser.cs
├── Visitors/
│   ├── XmSqlAnalysisVisitor.cs
│   ├── DirectQuerySqlAnalysisVisitor.cs
│   ├── XmSqlFormattingVisitor.cs
│   └── XmSqlFingerprintVisitor.cs
├── Formatters/
│   ├── AntlrXmSqlFormatter.cs
│   └── XmSqlQueryGrouper.cs
└── Models/
    ├── XmSqlAnalysis.cs
    ├── XmSqlQueryFingerprint.cs
    └── XmSqlQueryGroup.cs
```

**NuGet dependencies for this new project:**
- `Antlr4.Runtime.Standard` (4.13.1) — .NET Standard 2.0 ✅
- `Antlr4BuildTasks` — build-time only
- `Serilog` — .NET Standard 2.0 ✅

**What stays in DaxStudio.UI:**
- `DaxLineParser` — tightly coupled to AvalonEdit for intellisense
- `SyntaxHighlightingHelper` — WPF-specific highlighting
- All XAML views and ViewModels that consume parser results

### 2.4 Build/Runtime Impact of More Projects

**Your concern about adding projects is valid.** Here's the analysis:

**Build time impact:**
- **MSBuild parallelism** — MSBuild builds independent projects in parallel. A new project with no XAML (pure C# + ANTLR) will build faster than it currently does inside the WPF project, because:
  - ANTLR grammar compilation won't compete with XAML compilation
  - The new project can be cached (incremental build) independently
  - On subsequent builds, if only XAML changes, the parser project won't rebuild
- **Net effect: Likely neutral to slightly faster** for incremental builds, slightly slower for clean builds (one more project to initialize)

**Runtime impact:**
- **Assembly loading** — One additional DLL. The CLR loads assemblies on first use. Since the parser is used during trace analysis (not at startup), it won't affect app startup time
- **JIT compilation** — No change; the same code JITs regardless of which assembly it's in
- **Net effect: Negligible** — no measurable runtime difference

**The real benefit:**
- **Simpler builds** — ANTLR grammar errors are isolated from WPF XAML errors
- **Testability** — Parser logic can be unit tested without a WPF test host
- **Reusability** — The CommandLine tool (or other tools) could use the parser directly
- **Cleaner separation** — DaxStudio.UI's `.csproj` becomes smaller and easier to manage

---

## Question 3: Making the CommandLine Cross-Platform

### 3.1 Current Blockers for Cross-Platform CLI

The `dscmd.exe` is currently Windows-only because:

1. **Direct project reference to `DaxStudio.UI`** — which brings in WPF transitively[^4]
2. **Transitive dependency on `DaxStudio.Common`** — which has WPF assembly references[^5]
3. **Target framework is .NET Framework 4.7.2** — Windows only[^1]

However, the CommandLine code itself has **zero WPF usings**. It implements UI stubs that satisfy DaxStudio.UI interfaces without using any WPF APIs[^26].

### 3.2 What the CommandLine Actually Needs from DaxStudio.UI

Here's the complete list of types from `DaxStudio.UI` used by the CommandLine, and their WPF coupling status:

| Type | Namespace | WPF Coupled? | Relocatable? |
|---|---|---|---|
| `ConnectionManager` | `DaxStudio.UI.Model` | **No**[^15] | ✅ Move to Core |
| `OptionsViewModel` | `DaxStudio.UI.ViewModels` | **Yes** (inherits `Screen`)[^18] | ⚠️ Extract settings to POCO |
| `ExportDataWizardViewModel` | `DaxStudio.UI.ViewModels` | **Yes** (inherits `Conductor<IScreen>`)[^18] | ⚠️ Extract export service |
| `SettingsProviderFactory` | `DaxStudio.UI.Model` | **No**[^16] | ✅ Move to Core |
| `SelectedTable` | `DaxStudio.UI.ViewModels` | **Minimal** (`PropertyChangedBase`)[^19] | ✅ Convert to POCO |
| `ModelAnalyzer` | `DaxStudio.UI.Utils` | **Superficial** (file dialog only)[^17] | ✅ Extract core methods |
| `OutputMessage` / `MessageType` | `DaxStudio.UI.Events` | **No** (base class)[^20] | ✅ Move to Core |
| `IQueryRunner` | `DaxStudio.UI.Interfaces` | **No**[^21] | ✅ Move to Interfaces |
| `IDocumentToExport` / `IDaxDocument` | `DaxStudio.UI.Interfaces` | **No** | ✅ Move to Interfaces |
| `IDaxStudioHost` | `DaxStudio.UI.Interfaces` | **No** | ✅ Move to Interfaces |
| `ISettingProvider` | `DaxStudio.UI.Interfaces` | **No** | ✅ Move to Interfaces |
| `PowerBIHelper` | `DaxStudio.UI.Utils` | **No** | ✅ Move to Core |
| `ResultsTargetExcelFile` / `TextFile` | `DaxStudio.UI.ResultsTargets` | **Likely** (file output) | ⚠️ May need abstraction |
| `ConnectEvent` | `DaxStudio.UI.UIStubs` (cmdline) | **No** | ✅ Already has cmdline stub |
| `ExportDataType` / `ExportStatus` enums | `DaxStudio.UI.Enums` | **No** | ✅ Move to Core |
| `CustomTraceViewModel` | `DaxStudio.UI.ViewModels` | **Yes** | 🔴 DEBUG only, keep as-is |

### 3.3 WPF in DaxStudio.Common — Can It Be Removed?

`DaxStudio.Common` has WPF references for these specific reasons[^5]:

| File | WPF Usage | Severity | Remediation |
|---|---|---|---|
| `Constants.cs` | `System.Windows.Input.Key` for logging hotkeys | Medium | Replace with custom enum |
| `AppProperties.cs` | Extension method on `System.Windows.Application` | Medium | Refactor to accept `IDictionary` |
| `ApplicationExtensions.cs` | Extension method on WPF `Application` | Medium | Extract logic to static helper |
| `EntraIdHelper.cs` | `ContentControl`, `HwndSource`, `PresentationSource` for MSAL window parenting | **High** | Abstract to `IntPtr` parameter; use `#if` for WPF vs console |
| `PbiServiceHelper.cs` | Similar window handle interop | **High** | Same abstraction approach |
| `CmdLineArgs.cs` | Unused `using System.Windows` | Low | Remove unused import |
| `ObjectExtensions.cs` | Unused `using System.Windows.Navigation` | Low | Remove unused import |

**Strategy for DaxStudio.Common:** Use `#if` conditional compilation or a platform abstraction interface. For MSAL authentication in non-WPF contexts, pass `IntPtr.Zero` as the parent window handle — MSAL will still work but without window parenting (acceptable for CLI).

### 3.4 Recommended Cross-Platform Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    DaxStudio.Core                         │
│              (NEW - .NET Standard 2.0 or net6.0)          │
│                                                           │
│  • ConnectionManager     • Export Services                │
│  • SettingsProviderFactory  • ModelAnalyzer (core logic)  │
│  • SelectedTable (POCO)  • OutputMessage / MessageType    │
│  • IQueryRunner          • ISettingProvider                │
│  • IDocumentToExport     • PowerBIHelper                  │
│  • ExportDataType enums  • Other non-UI interfaces        │
└─────────────────────┬─────────────────────────────────────┘
                      │
         ┌────────────┼────────────┐
         │            │            │
         ▼            ▼            ▼
┌──────────────┐ ┌──────────┐ ┌──────────────────┐
│DaxStudio.UI  │ │ dscmd    │ │DaxStudio.Addin   │
│(WPF, net8.0- │ │(.NET 8+, │ │(.NET 4.7.2 VSTO) │
│ windows)     │ │ console, │ │  Unchanged        │
│              │ │ x-plat)  │ │                    │
└──────────────┘ └──────────┘ └──────────────────┘
```

### 3.5 Step-by-Step Migration Plan

**Phase 1: Extract `DaxStudio.Core`** (lowest risk, highest value)
1. Create new `DaxStudio.Core` project targeting `net472` initially (so everything still builds)
2. Move interfaces (`IQueryRunner`, `ISettingProvider`, `IDocumentToExport`, etc.) from `DaxStudio.UI.Interfaces`
3. Move data classes (`ConnectionManager`, `SettingsProviderFactory`, `SelectedTable`, `MessageType`, enums)
4. Extract export service logic from `ExportDataWizardViewModel` into a new `ExportService` class
5. Create a `GlobalOptionsBase` class (non-WPF) that `OptionsViewModel` inherits from
6. Update all project references
7. Verify existing tests still pass

**Phase 2: Extract `DaxStudio.QueryAnalysis`** (medium risk, medium value)
1. Create new project with ANTLR grammars and parser classes
2. Move all parser/visitor/formatter code listed in Section 2.3
3. `DaxStudio.UI` adds a reference to this new project
4. Verify parser unit tests still pass

**Phase 3: Clean up `DaxStudio.Common` WPF dependencies** (low risk)
1. Remove unused `using` statements (3 files)
2. Abstract `EntraIdHelper.GetHwnd()` to accept `IntPtr` instead of `ContentControl`
3. Replace `System.Windows.Input.Key` constants with a custom enum
4. Refactor `AppProperties`/`ApplicationExtensions` to use `IDictionary`
5. Conditional compilation (`#if`) for any remaining platform-specific code

**Phase 4: Retarget CommandLine to .NET Core** (medium risk)
1. Convert `DaxStudio.Core`, `DaxStudio.Common`, `DaxStudio.Interfaces`, `ADOTabular` to SDK-style `.csproj` with multi-targeting: `<TargetFrameworks>net472;net8.0</TargetFrameworks>`
2. Convert `DaxStudio.CommandLine` to SDK-style targeting `net8.0`
3. Remove the `DaxStudio.UI` project reference from CommandLine (now using `DaxStudio.Core`)
4. Test on Linux/macOS (ADOMD supports .NET 6+ cross-platform)

**Phase 5 (Optional): Retarget Standalone to .NET Core WPF** (higher risk)
1. Convert all WPF projects to SDK-style with `net8.0-windows` target
2. Migrate MEF to `System.Composition` or `Microsoft.Extensions.DependencyInjection`
3. Update VSTO Addin to continue targeting `net472` separately
4. Extensive testing of all UI interactions

### 3.6 Cross-Platform Limitations

Even with .NET Core, some CLI features will have platform caveats:

| Feature | Windows | Linux/macOS | Notes |
|---|---|---|---|
| ADOMD connections | ✅ Full | ✅ Cloud only | On-premises SSAS uses Windows Auth (NTLM/Kerberos), which has limited Linux support |
| Entra ID auth | ✅ Full (with broker) | ⚠️ Limited | MSAL broker uses Windows APIs; CLI would need device code flow on Linux |
| VPAX export | ✅ Full | ✅ Full | Dax.Vpax supports .NET 6+ |
| CSV/Parquet export | ✅ Full | ✅ Full | CsvHelper and Parquet.Net are cross-platform |
| XLSX export | ✅ Full | ✅ Full | LargeXlsx is cross-platform |
| SQL Server export | ✅ Full | ⚠️ Limited | System.Data.SqlClient → Microsoft.Data.SqlClient needed |
| Local .pbix file detection | ✅ Full | ❌ N/A | Power BI Desktop is Windows-only |

---

## Key Repositories & Dependencies

| Package | Version | Supports .NET Core | Cross-Platform | Critical For |
|---|---|---|---|---|
| Microsoft.AnalysisServices.AdomdClient | 19.113.2 | ✅ .NET 6+ | ✅ | All data access |
| Caliburn.Micro | 5.0.258 | ✅ .NET 8/9 | WPF only | MVVM framework (Standalone) |
| Dax.Model.Extractor | 1.12.0 | ✅ .NET 6+ | ✅ | VPAX export |
| Dax.Vpax | 1.12.0 | ✅ .NET 6+ | ✅ | VPAX format |
| Spectre.Console | 0.54.0 | ✅ .NET Standard 2.0 | ✅ | CLI output |
| Antlr4.Runtime.Standard | 4.13.1 | ✅ .NET Standard 2.0 | ✅ | Parser runtime |
| CsvHelper | 33.1.0 | ✅ .NET Standard 2.0 | ✅ | CSV export |
| Parquet.Net | 5.5.0 | ✅ .NET 6+ | ✅ | Parquet export |
| Fluent.Ribbon | 9.0.4 | ✅ .NET Core WPF | Windows only | Ribbon UI |
| SignalR Client | 1.2.2 | ⚠️ Legacy | N/A | Excel Addin IPC |

---

## Confidence Assessment

| Finding | Confidence | Basis |
|---|---|---|
| ExcelAddin does NOT reference DaxStudio.UI | **High** | Verified in .csproj and source analysis[^3] |
| Parser code in UI has no WPF dependencies | **High** | Verified imports in all parser files[^22] |
| ConnectionManager has no WPF deps | **High** | Verified source code[^15] |
| ADOMD supports .NET Core cross-platform | **High** | NuGet metadata + Microsoft docs[^6] |
| ExportDataWizardViewModel logic is extractable | **Medium-High** | Core I/O logic is clean; needs Caliburn.Micro event aggregator abstraction[^18] |
| Adding projects won't hurt build/runtime | **Medium-High** | Based on MSBuild parallelism behavior and CLR assembly loading patterns |
| Cross-platform Entra auth will work | **Medium** | MSAL supports .NET Core but broker features are Windows-specific; device code flow needed on other platforms |
| SQL export on Linux | **Medium-Low** | Requires Microsoft.Data.SqlClient migration; not all SQL auth modes work on Linux |

---

## Footnotes

[^1]: All `.csproj` files use `<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>` — verified across `src\DaxStudio.Standalone\DaxStudio.Standalone.csproj:15`, `src\DaxStudio.CommandLine\DaxStudio.CommandLine.csproj:11`, `src\DaxStudio.UI\DaxStudio.UI.csproj:14`, `src\DaxStudio.Common\DaxStudio.Common.csproj:12`

[^2]: `src\DaxStudio.UI\DaxStudio.UI.csproj` has 10 project references and 31 NuGet packages — lines 1411-1620

[^3]: `src\DaxStudio.ExcelAddin\DaxStudio.ExcelAddin.csproj` project references: ADOTabular, Common, Interfaces, QueryTrace, QueryTrace.Excel — no DaxStudio.UI reference. Communication via OWIN HTTP server in `src\DaxStudio.ExcelAddin\WebHost.cs` and WM_COPYDATA in `src\DaxStudio.Common\WMHelper.cs`

[^4]: `src\DaxStudio.CommandLine\DaxStudio.CommandLine.csproj:138-141` — `<ProjectReference Include="..\DaxStudio.UI\DaxStudio.UI.csproj">`

[^5]: `src\DaxStudio.Common\DaxStudio.Common.csproj:73-80` — references PresentationCore, PresentationFramework, System.Windows, WindowsBase

[^6]: Microsoft.AnalysisServices.AdomdClient NuGet page and [Microsoft Learn docs](https://learn.microsoft.com/en-us/analysis-services/client-libraries) confirm .NET 6+ support

[^7]: [Caliburn.Micro NuGet](https://www.nuget.org/packages/Caliburn.Micro) v5.0.258 supports .NET 8/9 WPF

[^8]: [Dax.Model.Extractor NuGet](https://www.nuget.org/packages/Dax.Model.Extractor/) and [Dax.Vpax NuGet](https://www.nuget.org/packages/Dax.Vpax/) target both net6.0 and net472

[^9]: Spectre.Console targets .NET Standard 2.0 — inherently cross-platform

[^10]: `src\DaxStudio.UI\DaxStudio.UI.csproj:1574` — Fluent.Ribbon 9.0.4; line 1568 — Dirkster.AvalonDock 4.72.1

[^11]: `src\DaxStudio.Common\WMHelper.cs` — WM_COPYDATA for inter-process messaging

[^12]: `src\DaxStudio.ExcelAddin\WebHost.cs` — OWIN self-hosted HTTP server

[^13]: `src\DaxStudio.ExcelAddin\SignalR\QueryTraceHub.cs` — SignalR hub for trace events

[^14]: `src\DaxStudio.QueryTrace\DaxStudio.QueryTrace.csproj` references Caliburn.Micro 5.0.258

[^15]: `src\DaxStudio.UI\Model\ConnectionManager.cs` — no System.Windows imports, implements IConnectionManager, IDmvProvider, IFunctionProvider, IDisposable

[^16]: `src\DaxStudio.UI\Model\SettingsProviderFactory.cs` — pure factory pattern, no WPF imports

[^17]: `src\DaxStudio.UI\Utils\ModelAnalyzer.cs` — only uses `System.Windows.Forms.OpenFileDialog` for dictionary file selection; core VPAX logic has no WPF dependency

[^18]: `src\DaxStudio.UI\ViewModels\ExportDataWizard\ExportDataWizardViewModel.cs` — inherits `Conductor<IScreen>.Collection.OneActive` from Caliburn.Micro; `src\DaxStudio.UI\ViewModels\OptionsViewModel.cs` — inherits `Screen` from Caliburn.Micro

[^19]: `src\DaxStudio.UI\ViewModels\ExportDataWizard\ExportDataWizardChooseTablesViewModel.cs` — `SelectedTable` inherits `PropertyChangedBase` from Caliburn.Micro

[^20]: `src\DaxStudio.UI\Events\OutputMessage.cs` — base `OutputMessage` uses `PropertyChangedBase`; `MessageType` enum has zero WPF deps; subclasses `LocationOutputMessage`/`FolderOutputMessage` use WPF FlowDocument

[^21]: `src\DaxStudio.UI\Interfaces\IQueryRunner.cs` — pure interface with standard .NET types (DataTable, DataSet, Task, string)

[^22]: All parser files verified to have no `System.Windows` imports — `src\DaxStudio.UI\Utils\AntlrXmSqlParser.cs`, `XmSqlParser.cs`, `DirectQuerySqlParser.cs`, `XmSqlAnalysisVisitor.cs`, `DirectQuerySqlAnalysisVisitor.cs`, `XmSqlFormattingVisitor.cs`, `XmSqlFingerprintVisitor.cs`, `AntlrXmSqlFormatter.cs`, `XmSqlQueryGrouper.cs`, `SqlBlockParser.cs`, `IXmSqlParser.cs`

[^23]: `src\DaxStudio.UI\Utils\Intellisense\DaxLineParser.cs` — uses `ICSharpCode.AvalonEdit.Document`

[^24]: `src\DaxStudio.UI\Utils\SyntaxHighlightingHelper.cs` — uses `ICSharpCode.AvalonEdit.Highlighting` and `ModernWpf.ThemeManager`

[^25]: `src\DaxStudio.UI\DaxStudio.UI.csproj:1535-1537` — `<PackageReference Include="Antlr4BuildTasks" PrivateAssets="all" IncludeAssets="build">`

[^26]: `src\DaxStudio.CommandLine\UIStubs\QueryRunner.cs` — implements `IQueryRunner` with logging stubs, no WPF imports; `src\DaxStudio.CommandLine\UIStubs\CmdLineDocument.cs` — implements `IDocumentToExport` with logging stubs
