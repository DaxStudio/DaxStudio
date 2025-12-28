# DaxStudio Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-26

## Active Technologies

- C# (.NET Framework 4.7.2+, matching DaxStudio target) + Caliburn.Micro (MVVM), WPF, existing DaxStudio.QueryTrace infrastructure (001-visual-query-plan)

## Project Structure

```text
src/
  DaxStudio.UI/                    # Main WPF UI project
    ViewModels/
      VisualQueryPlanViewModel.cs  # Main ViewModel for Visual Query Plan
      PlanNodeViewModel.cs         # Individual node ViewModel + tree building
    Views/
      VisualQueryPlanView.xaml     # XAML for the plan visualization
    Model/
      EnrichedQueryPlan.cs         # Data model for enriched plan
      EnrichedPlanNode.cs          # Data model for enriched nodes
      DaxOperatorDictionary.cs     # Operator metadata lookup
    Services/
      PlanEnrichmentService.cs     # Correlates timing data with plan nodes
      PerformanceIssueDetector.cs  # Detects performance issues
    AttachedProperties/
      CanvasPositionAnimation.cs   # Smooth position animations
  DaxStudio.Standalone/            # Entry point, creates full executable
tests/
  DaxStudio.Tests/
    VisualQueryPlan/               # Test files for Visual Query Plan
      Fixtures/                    # Test fixture data (JSON plans)
```

## Code Style

- Follow existing C# conventions in the codebase
- Use MVVM pattern (Caliburn.Micro) - ViewModels should inherit from `Screen` or `PropertyChangedBase`
- Use `static readonly Regex` with `RegexOptions.Compiled` for frequently used patterns
- Keep performance in mind - avoid O(n²) patterns in loops (use dictionaries for lookups)
- Add unit tests for new functionality in `tests/DaxStudio.Tests/VisualQueryPlan/`

## Recent Changes

- 001-visual-query-plan: Added C# (.NET Framework 4.7.2+, matching DaxStudio target) + Caliburn.Micro (MVVM), WPF, existing DaxStudio.QueryTrace infrastructure

<!-- MANUAL ADDITIONS START -->

## Documentation

Documentation for new features is in the `docs/` folder:
- `docs/VISUAL_QUERY_PLAN_REFERENCE.md` - Comprehensive reference for operators, properties, and terminology
- `docs/VISUAL_QUERY_PLAN_PATTERNS.md` - Regex patterns for parsing operation strings
- `docs/VISUAL_QUERY_PLAN_SOURCES.md` - Source links with summaries

## Build & Run (Python Script - Preferred)

Use `build.py` for all build operations (auto-approved in Claude Code settings):

```bash
python build.py build          # Build the test project
python build.py test           # Run all Visual Query Plan tests
python build.py test <filter>  # Run tests matching filter
python build.py run            # Launch DaxStudio
python build.py rebuild        # Clean rebuild of test project
python build.py restore        # Restore NuGet packages
```

## Build & Run (Manual MSBuild)

**Build the Standalone project (creates full executable with all dependencies):**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src/DaxStudio.Standalone/DaxStudio.Standalone.csproj -t:Build -p:Configuration=Debug -v:minimal -m
```

**Build UI project only (faster, for UI-only changes):**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src/DaxStudio.UI/DaxStudio.UI.csproj -t:Build -p:Configuration=Debug -v:minimal -m
```

**Restore packages first if needed:**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src/DaxStudio.Standalone/DaxStudio.Standalone.csproj -t:Restore -p:Configuration=Debug -v:minimal
```

**Run DaxStudio:** Use `python build.py run` (the PowerShell `Start-Process` command doesn't work from Claude Code's bash environment).

**Executable location:** `src\bin\Debug\DaxStudio.exe`

## Running Tests

**Build and run all VisualQueryPlan tests:**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" tests/DaxStudio.Tests/DaxStudio.Tests.csproj -t:Build -p:Configuration=Debug -v:minimal -m
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" src/bin/Debug/DaxStudio.Tests.dll --Tests:VisualQueryPlan --logger:"console;verbosity=minimal"
```

**Run specific test class (e.g., PerformanceIssueDetector tests):**
```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" src/bin/Debug/DaxStudio.Tests.dll --Tests:PerformanceIssueDetector --logger:"console;verbosity=detailed"
```

## Build Guidelines

- **ALWAYS check build output for errors** before attempting to run the application
- Look for "error" in MSBuild output - warnings are usually OK
- If build fails, fix errors before proceeding
- Build the **Standalone** project when running the app (includes all dependencies)
- Build the **UI** project for faster iteration on UI-only changes

<!-- MANUAL ADDITIONS END -->
