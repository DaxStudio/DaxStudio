---
title: Connection Dialog
---

The connection dialog in DAX Studio provides the ability to connect to:
* PowerPivot
* Power BI Desktop
* SQL Server Analysis Services servers (running Tabular or Multi-Dimensional)
The dialog box only presents connection options that are currently valid.

## Connect to PowerPivot
This option is only available when DAX Studio is launched from the Add-ins ribbon in Excel. If you lauch DAX Studio outside of excel it cannot connect to PowerPivot
![](ConnectPowerPivot.png)

## Connect to Power BI Desktop
DAX Studio can find any running instances of Power BI Desktop that are running on the local machine and present an option to connect to model inside the pbix file.
![](ConnectAll.png)

## Connect to SQL Server Analysis Services servers
DAX Studio can connect to any server running in Tabular or PowerPivot mode and can also connect to Multi-Dimensional servers providing they are running SQL Server 2012 SP1 CU4 or later (versions earlier than this do not understand DAX queries)
![](ConnectServer.png)

## Advanced Options
If you ever need to configure additional connection properties some of the more recent ones are listed under the Advanced Options section, any that are not can be added in the "Additional Options" section
![](ConnectAdvanced.png)