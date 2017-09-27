---
title: Server Timing Trace
---

The standard timings reported in the output window is the elapsed time for the query recorded by Dax Studio, but that can be impacted by network speeds and the size of the resultset. If you want to see the query timing from the server perspective you can do this with the server timing trace button.

This button causes an extra tab to be displayed which shows the total time the server spent processing the query as well as the time spent in the Storage Engine and the number of Storage Engine requests for the query.

_Note: Tracing requires server admin rights, if you do not have these the trace buttons will be disabled_

![](Server Timing Trace_trace_server_timings.png)