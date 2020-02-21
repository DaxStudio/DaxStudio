---
title: DAX Studio Ribbon
---

The ribbon in DAX Studio is split into a number of functional groups

![](Ribbon Control_HomeRibbon.png)

## Query

- **Run** - this button can be set to 2 modes, one that simply runs the query and another that clears the cache and runs the query as a single action (useful for performance tuning)
- **Cancel** - this buttion will send a cancel command to the data source for a running query
- **Clear Cache** - this command is only available if you have admin rights on the data source and will sent a ClearCache command to the data source for the current database
- **Output** - this option lets you choose one of the [output modes](../output-modes)

## Edit 
- **Cut / Copy / Paste** - buttons for standard edit operations, the standard hotkeys for these commands also work
- Undo / Redo -
## Format
- **Format Query** - this button will send the contents of the query editor (or the current selection) to [https://daxformatter.com](https://DaxFormatter.com) for [formatting](../daxformatter-support)
- **To Upper** - will convert the current selection to uppercase
- **To Lower** - will convert the current selection to lowercase
- **Swap Delimiters** - will convert the current selection between delimiter styles for lists & decimals
- **Comment** - will prefix the lines of the current selection with comment markers
- **Uncomment** - will remove the comment prefixes from the currently selected lines
- **Merge XML** - will look for a parameters XML block and merge the values into the current query text

## Find
- **Find** - will [find](../find-replace) the specified text in the query
- **Replace** - will allow [replacing](../find-replace) of specified text values

## Traces
- **Query Plan** - turns on the display of query plan information (requires admin rights on the data source)
- **Server Timings** - turns on the display of detailed timing information (requires admin rights on the data source)
- **All Queries** - traces all queries against the given data source. This lets you capture queries from other client tools like Excel or Power BI in order to assist in tuning them or learning about how a particular client tool constructs it's queries

## Server Timings
- **Scan** - displays information on storage engine scan events
- **Cache** - displays information on storage engine cache events
- **Internal** - displays information on storage engine internal events 
- "Right Layout/Bottom Layout* - Controls where the timing details pane appears
## Connect
- **Connect** - opens a [connection dialog](../connection-dialog) so that the user can change the connection for the current query window
- **Refresh Metadata** - will update the metadata of the currently selected model