---
title: Intellisense Support
---
Dax Studio provides full intellisense support

> **NOTE:** Currently DAX Studio does not support variables in intellisense

The intellisense is based on the best practice of always prefixing columns with the table name and never prefixing a measure with a table name. So if you type _'table name'[_  you will get intellisense for all the columns in the ‘table name’ table. But if you just type _[_ you will be presented with a list of all the measures in the model.

It will list both Functions and Keywords:

![](Intellisense Support_intellisense_functions.png)

Tables:

![](Intellisense Support_intellisense_tables.png)

Columns:

![](Intellisense Support_intellisense_columns.png)

and measures:

![](Intellisense Support_intellisense_measures.png)