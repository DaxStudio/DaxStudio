---
name: xaml-string-extraction
description: Step-by-step procedure for extracting hardcoded English strings from a DAX Studio XAML view file and replacing them with {x:Static} resource references. Use when localizing or internationalizing XAML files.
---

# XAML String Extraction for i18n

## Step 1: Add Resource Namespace

If not already present, add to the root element:
```xml
xmlns:loc="clr-namespace:DaxStudio.UI.Resources"
```

## Step 2: Identify Extractable Strings

Scan ALL attributes for hardcoded English text. Target attributes include:
- `Content`, `Text`, `Header`, `Title`, `ToolTip`
- `Watermark`, `Label`, `Description`
- `ScreenTipTitle`, `ScreenTipText` (Fluent.Ribbon)
- `GroupName` (when user-visible)
- `NullValueText`, `EmptyText`, `PlaceholderText`

## Step 3: Generate Resource Key

Follow this naming convention:

| View Context | Pattern | Example |
|-------------|---------|---------|
| RibbonView | `Ribbon_{Purpose}` | `Ribbon_RunQuery` |
| RibbonView tooltip | `Ribbon_{Purpose}_Tooltip` | `Ribbon_RunQuery_Tooltip` |
| Dialog | `{Dialog}_{Purpose}` | `ConnectionDialog_ServerLabel` |
| Tool pane | `{Pane}_{Purpose}` | `MetadataPane_SearchWatermark` |
| Column header | `Column_{Name}` | `Column_Duration` |
| Tab header | `Tab_{Name}` | `Tab_QueryResults` |

## Step 4: Replace String

```xml
<!-- BEFORE -->
<Button Content="Run Query" ToolTip="Execute the current DAX query (F5)"/>

<!-- AFTER -->
<Button Content="{x:Static loc:Strings.Ribbon_RunQuery}"
        ToolTip="{x:Static loc:Strings.Ribbon_RunQuery_Tooltip}"/>
```

## Step 5: Record Resx Entries

For each extraction, record the key and English value to add to `src\DaxStudio.UI\Resources\Strings.resx`:
```
Ribbon_RunQuery = Run Query
Ribbon_RunQuery_Tooltip = Execute the current DAX query (F5)
```

## Exclusion Rules

Do NOT extract:
- Binding expressions: `{Binding ...}`, `{StaticResource ...}`, `{DynamicResource ...}`
- DAX keywords: EVALUATE, CALCULATE, SUMMARIZE, MEASURE, VAR, RETURN
- Technical terms: VertiPaq, Storage Engine, Formula Engine, DirectQuery, xmSQL
- Product names: DAX Studio, Power BI, Analysis Services, Excel, Power Pivot
- Empty strings, whitespace, or numeric-only values
- Icon characters (single glyphs used as icons)
- Keyboard shortcuts: Ctrl+C, F5, Ctrl+Shift+Enter
- `Fluent:KeyTip.Keys` values (keyboard accelerators)
- Converter parameters that are not user-visible
- Design-time data (`d:DesignData`, `d:DataContext`)
