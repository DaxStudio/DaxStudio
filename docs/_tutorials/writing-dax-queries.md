---
title: Writing DAX Queries
---

DAX Queries have quite a simple structure. Microsoft describes the query syntax in their documentation [here](https://docs.microsoft.com/en-us/dax/dax-queries). But in this guide we are going to take a very practical, example based approach. 

If you want to follow along and try out these queries yourself all you need is:
* DAX Studio
* Power BI Desktop
* the [Adventure Works 2020](https://github.com/microsoft/powerbi-desktop-samples/raw/master/DAX/Adventure%20Works%20DW%202020.pbix) sample file

The simplest way to get started after installing both DAX Studio and Power BI Desktop is to open the **Adventure Works 2020.pbix** file, then click on External Tools and launch DAX Studio from there. 

For more details on how you can connect to your particular data model check out the tutorial on [Getting Connected](getting-connected)

## Basic Queries

The simplest form of DAX query is `EVALUATE <table expression>` 

So you can run `EVALUATE Customer` to output all the rows in the customer table

![](evaluate-customer.png)

But you don't just have to use a table name, you can use any function that returns a table. To get a distinct list of all the values in a column you can use the [VALUES](https://dax.guide/values) function which returns a table with a single row with all the unique values from the specified column.

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">VALUES</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Customer[City]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

![](evaluate-customer-city.png)

Or if you don't want every row in the table you could use the [CALCULATETABLE](https://dax.guide/calculatetable) function to only return rows that meet a given criteria.

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">CALCULATETABLE</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Customer,&nbsp;Customer[City]&nbsp;=&nbsp;<span class="StringLiteral" style="color:#D93124">"Redmond"</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

You can even combine the [VALUES](https://dax.guide/values) and [CALCULATETABLE](https://dax.guide/calculatetable) functions to get a list of all Cities that have a first character of "R"

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">CALCULATETABLE</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;<span class="Keyword" style="color:#035aca">VALUES</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Customer[City]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,&nbsp;<span class="Keyword" style="color:#035aca">LEFT</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Customer[City],&nbsp;<span class="Number" style="color:#EE7F18">1</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span>&nbsp;=&nbsp;<span class="StringLiteral" style="color:#D93124">"R"</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

## Sorting Results

If we continue on with the previous example you will see that the results come back in a random order. If we want our query to sort the results we can add an optional `ORDER BY` clause to the end of the query. So if we wanted to sort the results by the city name we would do the following:

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">CALCULATETABLE</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;<span class="Keyword" style="color:#035aca">VALUES</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Customer[City]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,&nbsp;<span class="Keyword" style="color:#035aca">LEFT</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Customer[City],&nbsp;<span class="Number" style="color:#EE7F18">1</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span>&nbsp;=&nbsp;<span class="StringLiteral" style="color:#D93124">"R"</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="Keyword" style="color:#035aca">ORDER</span>&nbsp;<span class="Keyword" style="color:#035aca">BY</span>&nbsp;Customer[City]<br>

> **NOTE:** Some client tools (like Power BI Desktop) will generate an `ORDER BY` clause for you based on the properties set in your data model


## Adding Calculations

To add a calculation to your query like measures and variables you would use the optional `DEFINE` keyword at the start of your query

To define a new measure in your query which sums the value of the existing `Sales[Sales Amount]` column you would write the following:

<span class="Keyword" style="color:#035aca">DEFINE</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">MEASURE</span>&nbsp;Sales[My&nbsp;Sales&nbsp;Amount]&nbsp;=<br><span class="indent8">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">ADDCOLUMNS</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;<span class="Keyword" style="color:#035aca">VALUES</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;'Date'[Month]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,&nbsp;<span class="StringLiteral" style="color:#D93124">"My&nbsp;Sales&nbsp;Amount"</span>,&nbsp;[My&nbsp;Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

To define multiple measures you can add multiple blocks of `MEASURE <table>[<measure name>] = <expression>`

<span class="Keyword" style="color:#035aca">DEFINE</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">MEASURE</span>&nbsp;Sales[My&nbsp;Sales&nbsp;Amount]&nbsp;=<br><span class="indent8">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">MEASURE</span>&nbsp;Sales[My&nbsp;Double&nbsp;Sales&nbsp;Amount]&nbsp;=<br><span class="indent8">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>&nbsp;*&nbsp;<span class="Number" style="color:#EE7F18">2</span><br><span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">ADDCOLUMNS</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">VALUES</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;'Date'[Month]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,<br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="StringLiteral" style="color:#D93124">"My&nbsp;Sales&nbsp;Amount"</span>,&nbsp;[My&nbsp;Sales&nbsp;Amount],<br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="StringLiteral" style="color:#D93124">"My&nbsp;Double&nbsp;Sales&nbsp;Amount"</span>,&nbsp;[My&nbsp;Double&nbsp;Sales&nbsp;Amount]<br><span class="Parenthesis" style="color:#808080">)</span><br>

![](evaluate-define-measures.png)


## Returning a single value

Sometimes you may just want to return the result of a measure. But measures return a single scalar value not a table, so if you try to write the following it will produce a syntax error

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

We can fix this by using the table constructor syntax and wrapping the measure in curly braces `{ }`

<span class="Keyword" style="color:#035aca">EVALUATE</span><br>{&nbsp;<span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>&nbsp;}<br>

For older versions of the tabular engine which do not support the table constructor syntax we can use the [ROW](https://dax.guide/row) function

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">ROW</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;<span class="StringLiteral" style="color:#D93124">"Sales&nbsp;Amount"</span>,&nbsp;[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>


You can also mix this with the `DEFINE` clause to create a measure expression and then return a single value

<span class="Keyword" style="color:#035aca">DEFINE</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">MEASURE</span>&nbsp;Sales[Total&nbsp;Sales]&nbsp;=<br><span class="indent8">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="Keyword" style="color:#035aca">EVALUATE</span><br>{&nbsp;[Total&nbsp;Sales]&nbsp;}<br>

## Selecting columns from multiple tables

The easiest way to generate a query using columns from multiple tables is to use the [SUMMARIZECOLUMNS](https://dax.guide/summarizecolumns) function. This function takes a list of columns, followed by an optional list of table expressions to use as filters, followed by an optional list of measures/expressions.

> **NOTE:** It is _**strongly**_ recommended to always use a measure or expression of some sort with the `SUMMARIZECOLUMNS` function if you don't do this the function will generate a large crossjoin of all possible combinations of every value in the specified columns which is not normally useful

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">SUMMARIZECOLUMNS</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span>Product[Color],<br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span>Reseller[Business&nbsp;Type],<br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">FILTER</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;<span class="Keyword" style="color:#035aca">ALL</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Product[List&nbsp;Price]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,&nbsp;Product[List&nbsp;Price]&nbsp;&gt;&nbsp;<span class="Number" style="color:#EE7F18">150.00</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,<br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">TREATAS</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;{&nbsp;<span class="StringLiteral" style="color:#D93124">"Accessories"</span>,&nbsp;<span class="StringLiteral" style="color:#D93124">"Bikes"</span>&nbsp;},&nbsp;'Product'[Category]&nbsp;<span class="Parenthesis" style="color:#808080">)</span>,<br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="StringLiteral" style="color:#D93124">"Total&nbsp;Sales"</span>,&nbsp;<span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="Parenthesis" style="color:#808080">)</span><br>

## Multiple Resultsets

DAX queries also allow for the return of multiple recordsets within a given batch

So you can execute the following: 

<span class="Keyword" style="color:#035aca">EVALUATE</span><br>Customer<br><span class="Keyword" style="color:#035aca">EVALUATE</span><br>'Product'<br>

![](evaluate-2-recordsets.png)

And you will get 2 tabs returned in DAX Studio, one with the contents of the **Customer** table and the other with the contents of the **Product** table.

But note that within a single batch, although you can have multiple `EVALUATE` statements you can only have a single `DEFINE` statement. So you would need to declare all your calculations in that one block.

<span class="Keyword" style="color:#035aca">DEFINE</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">MEASURE</span>&nbsp;Sales[Total&nbsp;Sales]&nbsp;=<br><span class="indent8">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">SUM</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales[Sales&nbsp;Amount]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="indent4">&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">MEASURE</span>&nbsp;Sales[Total&nbsp;Cost]&nbsp;=<br><span class="indent8">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span><span class="Keyword" style="color:#035aca">SUMX</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;Sales,&nbsp;Sales[Unit&nbsp;Price]&nbsp;*&nbsp;Sales[Order&nbsp;Quantity]&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br><span class="Keyword" style="color:#035aca">EVALUATE</span><br>{&nbsp;[Total&nbsp;Sales]&nbsp;}<br><span class="Keyword" style="color:#035aca">EVALUATE</span><br>{&nbsp;[Total&nbsp;Cost]&nbsp;}<br>

![](evaluate-2-recordsets-with-measures.png)

## Using Parameters in Queries

One of the unique features that DAX Studio has is the [support for parameterized queries](/documentation/features/parameter-support/)

To add a parameter to a DAX query you can start with a query that includes a filter such as the following:

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">FILTER</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;'Product',&nbsp;'Product'[Color]&nbsp;=&nbsp;<span class="StringLiteral" style="color:#D93124">"Red"</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

And then replace the reference to `"Red"` with a parameter called `@Color` 

<span class="Keyword" style="color:#035aca">EVALUATE</span><br><span class="Keyword" style="color:#035aca">FILTER</span><span class="Parenthesis" style="color:#808080">&nbsp;(</span>&nbsp;'Product',&nbsp;'Product'[Color]&nbsp;=&nbsp;<span class="QueryParameter" style="color:#dc419d">@Color</span>&nbsp;<span class="Parenthesis" style="color:#808080">)</span><br>

When you execute a query with a parameter, DAX Studio will prompt you for the parameter to use

![](evaluate-parameter.png)

