---
title: Options Winodw
---

DAX Studio offers a number of options which users can use to customize their experience.

## Editor

These settings control the display and behaviour of the main editor window

| **Setting**       | **Description** | **Default** |
| --- | --- | --- |
| Font Family       | Sets the font used by the Query Editor pane | Lucida Console |
| Font Size         | Sets the default font size (in points) | 11pt |
| Show Line Numbers | controls whether line numbers are displayed | true |
| Enable Intellisense | whether to display Intellisense options while typing in the query editor | true |
| Keep Metadata Search Open | if this is true the search box in the metadata pane will always be displayed otherwise this option collapses to a small magnifying glass option in the upper right corner of the metadata pane | false |
| Intellisense Width | this option can be used to increase the default size of the intellisense dropdown window | 100% |

## Proxy

These settings control if/how DAX Studio will use a Proxy server to connect to online services (like DaxFormatter.com and crash reporting)

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Use System Proxy | If set to true DAX Studio will attempt to use the Proxy settings from the operating system | true |
| Proxy Address | The url for your proxy server | _(blank)_ |
| Proxy User | The username for accessing the Proxy server | _(blank)_ |
| Proxy Password | The password for accessing the Proxy server | _(blank)_ |


## Query History

DAX Studio keeps a log of recently executed commands (both successful and failed commands)

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| History Items to keep | by default DAX Studio keeps a limited number of recent queries (_Note: setting this number too high can affect the startup time for DAX Studio_)  | 200 |
| Show Trace Timings | This setting controls whether any trace timings are also capturd in the query log | true |



## Timeouts

These settings control the length of various timeouts for potentially long running operations

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Server Timings End Event Timeout | The trace events in the tabular engine are raised on a low priority background thread and occasionally if the server is very busy some events can be discarded. This setting controls how long DAX Studio will wait for a queries final QueryEnd event before it gives up and logs a warning. _For high latency connections (such as Azure AS and the Power BI XMLA endpoint) you may need to increase this setting._ | 15 sec |
| DAX Formatter Request Timeout | DAX Studio sends a background request https://daxformatter.com this setting controls how long we wait before we consider the request as failed and log an error  | 10 sec |
| Trace Startup Timeout | When DAX Studio starts a trace it periodically "pings" the server with an empty command. It then waits until the trace captures one of these requests before it considers the trace to be fully active. This setting controls how long DAX Studio will wait to see one of these "ping" requests before it stops waiting and logs and error.  _For high latency connections (such as Azure AS and the Power BI XMLA endpoint) you may need to increase this setting._| 30 sec |

## Separators

This setting controls the default Separator style used by DAX Studio.

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Separators | This setting can either be set to US/UK (commas as the list separator character) style or Other (semi-colons as the list separator character) | US/UK |

## Trace

This setting controls the behavours of the various tracing features.

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Legacy DirectQuery Trace | This setting controls whether tracing of DirectQuery Events is enabled for connections to servers with a version number earlier then 15.0 (SSAS 2017). For older servers the DirectQuery events do not allow per session filtering on the server so we have capture events from all sessions and apply filtering in DAX Studio. This places much more load on both the client and server which is why this option is off by default. If you need to enable it we recommend keeping any traces running for as short a time as possible | false |

## Results

These settings change settings for the Results window

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Font Family | This sets the font for the results pane | Segoe UI |
| Font Size | This is the default size for the results font | 11pt |
| Scale Font with Editor | When this option is selected increasing the zoom factor on the editor will also increase the zoom for the results pane | true |
| Exclude Headers when Copying data | This setting controls whether column headers are included when copying data from the results pane | true |
| Automatic Format Results | This setting controls whether the results pane attempts to auotmaticlly format numbers and percentages | false |

## Version Notifications 
**TBA**

## Metadata

This section controls what additional information is displayed in the tooltips for various metadata objects
| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Show Basic Statistics | This will show statistics like min/max/distinct values for a column | true |
| Show Sample Data | This will show a sample of 10 values from a given column | true |


These setting control the bahaviour of the automatic metadata refresh
| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Local Connections | For any connections to localhost (eg. PowerPivot, Power BI Desktop, SSDT)| true |
| Network Connections | For any connections to SSAS | true |
| Cloud Connections | For any connections to data sources that start with asazure:// or powerbi:// | false |

This section controls the visibilty of hidden objects in the Metadata pane.

| **Setting** | **Description** | **Default** |
| --- | --- | --- |
| Show Hidden columns, tables and Measures | This setting allows for the showing of objects that are hidden in the normal report views | true |

## DAX Formatter

**TBA**