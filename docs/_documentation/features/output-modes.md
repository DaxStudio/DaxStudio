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

### Clipboard
Results will be sent to the Windows clipboard in a csv data format suitable for pasting into an application like Excel.

### Linked
When the results are output using the Linked mode a table is created in the active Excel workbook which has the connection to the tabular data source and the DAX query embedded in it. What this means is that users without Dax Studio could refresh the data in this table.

If you are running from the **Excel Add-in** you will be given a choice of which sheet in the active workbook the results will appear in.

If you are running DAX Studio outside of Excel we generate a .odc file with your selected query and this will open in a new Excel document.

> Note if you get an error or do not see any results using the **Linked Excel** output trying using the **Grid** output target to test your query to make sure that it works as expected. 

### Static
This output option simply executes the DAX query and copies the results into the specified sheet in the active Excel worksheet. This is just a static copy of the data which cannot be refreshed. (unlike the Linked output option)

If you are running from the **Excel Add-in** you will be given a choice of which sheet in the active workbook the results will appear in.

If you are running outside of Excel you will be prompted for a file name and a new xlsx file will be generated with one or more sheets (depending on how many queries are part of the current batch)