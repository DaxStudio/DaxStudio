# DAX Studio – Comment Script

DAX Studio Comment Script is a set of commands embedded in comments (so that other tools can still run the .dax files without the commands interfering)

These will work as pre-processor directives so that before executing a .dax file the pre-processor will split the document into one or more “blocks” and each block will 0 or many commands, 1 DAX statement and 0 or 1 XMLA parameter blocks. 
The execution engine will then loop through all the blocks executing them one after the other. It will first process any commands, then execute the DAX. Internally the XMLA <parameters> blocks are interpreted as --> PARAMETER commands so you can have different formats in different blocks or even mix and match them (although I expect most people will prefer the new command syntax as its more concise)
The pre-processor currently does not understand much about DAX other than the basic structure of things like string literals, quoted table names and measure/column names. But one thing it does understand is the RSCustomDaxFilter() function. It can either take a parameter and expand it into proper DAX or it can insert a comment starting with --~ so that we can send the text to daxformatter.com and then just replace the --~ comments with an empty string once it comes back
# Commands

## Connect
```
--> CONNECT [SERVER|PBIX|SSDT] <servername/filename>
```

This would be used to bypass the connection dialog. I’m thinking it might make sense to process the commands in the first block when opening a file to see if it has a connect command and then execute that. This command should check if it is already connected to the specified source.
For PBIX / SSDT we can only pass the part of the file that appears in the title bar of the app. I had a look, but it’s too hard to figure out which exact pbix file in which folder the user has open. You need a kernel level driver to find out that level of detail from another process and that is beyond me (and may require running as admin)

For a PBIX connection the argument can also be a full path to a `.pbix` file, for example `--> CONNECT PBIX "C:\reports\Sales.pbix"`. In that case DAX Studio matches a running Power BI Desktop instance by the file name without its extension (e.g. `Sales`); if no matching instance is running it will open the file in Power BI Desktop, wait for it to finish loading, and then connect. This replaces the earlier standalone OPEN command.

## Use
```
--> USE <databasename>
```

This would work the same as changing the database from the dropdown in the metadata pane

## Parameter
```
--> PARAMETER @<parametername> [INTEGER|DOUBLE|DATETIME|STRING|BOOLEAN] = <value>
```

This is a way of setting a parameter value for a query to avoid being prompted every time.
For RSCustomDaxFilter() I’m thinking that maybe the user must specify an array of values. 
Eg. {1,2,3}  or {“Red”, “Green”, “White”}

We could possibly also just set the parameter type this way:
```
--> PARAMETER @Size INTEGER
```

## Variables

```
--> SET <name> = <value>
```

Defines a reusable script variable that can be substituted into later command arguments (most
usefully dynamic file paths for CI/CD). The name is a bare identifier (letters, digits, underscore;
case-insensitive) with **no** leading `@` — that prefix is reserved for query parameters. The value
may be a quoted string, a bare identifier, an integer, or a real number.

Use a variable with the `$(name)` syntax inside a command argument. The parentheses give explicit
boundaries so names concatenate safely inside paths:

```
--> SET OutDir = "C:\Reports"
--> SET Env = prod
--> METRICS EXPORT "$(OutDir)\metrics-$(Env).vpax"
```

### Built-in variables

The same `$(...)` syntax exposes built-in namespaces:

| Reference | Expands to |
|-----------|-----------|
| `$(now:<fmt>)` | Local current time formatted with the .NET format string `<fmt>`, e.g. `$(now:yyyy-MM-dd)` |
| `$(utcnow:<fmt>)` | UTC current time, same formatting |
| `$(env:<VAR>)` | The value of the `<VAR>` environment variable, e.g. `$(env:BUILD_ID)` |

### Capturing a built-in in a variable

A `SET` value may itself contain `$(...)` references — other variables **and** built-ins. These are
expanded **eagerly, at the moment the `SET` executes**, and the resolved literal is stored. This
freezes a captured timestamp so it stays constant for the rest of the run:

```
--> SET OutDir = "C:\Report\$(now:yyyy-MM-dd)"
--> METRICS EXPORT "$(OutDir)\model.vpax"
EVALUATE ...
--> GO
--> ASSERT TABLE CSV "$(OutDir)\baseline.csv"
EVALUATE 'Product'
```

Here `$(OutDir)` yields the same dated folder in both batches, even minutes apart. Because expansion
is eager, a variable can only reference names defined above it.

### Semantics and errors

- **Where it applies:** command string arguments — `ASSERT TABLE <file>`, `METRICS EXPORT <path>`,
  `CONNECT` targets, and `USE <database>`. The DAX query body is not expanded.
- **Ordering:** a `SET` is visible only to commands that follow it (including across `--> GO`).
- **Escaping:** write `$$(` to emit a literal `$(`; every other `$` is literal.
- **Undefined variable / unknown namespace / bad date format:** a hard error that fails the run.

## Output
```
--> OUTPUT [CSV|EXCEL|EXCEL-LINKED|GRID]
```

This will work the same as changing the output option in ribbon
```
--> OUTPUT-FOLDER <path>
```

This will allow the setting of a default output folder when using one of the outputs that generates files.
```
--> OUTPUT-FILE <filename>
```

This sets the file name to use when outputting a file. I’m thinking that this could be either a full path or just the name and extension (eg. Products.csv )
```
--> EXCEL-SHEET <sheetname>
```

## Traces
This will let the user override the name of the sheet in excel to write the query to. Maybe if the filename is the same (or not specified on the second block) then I could output each query to it’s own sheet. I’m not sure, but this might be possible.
Traces
```
--> TRACE SERVERTIMINGS [ON|OFF]
--> TRACE QUERYPLAN [ON|OFF]
--> TRACE ALLQUERIES [ON|OFF]
```
## Go
```
--> GO
```

This would just be a batch separator if there are no other commands that need to be run. This way you could run 2 or more DAX queries with different DEFINE statements.

## Clear Cache
```
--> CLEARCACHE
```

This would work the same as clicking the clear cache button in the ribbon

## Save As
```
--> SAVEAS "<filename>"
```

Saves a snapshot of the current query to `<filename>` **after** the query has run, without changing
the identity of the open document (its tab name / dirty state are left untouched). The path must be
quoted when it contains a drive letter or backslashes (e.g. `"C:\Reports\products.daxx"`).

The extension controls the format, mirroring the ribbon's Save As:

* `.daxx` &mdash; a full DAX Studio package containing the query text plus the visible trace watchers
  and any `--> SHOW` output. When Server Timings is active the server-timing data is captured too, so
  a `.daxx` snapshot doubles as a performance record.
* any other extension (e.g. `.dax`, `.txt`) &mdash; just the query text.

Because it runs after the query (and, for `.daxx`, after the Server Timings trace finishes
aggregating), the file reflects the fully-executed script. `$(...)` variable / built-in references are
expanded in the path, which is the intended way to add a timestamp for CI/CD runs:

```
--> SET OutDir = "C:\Reports"
--> SAVEAS "$(OutDir)\products-$(now:yyyy-MM-dd).daxx"
```

In the `dscmd` command line, a `.dax` target writes the query text and a `.daxx` target writes a
package that also embeds Server Timings when the script contains `--> TRACE SERVERTIMINGS ON`.

## Metrics
```
--> METRICS EXPORT <filename>
--> METRICS VIEW
```

This would be the same as clicking view or export metrics in the ribbon. More useful with a command line option.

## Show

```
--> SHOW DEPENDENCIES
--> SHOW LAST_UPDATED
--> SHOW MAX_UPDATED
```

The `SHOW` command renders a tree-grid in the Results pane instead of running the query. It is
mutually exclusive with the normal results grid for that run and can be dismissed with the *Hide*
button, revealing the last query results again.

| Sub-command | Behaviour |
|---|---|
| `SHOW DEPENDENCIES` | Analyses the DAX query in the batch (**without executing it**) and displays the full recursive dependency tree of every referenced object – measures, columns, tables and functions. Uses `DISCOVER_CALC_DEPENDENCY`. |
| `SHOW LAST_UPDATED` | Ignores the query and displays the model metadata as a tree that mirrors the Power BI Desktop model view: a single **Semantic model** root with grouping folders (*Calculation groups*, *Cultures*, *Expressions*, *Functions*, *Perspectives*, *Relationships*, *Roles*, *Tables*) and, under each table, *Calendars*, *Columns*, *Hierarchies*, *Measures* and *Partitions*. Each item shows its last schema-modified timestamp, sourced from the `TMSCHEMA_*` DMVs. |
| `SHOW MAX_UPDATED` | Same metadata source and structure as `LAST_UPDATED`, but pruned to the object(s) carrying the **maximum** modified timestamp (nested inside their enclosing folders / tables) so you can quickly see what was changed most recently. |

The tree-grid shows an *Object*, *Type* and *Table* column for every variant; the *Last Modified*
column is shown for `LAST_UPDATED` / `MAX_UPDATED`, and two additional columns are shown for
`LAST_UPDATED` only: *Max Update* (the most-recent change among a row's descendants) and *Days Since
Change* (whole days since the row's effective most-recent change). Where a DMV exposes both a
*ModifiedTime* and a *StructureModifiedTime* the structure-modified value is used. The *Functions* and
*Calendars* groups are newer features and are omitted when the connected model does not support the
corresponding DMVs. All engines DAX Studio connects to are XMLA-capable, so the core DMVs are
available; permission or empty-result errors on individual DMVs are logged and that group is skipped.

> **Tip:** `SHOW LAST_UPDATED` and `SHOW MAX_UPDATED` can also be run without typing a command –
> right-click the database in the Metadata pane and choose **Show Last Updated** or **Show Max
> Updated**. These menu items produce the same timestamp tree-grid as the corresponding commands.

## Assert
```
--> ASSERT ROWCOUNT [>|>=|<|<=|=] <value>
--> ASSERT DURATION [>|>=|<|<=|=] <value>
--> ASSERT SE_CPU [>|>=|<|<=|=] <value>
--> ASSERT SE_QUERIES [>|>=|<|<=|=] <value>
```

These assertions run the query in the batch and check a scalar property of the run: the number of
rows returned (`ROWCOUNT`) or a Server Timings metric (`DURATION`, `SE_CPU`, `SE_QUERIES`). To assert
the actual result set — including a single value or a single column — use `ASSERT TABLE` (see below),
which compares the query output against inline expected rows or a baseline `CSV/TXT/MD/PARQUET` file.
Assertions are especially useful in a command-line scenario where batches of these can be run.

## Assert Table
```
--> ASSERT TABLE
-->> | <column1> | <column2> | ... |
-->> |-----------|-----------|-----|
-->> | <value1>  | <value2>  | ... |
-->> | <value3>  | <value4>  | ... |
```

`ASSERT TABLE` compares the query output against an expected table. The expected data can be provided
inline with `-->>` continuation rows (shown above), keeping it co-located with the query, or loaded from
a baseline file (see *Loading expected data from a file* below). Each continuation line uses the `-->>` prefix; since every
line starts with `--`, the table data remains a valid DAX comment and will be ignored by non-DAX Studio
tools. A single-column expected table asserts one column, and a single-cell (1×1) table asserts a single
value.

The first `-->>` row defines the column headers (which must match the query output column names, case-insensitive). The separator line (`|---|---|`) is optional but recommended for readability. Subsequent `-->>` rows define the expected data.

**Variants:**
```
--> ASSERT TABLE                 // row order must match query output
--> ASSERT TABLE UNORDERED       // same rows required, any order
--> ASSERT TABLE PARTIAL         // query output must contain these rows (extra rows allowed)
```

**Loading expected data from a file.** Instead of inline `-->>` rows, the expected table can be read
from a baseline file. Add a file-format keyword and a quoted path after the (optional) mode:
```
--> ASSERT TABLE CSV "expected/products.csv"
--> ASSERT TABLE UNORDERED PARQUET "expected/products.parquet"
```
Supported file formats: `CSV`, `TXT` (tab-delimited), `MD` (markdown table), `PARQUET`. This replaces
the earlier `ASSERT RESULTS <csv file>` idea — baseline files can be generated by a previous script or
by another tool and checked in a command-line / CI-CD run.

**Example:**
```
--> ASSERT TABLE
-->> | Color | ProductCount |
-->> |-------|--------------|
-->> | Red   | 5            |
-->> | Blue  | 3            |

EVALUATE
SUMMARIZECOLUMNS(
    'Product'[Color],
    "ProductCount", COUNTROWS('Product')
)
ORDER BY 'Product'[Color]
```

**Empty / null values.** An empty cell (nothing between the `|` delimiters) represents a
**null / DAX BLANK** in *every* column type: `-->> | Red | |`. To assert an explicit **empty
string** (rather than null) in a text column, use the `""` token: `-->> | Red | "" |`.

A leading backslash **escapes** a cell so the remainder is treated as a literal string, which is
how you assert a value that would otherwise be read as a token:

| Cell    | Asserted value        |
|---------|-----------------------|
| (empty) | null / BLANK          |
| `""`    | empty string          |
| `\""`   | the literal text `""` |
| `\"`    | the literal text `"`  |
| `\\x`   | the literal text `\x` |

**Column types** are inferred from the values by default (integers, decimals, booleans, dates, strings). To explicitly control types, add an optional **type row** immediately after the header row. If every cell in the second `-->>` row is a recognized DAX type name, it is treated as a type declaration (not data).

Recognized type names: `STRING` (or `TEXT`), `INT64` (or `INTEGER`, `INT`), `DOUBLE`, `CURRENCY` (or `DECIMAL`), `BOOLEAN` (or `BOOL`), `DATETIME` (or `DATE`)

**Example with explicit types:**
```
--> ASSERT TABLE
-->> | Product | Price    | OrderDate |
-->> | STRING  | CURRENCY | DATETIME  |
-->> |---------|----------|-----------|
-->> | Widget  | 19.99    | 2024-01-15|
-->> | Gadget  | 9.99     | 2024-06-30|
```

This is particularly useful for distinguishing `DOUBLE` from `CURRENCY` (both look like decimal numbers), or ensuring a column of numbers is kept as `STRING`.

## Other ideas:
•	Passing a folder on the command line could be an easy way to run a batch of these
•	Maybe the command line could support an /Assert parameter to do a similar thing
•	Potentially the Verify https://github.com/VerifyTests/Verify library could be helpful for doing file based comparison testing.

