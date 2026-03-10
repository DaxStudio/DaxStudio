---
name: spectre-cli-localization
description: Procedure for localizing Spectre.Console.Cli command and option descriptions in DaxStudio.CommandLine. Use when internationalizing the dscmd command-line tool.
---

# Spectre.Console.Cli Localization for dscmd

## Overview

The `DaxStudio.CommandLine` project uses Spectre.Console.Cli with `[Description]` attributes for help text. There are ~28 `[Description]` attributes across settings classes and ~9 `.WithDescription()` calls in `Program.cs`.

## Step 1: Migrate [Description] Attributes

Replace `System.ComponentModel.DescriptionAttribute` with `LocalizedDescriptionAttribute`:

```csharp
// BEFORE
[CommandOption("-s|--server <server>")]
[Description("The name of the tabular server to connect to")]
public string Server { get; set; }

// AFTER
[CommandOption("-s|--server <server>")]
[LocalizedDescription("Option_Server_Description")]
public string Server { get; set; }
```

`LocalizedDescriptionAttribute` extends `DescriptionAttribute` and overrides `Description` to return `CommandStrings.ResourceManager.GetString(key)`. Spectre.Console.Cli reads `DescriptionAttribute.Description` transparently.

## Step 2: Migrate .WithDescription() Calls

In `Program.cs`, these are method calls that accept a string — use the resource directly:

```csharp
// BEFORE
export.AddCommand<ExportCsvCommand>("csv")
    .WithDescription("Exports specified tables to csv files in a folder");

// AFTER
export.AddCommand<ExportCsvCommand>("csv")
    .WithDescription(CommandStrings.Command_ExportCsv_Description);
```

## Step 3: Update DirectLakeModeDescriptionAttribute

```csharp
// BEFORE
public override string Description => "Sets the Direct Lake mode. Valid values are: "
    + string.Join(", ", Enum.GetNames(typeof(DirectLakeExtractionMode)));

// AFTER
public override string Description => string.Format(
    CommandStrings.Option_DirectLakeMode_DescriptionFormat,
    string.Join(", ", Enum.GetNames(typeof(DirectLakeExtractionMode))));
```

**CommandStrings.resx:** `Option_DirectLakeMode_DescriptionFormat = Sets the Direct Lake mode. Valid values are: {0}`

## Step 4: Update CustomHelpProvider

Localize the hardcoded text in `GetHeader()`:
```csharp
new Markup("[dim]DAX Studio command line utility[/]")
// → new Markup($"[dim]{CommandStrings.Help_ToolDescription}[/]")
```

## Resource Key Naming

| Context | Pattern | Example |
|---------|---------|---------|
| Command | `Command_{Name}_Description` | `Command_ExportCsv_Description` |
| Option | `Option_{Name}_Description` | `Option_Server_Description` |
| Argument | `Argument_{Name}_Description` | `Argument_OutputFolder_Description` |
| Help text | `Help_{Purpose}` | `Help_ToolDescription` |

## Do NOT Localize

- `[CommandOption("-s|--server <server>")]` — CLI flags are part of the API contract
- Command verb names: `csv`, `file`, `xlsx`, `vpax`, `export`, `accesstoken`
- `[CommandArgument(0, "<OutputFolder>")]` placeholder syntax
- `.WithExample(...)` arrays — these are CLI usage examples showing exact syntax

## Files to Process (in order)

1. `Commands\CommandSettingsRawBase.cs` — shared connection options (5 descriptions)
2. `Commands\CommandSettingsFolderBase.cs` — output folder argument (1 description)
3. `Commands\ExportSqlCommand.cs` — SQL export options (3 descriptions)
4. `Commands\ExportCsvCommand.cs` — CSV export options (2 descriptions)
5. `Commands\ExportParquetCommand.cs` — Parquet export options (2 descriptions)
6. `Commands\FileCommand.cs` — file output options (3 descriptions)
7. `Commands\VpaxCommand.cs` — VPAX options (6 descriptions)
8. `Commands\XlsxCommand.cs` — XLSX options (2 descriptions)
9. `Commands\AccessTokenCommand.cs` — token options
10. `Commands\CustomTraceCommand.cs` — trace options (2 descriptions)
11. `Commands\CaptureDiagnosticsCommand.cs` — diagnostics options (2 descriptions)
12. `Program.cs` — command registrations (9 `.WithDescription()` calls)
13. `Help\CustomHelpProvider.cs` — help header text
14. `Attributes\DirectLakeModeDescriptionAttribute.cs` — dynamic description
