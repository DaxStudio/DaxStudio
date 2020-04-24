---
title: Export Data
---

This feature gives you the ability to export entire tables from your data model to either CSV files or to SQL Server

> **Note:** DAX Studio uses a stream architecture to write out the data as it arrives, so it rarely consumes more than a few hundred Mb while exporting. Typically any "Out of Memory" errors while exporting are due to the fact that the Tabular engine attempts to materialize the **entire** result set in memory before returning it to DAX Studio.

