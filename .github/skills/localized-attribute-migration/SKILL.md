---
name: localized-attribute-migration
description: Procedure for migrating DAX Studio OptionsViewModel attributes ([DisplayName], [Description], [Category], [Subcategory]) to their localized equivalents. Use when localizing the Options UI.
---

# Localized Attribute Migration for OptionsViewModel

## Background

DAX Studio's Options UI is built by `PropertyList.cs` which reads attributes via reflection:
- `DisplayNameAttribute` → option label
- `DescriptionAttribute` → help text below option
- `CategoryAttribute` → tab grouping
- `SubcategoryAttribute` → sub-grouping within tab

These standard attributes require compile-time constant strings. To localize them, we use custom subclasses that accept a resource key and override the virtual property to return a localized value at runtime.

## Localized Attribute Classes

Located in `DaxStudio.Common`:
- `LocalizedDisplayNameAttribute` extends `DisplayNameAttribute`
- `LocalizedDescriptionAttribute` extends `DescriptionAttribute`
- `LocalizedCategoryAttribute` extends `CategoryAttribute`
- `LocalizedSubcategoryAttribute` extends `SubcategoryAttribute`

## Migration Procedure

### Step 1: Replace Attributes

For each property in `OptionsViewModel.cs`:

```csharp
// BEFORE
[Category("Editor")]
[DisplayName("Editor Font Size")]
[Description("The font size used for the DAX editor")]
[Subcategory("Formatting")]
[SortOrder(20)]
[MinValue(6), MaxValue(120)]
[DataMember, DefaultValue(11d)]
public double EditorFontSize { get; set; }

// AFTER
[LocalizedCategory("Category_Editor")]
[LocalizedDisplayName("Options_EditorFontSize_DisplayName")]
[LocalizedDescription("Options_EditorFontSize_Description")]
[LocalizedSubcategory("Options_Formatting_Subcategory")]
[SortOrder(20)]
[MinValue(6), MaxValue(120)]
[DataMember, DefaultValue(11d)]
public double EditorFontSize { get; set; }
```

### Step 2: Resource Key Naming

| Attribute | Pattern | Example |
|-----------|---------|---------|
| Category | `Category_{Name}` | `Category_Editor` |
| DisplayName | `Options_{PropertyName}_DisplayName` | `Options_EditorFontSize_DisplayName` |
| Description | `Options_{PropertyName}_Description` | `Options_EditorFontSize_Description` |
| Subcategory | `Options_{Name}_Subcategory` | `Options_Formatting_Subcategory` |

### Step 3: Add Resx Entries

Add each key-value pair to `src\DaxStudio.UI\Resources\Strings.resx` with the original English text as the value.

## Known Categories

These are the existing category values to create resource keys for:
- Editor, Results, Trace, Server Timings, Proxy, Query History
- VertiPaq Analyzer, Timeouts, Defaults, Sounds, DAX Formatter
- Metadata Pane, Custom Export Format, Preview, Privacy, Logging

## Known Subcategories

- Grid, Tooltips, Double-Click, Defaults, Theme, Excel File
- Detect Metadata Changes, Hidden Objects, Scrollbars
- Preview Data, Sorting, Benchmark

## Important Notes

- Do NOT change `[SortOrder]`, `[MinValue]`, `[MaxValue]`, `[DataMember]`, `[DefaultValue]` — these are not user-visible text.
- Do NOT change `[EnumDisplay]` or `[EnvironmentVariableAttribute]` — these control behavior, not display text.
- Properties without a `[DisplayName]` attribute are intentionally hidden from the UI — do not add one.
- `PropertyList.cs` should require ZERO changes — the localized attributes extend the standard ones, so `GetCustomAttribute(typeof(DisplayNameAttribute))` still finds them.
