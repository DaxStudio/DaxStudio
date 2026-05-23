# Plan: Create DaxStudio.Core — Full Scope

## Problem Statement

`DaxStudio.UI` is a monolithic WPF library containing business logic, data access, export services, parsers, and UI code all mixed together. This forces the CommandLine project (`dscmd`) to reference the entire WPF assembly graph just to use ~20 non-UI types. A new `DaxStudio.Core` project will hold all non-UI business logic, enabling the CommandLine to drop its `DaxStudio.UI` reference and opening the door to .NET Core and cross-platform CLI in the future.

## Approach

1. Create `DaxStudio.Core` as a net472 class library (no WPF references)
2. Move types from `DaxStudio.UI` in phases, starting with the simplest (enums, interfaces, models) and progressing to more complex (export services, parsers)
3. For types with minor Caliburn.Micro dependencies (`PropertyChangedBase`, `IEventAggregator`), keep `Caliburn.Micro.Core` as a dependency in `DaxStudio.Core` — it targets .NET Standard 2.0 and has no WPF dependency
4. `DaxStudio.UI` adds a reference to `DaxStudio.Core` and continues working as before
5. `DaxStudio.CommandLine` replaces its `DaxStudio.UI` reference with `DaxStudio.Core`

## What Goes Into DaxStudio.Core

### Enums (12 files — no WPF dependencies)
All from `DaxStudio.UI\Enums\`:
- `ExportDataType.cs`, `ExportStatus.cs`
- `DialogResult.cs`, `DocumentState.cs`
- `FileIcons.cs`, `SaveAsExtension.cs`, `SaveResult.cs`
- `OpenDialogResult.cs`, `MultipleQueriesDetectedDialogResult.cs`
- `QueryBuilderItem.cs`, `QueryEndSubClass.cs`
- `SqlAuthenticationType.cs`

**Not moving** (WPF TypeConverter dependency): `CustomTraceOutput.cs`, `FilterType.cs`

### Interfaces (15+ files — no WPF dependencies)
From `DaxStudio.UI\Interfaces\`:
- `IQueryRunner.cs`, `IQueryTextProvider.cs`
- `ISettingProvider.cs`, `IResultsTarget.cs`
- `IDaxDocument.cs`, `IDaxStudioHost.cs`, `IDaxStudioProxy.cs`
- `IExportDataDetails.cs`, `IDocumentWorkspace.cs`
- `ICancellable.cs`, `IDataGridWindow.cs`
- `IMetadataProvider.cs`, `IPowerBIPerformanceData.cs`
- `ITraceDiagnostics.cs`, `ITraceWatcherData.cs`
- `IHaveLongRunningOperation.cs`, `IZoomable.cs`
- `IAutoSaver.cs`, `IQueryBuilderFieldList.cs`, `IQueryPlanRow.cs`

**Not moving** (Caliburn.Micro Screen/IResult/Brush deps): `IToolWindow.cs`, `IHaveShutdownTask.cs`, `ITraceWatcher.cs`, `IHaveTraceWatchers.cs`

### Model Classes (10+ files)
From `DaxStudio.UI\Model\`:
- `ConnectionManager.cs` — central data access class (uses Caliburn.Micro.Core for IEventAggregator but no WPF)
- `SettingsProviderFactory.cs` — factory for settings providers (no WPF)
- `DaxFile.cs` — DAX file data model (no WPF)
- `StatusBarMessage.cs` — simple message wrapper (no WPF)
- `TraceEvent.cs`, `TraceEventFactory.cs` — trace data models (no WPF)
- `XmSqlAnalysis.cs`, `XmSqlQueryFingerprint.cs`, `XmSqlQueryGroup.cs` — parser data models (no WPF)
- `CaptureDiagnosticsSource.cs` — config data model (no WPF)
- `SqlFormatter.cs` — SQL formatting model (no WPF)
- `SelectedTable` (extract from `ExportDataWizardChooseTablesViewModel.cs` to standalone file) — uses PropertyChangedBase (Caliburn.Micro.Core, no WPF)

### Events (~20-30 files — most have no WPF dependency)
From `DaxStudio.UI\Events\`:
- `ConnectEvent.cs`, `ConnectionChangedEvent.cs`, `ConnectionClosedEvent.cs`, `ConnectionOpenedEvent.cs`, `ConnectionPendingEvent.cs`
- `QueryStartedEvent.cs`, `QueryFinishedEvent.cs`, `CancelQueryEvent.cs`, `RunQueryEvent.cs`
- `TraceChangedEvent.cs`, `TraceChangingEvent.cs`, `QueryTraceCompletedEvent.cs`
- `ServerTimingsEvent.cs`, `DatabaseChangedEvent.cs`
- `LoadFileEvent.cs`, `FileSavedEvent.cs`, `FileOpenedEvent.cs`
- `OutputMessage.cs` (base class + `MessageType` enum only — the WPF subclasses `LocationOutputMessage`/`FolderOutputMessage` stay in UI)
- And other simple event classes with no WPF types

**Not moving**: `EditorResizeEvent.cs` (System.Windows.Size), `OutputMessage` WPF subclasses, editor/theme events

### Utils / Services
From `DaxStudio.UI\Utils\`:
- `ModelAnalyzer.cs` — VPAX export/import (split: core logic moves, `GetDictPathForOvpax` file dialog stays in UI)
- `PowerBIHelper.cs` — Power BI instance discovery (no WPF)
- `ParquetExporter.cs` — Parquet export logic (no WPF)
- `XlsxHelper.cs` — Excel export helper (no WPF)
- `SettingsProviderFactory.cs` (already listed in Models)
- `JsonSettingProviderBase.cs`, `JsonSettingProviderPortable.cs`, `JsonSettingProviderAppData.cs` — settings persistence (no WPF)
- `RegistrySettingProvider.cs` — registry settings (no WPF)

### ResultsTargets (4-5 files)
From `DaxStudio.UI\ResultsTargets\`:
- `ResultsTargetTextFile.cs` — CSV/text/JSON export (no WPF)
- `ResultsTargetFormattedTextFile.cs` — formatted text export (no WPF)
- `ResultsTargetExcelFile.cs` — XLSX export via LargeXlsx (no direct WPF, uses Caliburn.Micro.Core)
- `ResultsTargetTimer.cs` — timer-only results (no WPF)

**Not moving**: `ResultsTargetClipboard.cs` (System.Windows.Clipboard), `ResultsTargetGrid.cs`, `ResultsTargetExcelLinked*.cs` (WPF PropertyChangedBase + Excel interop)

### Parser / Query Analysis (14+ files)
From `DaxStudio.UI\Grammars\` and `DaxStudio.UI\Utils\`:
- `xmSQL.g4`, `DirectQuerySql.g4` — ANTLR grammars
- `AntlrXmSqlParser.cs`, `XmSqlParser.cs`, `DirectQuerySqlParser.cs` — parsers
- `IXmSqlParser.cs` — parser interface
- `XmSqlAnalysisVisitor.cs`, `DirectQuerySqlAnalysisVisitor.cs` — ANTLR visitors
- `XmSqlFormattingVisitor.cs`, `XmSqlFingerprintVisitor.cs` — formatting/fingerprinting visitors
- `AntlrXmSqlFormatter.cs` — static formatter
- `XmSqlQueryGrouper.cs` — query grouper
- `SqlBlockParser.cs` — hierarchical SQL block parser

All have **zero WPF dependencies**. Only deps: `Antlr4.Runtime.Standard` (.NET Standard 2.0), `Serilog`.

**Not moving**: `DaxLineParser.cs` (AvalonEdit), `SyntaxHighlightingHelper.cs` (AvalonEdit + ModernWpf)

### Export Service (new — extracted from ExportDataWizardViewModel)
The actual CSV/Parquet/SQL export I/O methods will be extracted from `ExportDataWizardViewModel` into a new `ExportService` class in Core. The ViewModel keeps the UI coordination/wizard flow; Core gets the data pipeline.

## What Stays in DaxStudio.UI

- All XAML Views and code-behind
- All ViewModels (they reference Core types but contain UI logic)
- WPF-specific controls, converters, behaviours, triggers, attached properties
- DAXEditor integration (AvalonEdit-based intellisense, syntax highlighting)
- WPF-coupled events (EditorResizeEvent, theme events, editor events)
- WPF-coupled interfaces (IToolWindow, ITraceWatcher, IHaveShutdownTask)
- Clipboard-based classes (ResultsTargetClipboard, ClipboardTextProvider)
- File dialog wrappers (ModelAnalyzer.GetDictPathForOvpax)
- OutputMessage WPF subclasses (LocationOutputMessage, FolderOutputMessage)

## NuGet Dependencies for DaxStudio.Core

| Package | Version | Why |
|---|---|---|
| Caliburn.Micro.Core | 5.0.258 | IEventAggregator, PropertyChangedBase (.NET Standard 2.0, no WPF) |
| Serilog | 4.3.1 | Logging |
| Newtonsoft.Json | 13.0.4 | Settings serialization |
| Antlr4.Runtime.Standard | 4.13.1 | Parser runtime (.NET Standard 2.0) |
| Antlr4BuildTasks | 12.14.0 | Build-time grammar compilation |
| Dax.Vpax | 1.12.0 | VPAX format |
| Dax.Model.Extractor | 1.12.0 | Model extraction |
| Dax.ViewVpaExport | 1.12.0 | VPA export |
| Dax.Vpax.Obfuscator | 1.2.1 | VPAX obfuscation |
| CsvHelper | 33.1.0 | CSV export |
| LargeXlsx | 2.0.1 | XLSX export |
| Parquet.Net | 5.5.0 | Parquet export |
| Polly | 8.6.6 | Retry policies (ConnectionManager) |
| Microsoft.AnalysisServices.AdomdClient | 19.113.2 | Data access |

**Note:** `Caliburn.Micro.Core` (not `Caliburn.Micro`) — the Core package is .NET Standard 2.0 with zero WPF dependencies. It provides `PropertyChangedBase`, `IEventAggregator`, `EventAggregator`, and `BindableCollection`.

## Project References

| Project | References |
|---|---|
| **DaxStudio.Core** (new) | DaxStudio.Interfaces, DaxStudio.Common, ADOTabular, DaxStudio.SqlFormatter |
| **DaxStudio.UI** (updated) | DaxStudio.Core + existing refs (minus types that moved) |
| **DaxStudio.CommandLine** (updated) | DaxStudio.Core, DaxStudio.Common, DaxStudio.Interfaces, ADOTabular (**drops DaxStudio.UI**) |
| **DaxStudio.Standalone** | unchanged |
| **DaxStudio.ExcelAddin** | unchanged |

## Implementation Phases

### Phase 1: Create project + move enums & interfaces
- Create `DaxStudio.Core` csproj (net472, no WPF refs)
- Move all 12 enums
- Move all 15+ clean interfaces
- Add Core reference to UI and CommandLine
- Build & test

### Phase 2: Move model classes and events
- Move ConnectionManager, SettingsProviderFactory, DaxFile, StatusBarMessage, trace models
- Move xmSQL data models (XmSqlAnalysis, etc.)
- Move clean events (ConnectEvent, query events, trace events, etc.)
- Extract SelectedTable to standalone file in Core
- Extract base OutputMessage + MessageType to Core (WPF subclasses stay)
- Build & test

### Phase 3: Move parsers and ANTLR grammars
- Move .g4 grammars and all parser/visitor/formatter files
- Move SqlBlockParser, XmSqlQueryGrouper
- Configure Antlr4BuildTasks in DaxStudio.Core
- Build & test

### Phase 4: Move VPAX logic and export services
- Split ModelAnalyzer (core to Core, file dialog to UI wrapper)
- Move PowerBIHelper, ParquetExporter, XlsxHelper
- Move ResultsTargetTextFile, ResultsTargetFormattedTextFile, ResultsTargetExcelFile, ResultsTargetTimer
- Move settings providers (JsonSettingProvider*, RegistrySettingProvider)
- Extract export service from ExportDataWizardViewModel
- Build & test

### Phase 5: Remove DaxStudio.UI reference from CommandLine
- Update all CommandLine using statements to reference DaxStudio.Core namespaces
- Remove `<ProjectReference>` to DaxStudio.UI from CommandLine csproj
- Build & verify dscmd.exe no longer loads DaxStudio.UI.dll
- Full test pass

## Notes / Risks

- **Caliburn.Micro.Core is key**: Using the `.Core` package (not the full `Caliburn.Micro`) avoids pulling WPF. It's .NET Standard 2.0 and provides the MVVM primitives many classes use.
- **Namespace changes**: Moving files changes namespaces from `DaxStudio.UI.*` to `DaxStudio.Core.*`. All callers must be updated. Using `TypeForwardedTo` attributes could ease the transition but adds complexity.
- **Build time**: Adding one project has negligible build impact. MSBuild parallelizes independent projects, and separating ANTLR from WPF compilation may actually speed incremental builds.
- **Future .NET Core**: Once Core exists with no WPF refs, the CommandLine can later be retargeted to `net8.0` by multi-targeting the shared projects — but that's a separate effort.

