---
title: Output Modes
---

![output modes](output modes_output_targets.png)

### Grid
This is the default mode. Results are displayed in a grid within the Dax Studio Results tab.

### Timer
In this mode the query is run, but the results are discarded. This is mainly useful for performance tuning where you want to measure the speed of two queries, but are not interested in viewing the results

### File
Results can be sent directly to a tab separated (.txt) file or to a comma separated (.csv) file

### Linked (Excel only)
When the results are output using the Linked mode a table is created in the active Excel workbook which has the connection to the tabular data source and the DAX query embedded in it. What this means is that users without Dax Studio could refresh the data in this table.

### Static (Excel only)
This output option simply executes the DAX query and copies the results into the specified sheet in the active Excel worksheet. This is just a static copy of the data which cannot be refreshed. (unlike the Linked output option)