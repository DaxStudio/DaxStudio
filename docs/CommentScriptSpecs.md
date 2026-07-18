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
--> SAVEAS <filename>
```

This would work the same as the Save As option in the ribbon. I probably need some way of adding timestamp information. This probably makes more sense if I add a command line option. So if you run a file with something like --> SAVEAS “products-%yyyy-MM-dd%.dax” if you had server timings on you could save a file with the timings as part of a CI/CD script.

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

## Assert
```
--> ASSERT VALUE <value>
--> ASSERT COLUMN <column name> = <value>
--> ASSERT ROWCOUNT [>|>=|<|<=|=] <value>
--> ASSERT DURATION [>|>=|<|<=|=] <value>
--> ASSERT SE_CPU [>|>=|<|<=|=] <value>
--> ASSERT SE_QUERIES [>|>=|<|<=|=] <value>
--> ASSERT RESULTS <csv file>
```

The idea here is that the query following the assertion would be run and the results would be checked to ensure that they either match the contents of the file specified in <filename>. These files could be generated by a previous script that generated a base line or maybe they could be generated using SQL queries or some other tool against the source data.
In the case of ASSERT VALUE the results are checked to ensure that it is a single row with a single column and it matches the value specified in <value>. 
The idea of assertions could be useful in a command line scenario where batches of these could be run

## Assert Table
```
--> ASSERT TABLE
-->> | <column1> | <column2> | ... |
-->> |-----------|-----------|-----|
-->> | <value1>  | <value2>  | ... |
-->> | <value3>  | <value4>  | ... |
```

This is an inline alternative to `ASSERT RESULTS <csv file>` that keeps the expected data co-located with the query. Each continuation line uses the `-->>` prefix (which is already used by the LOOP command for multi-line constructs). Since every line starts with `--`, the table data remains a valid DAX comment and will be ignored by non-DAX Studio tools.

The first `-->>` row defines the column headers (which must match the query output column names, case-insensitive). The separator line (`|---|---|`) is optional but recommended for readability. Subsequent `-->>` rows define the expected data.

**Variants:**
```
--> ASSERT TABLE                 // row order must match query output
--> ASSERT TABLE UNORDERED       // same rows required, any order
--> ASSERT TABLE PARTIAL         // query output must contain these rows (extra rows allowed)
```

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

**Empty/null values** are represented by an empty cell: `-->> | Red | |`

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

