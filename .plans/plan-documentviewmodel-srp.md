# DocumentViewModel.cs — Single Responsibility refactor

> **Refreshed 2026-07-07** against the current source. The metrics below and the
> per-phase notes were re-measured; see **"Refresh notes"** immediately after this
> section for what has changed since the plan was first written and how it
> re-scopes individual phases. None of the nine planned services exist yet — this
> is still a greenfield refactor.

## Why this plan exists

`DocumentViewModel.cs` is **4,945 lines, 226 public members, 12 distinct responsibilities, 34 `IHandle<T>` event handlers (30 `HandleAsync` methods), 11 non-`IHandle` interfaces plus the `Screen` base.** It's a textbook god object.

A partial-class split would only shuffle the deck chairs. This plan is about fixing the SRP violation. Line-count is incidental — the goal is one-class-one-job and meaningful unit-testability.

## Refresh notes (2026-07-07)

**Core premise still holds.** The `SubscribeAll()` / `UnsubscribeAll()` pattern (now
line **1075**, was 1076) is unchanged and still subscribes non-VM objects
(`Connection`, `IntellisenseProvider`, `MeasureExpressionEditor.IntellisenseProvider`,
`HelpWatermark`, every `ITraceWatcher`, every `ToolWindow`). The key architectural
insight — that single-responsibility services can be event sinks — is intact.

**The file shrank ~770 lines (5,718 → 4,945).** Several enabling refactors already
landed on `develop` and re-scope specific phases:

- **VPAX core logic moved to `DaxStudio.Core`** (commit `4a560a13`). The heavy
  analyzer computation is gone from the VM; what remains is **dialog/orchestration
  flow** — `ImportAnalysisData`, `ExportAnalysisData`, plus `*Async` variants that
  now delegate to a VertiPaq view-model (`vm.ExportAnalysisDataAsync(...)`) with a
  local fallback. **Phase 4 is smaller than originally scoped** — it's now mostly
  extracting the dialog/retry orchestration, not the analysis engine.
- **`TraceWatcherBaseViewModel` split into Core base + UI shell** (`32ed8eed`,
  `182583e1`). The watcher hierarchy is already decomposed; **Phase 5** now extracts
  the VM-side *orchestration* (`TraceWatchers` collection, show/hide, reconnect)
  against the already-refactored watchers.
- **`ConnectionManager` moved to `DaxStudio.Core`** via inheritance split
  (`96f21d91`). `IDocumentContext.Connection` should be typed as the Core
  `IConnectionManager`, not the UI type.
- **Publishers switched to `PublishAsync`; subscribers own thread marshalling**
  (`ffb542dd`). Extracted services that publish must use `PublishAsync`; the
  `OutputMessage`/`OutputError` helpers already publish this way.

**New events to fold into the service map (weren't in the original tables).** The
`IHandle<T>` count grew 30 → 34. Assign the following, currently handled inline on
the VM, when the matching service is extracted:

| Event | Target service |
|---|---|
| `ChangeThemeEvent` | `DocumentLifecycle` (Phase 8) — VM-level appearance state |
| `RunStyleChangedEvent` | `QueryExecutionService` (Phase 7) — affects run behaviour |
| `QueryResultsPaneMessageEvent` | `QueryExecutionService` (Phase 7) |
| `SetSelectedWorksheetEvent` | `QueryExecutionService` (Phase 7) — Excel output target |
| `ExportDaxFunctionsEvent` | `VertiPaqAnalyzerService` or a small export helper (Phase 4) |
| `LoadQueryBuilderEvent` | `ToolWindowManager` (Phase 8) |
| `ShowMeasureExpressionEditor` | `EditorTextService` (Phase 3) |
| `ShowTablesInModelDiagramEvent` | `ToolWindowManager` (Phase 8) |
| `SetFocusEvent` | stays on the VM (coordinator-level focus routing) |

**Unchanged and still accurate:** the SDK-style dual-target csproj note
(`net472;net8.0-windows`, new `.cs` files auto-included), the `IDocumentContext`
back-reference guard, the testability argument, and the out-of-scope list.

## The 12 responsibilities currently jammed into the class

1. **Document state / file persistence** — read/write `.dax`, `.daxx`, `.vpax`, `.ovpax`; orchestrate `ISaveState` tool windows; autosave.
2. **Query execution** — run DAX, manage lifecycle (`IsQueryRunning`, stopwatch, cancellation, output routing).
3. **Editor integration** — `GetEditor`, insert/replace/append, formatting, comments, case changes, find/replace, goto line.
4. **Trace watcher orchestration** — instantiate factories, show/hide windows, restart on reconnect, shut down on close.
5. **VertiPaq Analyzer** — view/export/import VPAX, dialog flow, retry-without-stats fallback.
6. **Drag & drop** — `IDropTarget` implementation.
7. **Output / messaging** — `OutputMessage`/`OutputError`/`OutputWarning`/`ActivateOutput`.
8. **Tool-window collection** — `ToolWindows`, `ShowToolWindow`, `CloseToolWindow`, dock-manager layout load/save.
9. **Connection lifecycle** — wraps `ConnectionManager`, restarts traces on `AfterReconnect`.
10. **`Screen` / `IGuardClose` / `IHaveShutdownTask`** — close confirmation, shutdown sequence.
11. **AvalonDock layout participation** — `IDaxDocument`, view-model conventions.
12. **Event bridge** — 30 `IHandle<T>` interfaces, each dispatching to one of the above subsystems.

## The constraint that looked like a wall — but isn't

> "Caliburn requires `IHandle<T>` on the registered MEF part, so nothing can move."

False in this codebase. `DocumentViewModel.SubscribeAll()` (line 1075) already calls `_eventAggregator.SubscribeOnUIThread(...)` against:

- `this` (the VM)
- `Connection` (the `ConnectionManager`)
- `IntellisenseProvider`
- `MeasureExpressionEditor.IntellisenseProvider`
- `HelpWatermark`
- every `ITraceWatcher`
- every `ToolWindow` in `ToolWindows`

Any object the VM owns and subscribes can implement `IHandle<TEvent>` and receive the same dispatch. The pattern is already proven and symmetrically torn down in `UnsubscribeAll()`. **Events can be handled directly by single-responsibility services**, the VM stops being the event sink for everything.

What *does* have to stay on `DocumentViewModel`:

- The MEF `[Export(typeof(Screen))]` / `[Export(typeof(DocumentViewModel))]` and the class name (AvalonDock + Caliburn view-model convention).
- Any method invoked from XAML via `cal:Message.Attach="[Action MethodName(...)]"` or Caliburn action-by-name. These remain as **one-line shims** that delegate to a service.
- XAML-bound properties — but these can be facades (`get => _persistence.IsDirty;`).

## Target architecture

`DocumentViewModel` becomes a **coordinator**:

- Hosts the MEF/AvalonDock identity.
- Owns the service instances (constructed in `Init`, disposed in `Dispose`).
- Exposes the small set of XAML-bound properties, most delegating to a service.
- Holds one-line Caliburn action shims.
- Subscribes each service via `_eventAggregator.SubscribeOnUIThread(service)`.

**Realistic post-refactor size: ~600–900 lines, all coordination — no domain logic.**

### Single-responsibility services (plain `internal` classes, not MEF parts)

| Service | Responsibility | `IHandle<T>` it absorbs | Public methods it absorbs |
|---|---|---|---|
| `DocumentPersistenceService` | open/save/autosave for `.dax`/`.daxx`/`.vpax`/`.ovpax`; orchestrate `ISaveState` | `LoadFileEvent`, `DockManagerSaveLayout`, `DockManagerLoadLayout` | `Save`, `SaveAs`, `SavePackageFile`, `SaveSingleFiles`, `LoadFile`, `LoadPackageFile`, `LoadStateAsync` (both overloads), `AutoSaveAsync`, `DeleteAutoSave` |
| `QueryExecutionService` (`IQueryRunner`) | run a DAX query, manage lifecycle (timer, cancellation), publish results | `RunQueryEvent`, `CancelQueryEvent` | `RunQuery`, `ExecuteDataTableQueryAsync`, `QueryStopWatch`, `RefreshElapsedTime`, `IsQueryRunning` |
| `EditorTextService` | all editor-text manipulation | `SelectionChangeCaseEvent`, `CommentEvent`, `ToggleCommentEvent`, `SendTextToEditor`, `SendTabularObjectToEditor`, `DefineMeasureOnEditor`, `EditorHotkeyEvent`, `NavigateToLocationEvent` | `InsertTextAtCaret`, `InsertTextAtSelection`, `AppendText`, `MergeParameters`, `FormatDax`, `Find`, `Replace`, `GotoLine`, selection helpers |
| `TraceWatcherOrchestrator` (`IHaveTraceWatchers`) | trace watcher factories, show/hide, reconnect, shutdown | `TraceWatcherToggleEvent`, `TraceChangedEvent`, `TraceChangingEvent`, `ShowTraceWindowEvent`, `CloseTraceWindowEvent`, `PasteServerTimingsEvent`, `ReconnectEvent` | `TraceWatchers`, `DisplayTraceWindow`, `ShutDownTraces`, reconnect handler |
| `VertiPaqAnalyzerService` | view/export/import VPAX, dialog flow, retry-without-stats | (none) | `ViewAnalysisDataAsync`, `ExportAnalysisDataAsync`, `ImportAnalysisData`, `GetSelectedModelName` |
| `DocumentDropHandler` (`IDropTarget`) | drag/drop target | (none) | `DragOver`, `Drop`, `OnDragEnterPreview` |
| `DocumentOutputWriter` | façade for `OutputMessage` publishes | `OutputMessage` (forwards to `OutputPane`) | `OutputMessage`, `OutputError`, `OutputWarning`, `ActivateOutput` |
| `ToolWindowManager` | `ToolWindows` collection, dock layout | `ShowToolWindowEvent` | `ToolWindows`, ShowToolWindow handler |
| `DocumentLifecycle` (`IGuardClose`, `IHaveShutdownTask`) | shutdown, dirty check, autosave teardown | `ApplicationActivatedEvent`, `CancelConnectEvent`, `ConnectEvent`, `UpdateGlobalOptions` | `GetShutdownTask`, `ShutDownTraces`, `DoCloseCheck` |

### Avoiding a new god object via the back door

Services must not hold a back-reference to the whole `DocumentViewModel` — that would just rebuild the god object via the back door. Instead extract a thin `IDocumentContext` interface that exposes only the small surface services need to reach into:

```csharp
internal interface IDocumentContext
{
    IConnectionManager Connection { get; }
    IGlobalOptions Options { get; }
    Guid DocumentId { get; }
    DAXEditor GetEditor();
    IDocumentOutputWriter Output { get; }
    void SetDirty(bool isDirty);
    void SetState(DocumentState state);
    // …only what's actually needed by a service
}
```

`DocumentViewModel` implements `IDocumentContext`. Each service gets an `IDocumentContext` and is unit-testable with a mock — without the MEF/Caliburn/AvalonDock stack.

## Testability — the actual prize

Today `DocumentViewModel` is effectively un-unit-testable (no test in `tests/DaxStudio.Tests` references it directly). After the split:

- `QueryExecutionService` — stub `IConnectionManager`, no WPF/MEF.
- `DocumentPersistenceService` — round-trip `.daxx` against a `MemoryStream`-backed `Package`.
- `EditorTextService` — fake `IEditorAccessor` returning an in-memory `TextDocument`; every comment/format/case-change becomes a trivial unit test.
- `TraceWatcherOrchestrator` — start/stop/reconnect against a stub `ITraceWatcher`.
- `VertiPaqAnalyzerService` — already mostly testable once decoupled.
- `DocumentOutputWriter` — assert the correct `OutputMessage` shape is published.

## Migration phases (each shippable independently)

For every phase: **N** = methods to move, **F** = new files, **I** = `IHandle<T>` moved off the VM, **X** = XAML binding sites to audit (`rg "cal:Message.Attach"` + action-by-name in `DocumentView.xaml`), **T** = unit tests to add.

### Phase 1 — `IDocumentContext` + `DocumentOutputWriter`
- **N:** 4 (`OutputMessage`, `OutputError`, `OutputWarning`, `ActivateOutput`)
- **F:** 2–3 (interface + writer + small DTOs)
- **I:** 1 (`OutputMessage`)
- **X:** low — output helpers aren't XAML-bound
- **T:** 1–2 tests against `DocumentOutputWriter`
- **Notes:** smallest diff, proves the context-injection pattern, doubles as a **calibration run** for sizing the rest.

### Phase 2 — `DocumentDropHandler` (`IDropTarget`)
- **N:** 3 (`DragOver`, `Drop`, `OnDragEnterPreview`)
- **F:** 1
- **I:** 0
- **X:** 1 (drop binding on the editor host in `DocumentView.xaml`)
- **T:** 1
- **Notes:** genuinely self-contained, no Caliburn action binding involved.

### Phase 3 — `EditorTextService`
- **N:** ~18 (every text-manipulation method)
- **F:** 1–2
- **I:** 8
- **X:** **medium-high** — most of these methods are bound by Caliburn action-by-name from ribbon buttons; expect one shim per moved entry-point
- **T:** good test return — each operation is small and side-effect free
- **Notes:** biggest readability win.

### Phase 4 — `VertiPaqAnalyzerService`
- **N:** 4
- **F:** 1
- **I:** 0–1 (`ExportDaxFunctionsEvent`, if folded in here)
- **X:** 1–2 (View Metrics / Export Metrics ribbon buttons)
- **T:** moderate — async file I/O and dialog flow
- **Notes:** **Re-scoped 2026-07-07.** The analyzer *engine* already moved to
  `DaxStudio.Core` (commit `4a560a13`); the VM now delegates via
  `vm.ExportAnalysisDataAsync(...)`. This phase extracts only the remaining
  **dialog / retry-without-stats orchestration**, so it's smaller than first
  scoped. Bounded feature, no inter-service dependencies.

### Phase 5 — `TraceWatcherOrchestrator` (`IHaveTraceWatchers`)
- **N:** ~6
- **F:** 1
- **I:** 6–7
- **X:** low — mostly event-driven, not action-bound
- **T:** start/stop/reconnect tests against a stub `ITraceWatcher`
- **Notes:** also picks up the `Connection_AfterReconnect` handler.
  **Re-scoped 2026-07-07.** `TraceWatcherBaseViewModel` already split into a Core
  base + UI shell (`32ed8eed` / `182583e1`), so this phase extracts the VM-side
  *orchestration* against the already-decomposed watcher hierarchy.

### Phase 6 — `DocumentPersistenceService`
- **N:** ~12
- **F:** 1–2
- **I:** 1 (`LoadFileEvent`)
- **X:** 2 (Save / SaveAs on the ribbon)
- **T:** highest test return — round-trip every file format against a `MemoryStream`
- **Notes:** most complex due to async file I/O on the dispatcher; own PR.

### Phase 7 — `QueryExecutionService` (`IQueryRunner`)
- **N:** ~8
- **F:** 1
- **I:** 2 (`RunQueryEvent`, `CancelQueryEvent`)
- **X:** 2 (Run / Cancel ribbon buttons)
- **T:** stub `IConnectionManager`, assert state machine transitions
- **Notes:** last so we can see what state the VM still owns vs. what belongs in the service.

### Phase 8 — `ToolWindowManager` + `DocumentLifecycle` mop-up
- **N:** ~10
- **F:** 2
- **I:** 5–6
- **X:** low
- **T:** moderate — these are mostly orchestration
- **Notes:** by this point the VM should be coordinator-only.

## On effort and calendar time — honest answer

I previously gave per-phase estimates in days. **That was developer-day intuition, not a measurement.** I didn't account for who's doing the work, didn't separate coding from review, didn't validate against any actual extraction in this codebase. Treat any calendar number from me for this refactor as a guess.

The per-phase work-unit counts above (N/F/I/X/T) are what I can describe with confidence — derived by reading the current file. Map them to your own velocity (or mine, if I drive it).

If you want a defensible calendar number, the only honest path is:

1. Run **Phase 1** end-to-end as a measured pilot — open, extract, write tests, get a green build, get the diff into review-ready shape.
2. Record actual wall-clock + iteration count.
3. Project Phases 2–8 from the work-unit ratios.

If you want me to drive the work, Phase 1 is also the natural starting point precisely because it doubles as the calibration run.

## Other costs not in the per-phase table

- **Build hygiene.** `DaxStudio.UI.csproj` is **SDK-style** (`<Project Sdk="Microsoft.NET.Sdk">`, dual-targets `net472;net8.0-windows`). New `.cs` files under the project are **auto-included** — no `<Compile Include>` entries needed for new files. (The `<Compile Remove>` block in the csproj is for excluding stale orphans; new files just work.)
- **Smoke testing per phase.** Build-and-test skill is the floor: close DaxStudio, build standalone, run the test suite. Beyond that, manual smoke (open file, run query, view metrics, save) is the only behavioural-regression backstop because the existing test suite doesn't cover `DocumentViewModel` end-to-end.
- **Review surface.** Each phase produces a meaningful diff (500–800 lines moved is common). Reviewable, but only if each phase stays strictly scoped — combining phases multiplies review burden non-linearly.

## Out of scope

- Renaming `DocumentViewModel` (would break Caliburn naming convention with `DocumentView.xaml`).
- Splitting `DocumentView.xaml` — this refactor is VM-side only.
- Replacing Caliburn action binding with explicit commands (would touch every view).
- Making services MEF parts — kept as plain classes constructed in `Init` so the test harness can `new` them directly.