---
name: csharp-string-extraction
description: Step-by-step procedure for extracting hardcoded English strings from DAX Studio C# files and replacing them with resource references. Use when localizing ViewModels, services, or other C# code.
---

# C# String Extraction for i18n

## Step 1: Add Using Statement

```csharp
using DaxStudio.UI.Resources;
```

## Step 2: Classify Strings

For each string literal in the file, determine if it is **user-facing**:

### Extract (user-facing):
- Messages displayed in message boxes, dialogs, or output pane
- Status bar text
- Notification text
- Error messages shown to the user
- Tooltip text set in code-behind
- Window/dialog titles set in code

### Do NOT Extract:
- Serilog log templates: `Log.Information(Constants.LogMessageTemplate, ...)`
- Internal exception messages not shown to the user
- Dictionary keys, property names, column names
- DAX query text or templates
- File paths, URLs, connection string keys
- Enum names used as identifiers
- XML/JSON element names
- Regex patterns
- Constants used as technical identifiers

## Step 3: Generate Resource Key

| Context | Pattern | Example |
|---------|---------|---------|
| ViewModel message | `{ShortName}_{Purpose}` | `Document_QueryCancelled` |
| Error message | `Error_{Context}_{Purpose}` | `Error_Connection_Timeout` |
| Status message | `Status_{Purpose}` | `Status_QueryRunning` |
| Format string | Append `Format` | `Export_RowsExportedFormat` |

## Step 4: Replace Strings

### Simple string:
```csharp
// BEFORE
ShowMessage("Query was cancelled by the user");

// AFTER
ShowMessage(Strings.Document_QueryCancelled);
```

### Interpolated string:
```csharp
// BEFORE
OutputMessage($"Exported {rowCount} rows to {fileName}");

// AFTER
OutputMessage(string.Format(Strings.Export_RowsExportedFormat, rowCount, fileName));
```

**Strings.resx:** `Export_RowsExportedFormat = Exported {0} rows to {1}`

### Conditional string:
```csharp
// BEFORE
var msg = isConnected ? "Connected" : "Disconnected";

// AFTER
var msg = isConnected ? Strings.Status_Connected : Strings.Status_Disconnected;
```

### String concatenation:
```csharp
// BEFORE
var msg = "Error connecting to " + serverName + ": " + ex.Message;

// AFTER
var msg = string.Format(Strings.Error_Connection_DetailFormat, serverName, ex.Message);
```

**Strings.resx:** `Error_Connection_DetailFormat = Error connecting to {0}: {1}`

## Step 5: Record Resx Entries

Record all key-value pairs to add to `src\DaxStudio.UI\Resources\Strings.resx`.

## Important Notes

- Ensure `{0}`, `{1}` placeholder ordering works when translated — some languages reorder subjects and objects.
- When a string appears in both a log call AND a user display, extract it and use the resource for the display, keep the log template as-is.
- Watch for `[Description]` and `[DisplayName]` attributes — those are handled by the `localized-attribute-migration` skill.
