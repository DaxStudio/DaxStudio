---
title: Connection Dialog
---

The connection dialog in DAX Studio provides the ability to connect to:
* PowerPivot _(only available when starting DAX Studio from the Addins ribbon in Excel)_
* Power BI Desktop / SSDT Integrated Workspaces
* SQL Server Analysis Services servers (running Tabular or Multi-Dimensional)
* Azure Analysis Services
The dialog box only presents connection options that are currently valid.

## Connect to PowerPivot
This option is only available when DAX Studio is launched from the Add-ins ribbon in Excel. If you launch DAX Studio outside of excel it cannot connect to PowerPivot

![](ConnectPowerPivot.png)

## Connect to Power BI Desktop / SSDT Integrated Workspaces
DAX Studio can find any running instances of Power BI Desktop that are running on the local machine and present an option to connect to model inside the pbix file. And if you run SQL Server Data Tools (SSDT) with a model that is using an Integrated Workspace we can connect to that too

![](ConnectAll.png)

> Note: For SSDT we get the name of the model from the Title bar of SSDT. This only works if your Tabular project is the only one in the solution. If you have multiple projects inside the one solution you will see multiple data sources with the same name. Unfortunately at this point in time we have not discovered any way of linking the in-memory engine instances to a specific project inside a solution. 

## Connect to SQL Server Analysis Services servers
DAX Studio can connect to any server running in Tabular or PowerPivot mode and can also connect to Multi-Dimensional servers providing they are running SQL Server 2012 SP1 CU4 or later (versions earlier than this do not understand DAX queries)

![](ConnectServer.png)

## Advanced Options
If you ever need to configure additional connection properties some of the more recent ones are listed under the Advanced Options section, any that are not can be added in the "Additional Options" section

![](ConnectAdvanced.png)