---
title: Query Editor
---
The query editor pane is where you enter the queries that you want DAX Studio to execute.

It has a number of advanced capabilities including:
### Syntax highlighting
![](Query Editor_SyntaxHighlighting.png)
As of v2.4.4 the Syntax highlighting is now dynamic and discovers new keywords and functions from the currently connected data source. This is particularly important for PowerBI where new functionality is regularly released.

### Code Completion support
![](Query Editor_intellisense.png)
The editor can display auto-complete information for [functions, tables and columns](../intellisense-support)

### Function Insight Tooltips
![](Query Editor_FunctionTooltips.png)
DAX Studio displays information about the function including a description and the parameter signature

### Bracket matching
As you type or move around the editor DAX Studio will show you matching brackets
![](Query Editor_BracketMatching.png)

And will highlight in red if it can't find a matching opening or closing bracket
![](Query Editor_MismatchedBrackets.png)
