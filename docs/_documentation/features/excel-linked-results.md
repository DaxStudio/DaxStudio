---
title: Excel - Linked Results
---

> This output option is only available when DAX Studio is launched from the Excel Addin

When the results are output using the Linked Excel mode a table is created in the active Excel workbook which has the connection to the tabular data source and the DAX query embedded in it. What this means is that users without Dax Studio could refresh the data in this table.

 **Note:** _using this type of output really only makes sense when the data source is PowerPivot or a Server based connection. When connecting to PowerBI Desktop or and SSDT Integrated Workspace the internal port these 2 data sources use is randomized each time they are opened. This will effectively break the "link" meaning that the [Static Excel](../excel-static-results) output is a better choice for these data sources._