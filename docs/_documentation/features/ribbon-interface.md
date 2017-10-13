# Ribbon Interface

## Main Ribbon
The main ribbon contains commands for running queries, connecting to data sources and activating traces
![](Ribbon interface_ribbon_main.png)
* **Run** - will start a query, while a query is running the run button will be disabled until the query finishes (or is cancelled
* **Stop** - cancels a running query, this button is only enabled while a query is running.
*  **Clear Cache** -  will clear the cache on a SSAS server, requires admin rights and the button will be disabled if the user does not have them. This button also runs a simple query in order to force the MDX/DAX script to be evaluated.
* **Output** - lets you select the [output modes](output-modes) for your queries
* **Cut/Copy/Paste** - standard clipboard actions
* **Connect** - lets you change the current connection details
* **Database** - allows for the selection of the database to query
* **Traces** (only available if the user is a server admin)
	* **Query Plan** - shows the DAX logical/physical query plan text
	* **Server Timings** - shows basic server side timing information
## Format Ribbon
The format ribbon contains utility commands that allow you to alter the current query
![](Ribbon interface_ribbon_format.png)
* **To Upper** - converts the selected text to uppercase
* **To Lower** - converts the selected text to lowercase
* **Comment** - comments the selected line(s)
* **Uncomment** - uncomments the selected line(s)
* **Undo** - undoes the previous edits
* **Redo** - redoes any edits that were undone
* **Merge Parameters** - takes a selected query with an XML parameter block (most likely captured from a profiler trace) and puts the parameter values into the query text. _Note: you can also just run the query and the parameters will be inserted at run time without having to alter the actual query text_

## Help Ribbon
The help ribbon contains links to MSDN forums and to this wiki
![](Ribbon interface_ribbon_help.png)
* **Links** - provide easy access to the relevant web pages
* **About** - displays version information