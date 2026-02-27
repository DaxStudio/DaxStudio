# Copilot Instructions for DAX Studio

## What is DAX Studio?

DAX Studio is a WPF desktop application for writing and executing DAX queries against Microsoft Analysis Services tabular models, Power BI, and Power Pivot. It ships as both a standalone `.exe` and an Excel add-in. A command-line tool (`dscmd`) is also available.

## Build & Test

- **Solution:** `src\DaxStudio.sln` — open in Visual Studio 2022.
- **Target framework:** .NET Framework 4.7.2, C# 8.0.
- **Restore:** NuGet restore pulls all dependencies. No additional setup is required.
- **Build (VS):** Build the solution normally in Visual Studio. The `DaxStudio.Standalone` project is the default startup project for debugging.
- **Build (CLI):** `msbuild src\DaxStudio.sln /p:Configuration=Debug /restore`
- **Tests:** MSTest framework with NSubstitute for mocking.
  - Run all tests: `vstest.console src\bin\Debug\DaxStudio.Tests.dll` or use Test Explorer in VS.
  - Run a single test: `vstest.console src\bin\Debug\DaxStudio.Tests.dll /Tests:TestMethodName`
  - Some tests require a local SSAS instance and may be skipped in CI.
- **Installer:** Built with Inno Setup 6 via `build.msproj` MSBuild targets.

## Architecture

### Entry Points

| Project | Executable | Purpose |
|---|---|---|
| `DaxStudio.Standalone` | `DaxStudio.exe` | Standalone WPF app. Entry point: `EntryPoint.Main()` |
| `DaxStudio.ExcelAddin` | (VSTO add-in) | Excel add-in for PowerPivot connectivity |
| `DaxStudio.CommandLine` | `dscmd.exe` | Command-line interface for batch/automated DAX operations |
| `DaxStudio.Checker` | `DaxStudio.Checker.exe` | Standalone diagnostic tool for end-user environment checks |

### MVVM with Caliburn.Micro + MEF

The UI layer (`DaxStudio.UI`) uses **Caliburn.Micro** for MVVM and **MEF** (`System.ComponentModel.Composition`) as the IoC container.

- **View/ViewModel binding:** Caliburn.Micro's naming convention auto-wires `Views\FooView.xaml` to `ViewModels\FooViewModel.cs`. Always follow this naming pattern.
- **Dependency injection:** Use `[Export]` and `[Import]` attributes. Services are registered in `AppBootstrapper.cs` via `CompositionContainer`.
- **Messaging:** Use `IEventAggregator` for pub/sub communication between ViewModels. Event classes live in `DaxStudio.UI\Events\`.
- **ViewModels** inherit from Caliburn.Micro's `Screen` (for documents/dialogs) or `PropertyChangedBase`.

### Startup Flow

`EntryPoint.Main()` → creates `AppBootstrapper` → MEF composes → `ShellViewModel` → `RibbonViewModel` + `StatusBarViewModel` → `DocumentTabViewModel` → `DocumentViewModel` (core editing surface) → tool panes (Metadata, Output, QueryResults, etc.) → `ConnectionDialogViewModel`.

### Key Libraries

- **ADOTabular** (`src\ADOTabular`): Wrapper over ADOMD providing a tabular abstraction (Models → Tables → Columns) over Analysis Services metadata.
- **DAXEditor** (`src\DAXEditor`): Customized AvalonEdit control for DAX editing with syntax highlighting and intellisense.
- **QueryTrace** (`src\DaxStudio.QueryTrace`): Engine for capturing Analysis Services trace events (server timings, query plans, etc.).
- **Serilog** for logging, **Newtonsoft.Json** for serialization, **Fluent.Ribbon** for the ribbon UI.

### Project Dependency Layers

```
DaxStudio.Interfaces  (shared contracts)
       ↑
DaxStudio.Common      (shared utilities)
       ↑
ADOTabular / QueryTrace / DAXEditor  (domain libraries)
       ↑
DaxStudio.UI          (all UI: ViewModels, Views, Models, Events)
       ↑
DaxStudio.Standalone / ExcelAddin / CommandLine  (entry points)
```

## Key Conventions

- **Logging:** Use Serilog's structured logging with `Constants.LogMessageTemplate` (`"{class} {method} {message}"`). Example: `Log.Information(Constants.LogMessageTemplate, nameof(MyClass), nameof(MyMethod), "description")`.
- **Branching:** Feature branches should be created off `develop`. The `master` branch contains the last stable release only.
- **Build output:** Debug builds output to `src\bin\Debug\`; Release builds output to `Release\bin\`.
- **Versioning:** Assembly version is in `src\CommonAssemblyVersion.cs` (auto-incremented on CI). Release version tracked in `src\CurrentReleaseVersion.json`.
- **Custom conventions:** Caliburn.Micro conventions are extended in `DaxStudio.UI\Conventions\` for AvalonDock and ModernWpf integration.
