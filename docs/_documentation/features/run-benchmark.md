---
title: Run Benchmark
---

The benchmarking feature provides an automated way of running a query a number of times with a cold and warm cache and then collecting detailed timing information.

The **Run Benchmark** button is in the **Advanced** tab in the ribbon

![](benchmark-button.png)

Clicking on this button brings up the following dialog where you can set the number of times the current query should be run on a cold and warm cache. To do the _cold cache_ runs we execute a Clear Cache command before each execution. The _warm cache_ runs we just execute the query without clearing the cache so you should see lower durations if your query is able to take advantage of the engine cache.

![](benchmark-dialog.png)

By default we run the same number of cold and warm cache executions, but you can change this by unticking the link between the two and then setting the two numbers individually.

Once all of the queries are complete we show the timings results. The first tab is the summary results which shows you various summary statistics about the cold and warm cache runs

![](benchmark-summary.png)

And there is also a detailed view which shows the detailed results of each individual query.

![](benchmark-details.png)