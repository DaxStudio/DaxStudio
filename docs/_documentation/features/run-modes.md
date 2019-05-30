---
title: Run Modes
---

![run modes](run modes_options.png)

### Run
This is the default mode. It will execute the selected query and sent the results to the selected [output](output-modes)

### Clear Cache and Run
In this mode before the query is run a clear cache command is sent to make sure that the query runs on a cold cache. This is most often used when performance tuning and saves having to remember to manually click the Clear Cache button before running a query

### Run Function
Results can be sent directly to a tab separated (.txt) file or to a comma separated (.csv) file

### Run Scalar
When the results are output using the Linked mode a table is created in the active Excel workbook which has the connection to the tabular data source and the DAX query embedded in it. What this means is that users without Dax Studio could refresh the data in this table.

