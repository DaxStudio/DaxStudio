---
name: build-and-test
description: Build DAX Studio and run tests. Use when you need to verify changes compile correctly and all tests pass.
---

# Build and Test DAX Studio

## Prerequisites

- Close any running `DaxStudio.exe` process before building — it locks DLLs in `src\bin\Debug\` and causes MSBuild copy failures.

## Build

```
msbuild src\DaxStudio.sln /p:Configuration=Debug /restore
```

If `msbuild` is not on `PATH`, use `vswhere` to find MSBuild in the latest Visual Studio installation. Do not hard-code a Visual Studio year or edition:

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild src\DaxStudio.sln /p:Configuration=Debug /restore
```

Visual Studio 2026 or later is supported.

## Test

```
vstest.console src\bin\Debug\DaxStudio.Tests.dll
```

Or for a single test:
```
vstest.console src\bin\Debug\DaxStudio.Tests.dll /Tests:TestMethodName
```

## Old-Style csproj Reminders

DaxStudio.UI and several other projects use **old-style `.csproj`** files (not SDK-style). When adding files:

- **New `.cs` file**: Add a `<Compile Include="Path\To\File.cs" />` entry to the `.csproj`
- **New resource file**: Add an `<EmbeddedResource Include="Path\To\File.resx" />` entry
- **New `.resx` with designer**: Also add a `<Compile Include="Path\To\File.Designer.cs" />` with `<DependentUpon>File.resx</DependentUpon>`

Forgetting these entries means the file compiles locally in Visual Studio (which auto-includes) but fails on the build server.

## Common Build Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `Unable to copy file ... being used by another process` | DaxStudio.exe is running | Close DaxStudio before building |
| `The type or namespace 'X' could not be found` | Missing `<Compile Include>` in `.csproj` | Add the file reference to the project file |
| `Resource file not found` | Missing `<EmbeddedResource Include>` | Add the resource reference to the project file |
