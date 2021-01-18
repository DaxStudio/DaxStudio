---
title: Publish Functions
---

There are two buttons available in the **Advanced** tab of the **Options** window. These buttons exist to help support the https://dax.guide website. 

* **Publish Functions** this options sends a list of all the DAX functions and their parameters to dax.guide.
* **Export Functions** this option exports a file with the list of DAX functions. (only used when there is no internet connection available)

Typically these buttons would be rarely used and would usually be at the request of one of the DAX Studio development team.

However there is now an extremely wide variety of versions of the tabular engine being used with the following major varieties:
* at least 5 versions of Power BI Desktop 
  - 3 supported releases of Power BI Desktop for Report Server
  - the Microsoft Store version
  - the install version
* at least 5 versions of SSAS on-prem (2012/2014/2016/2017/2019)
* Power BI Premium XMLA endpoint
* Azure Analysis Services
* at least 4 major versions of PowerPivot (Excel 2010/2013/2016/O365)

And then there are different service packs and cummulative updates for all of the above plus beta versions and preview releases. So with all this variation it was decided that these buttons should be publically available. So should anyone notice a new function in the code completion that was not listed on dax.guide this gives them an easy way of sending the details of the functions and the engine version to dax.guide.