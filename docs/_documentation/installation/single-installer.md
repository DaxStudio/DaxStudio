---
title: Universal Installer
---

As of version 2 there is now a single integrated installer which caters for both 32 and 64 bit installs and for both Excel 2010 and Excel 2013. It will also download and install any missing prerequisites.

Prerequisites (the installer will attempt to download these if they are not present):
* [.Net Framework 4.5](https://www.microsoft.com/en-au/download/details.aspx?id=30653)
* SQL Server 2016 version of ADOMD ([part of the SQL Server feature pack](http://www.microsoft.com/en-us/download/details.aspx?id=52676))
* SQL Server 2016 version of AMO ([part of the SQL Server feature pack](http://www.microsoft.com/en-us/download/details.aspx?id=52676))


## Manually Installing Dependencies
The links to the dependencies are listed above. For the SQL Feature pack you need to click on the download button which will take you to a screen where you need to select which components to download. The two components that you require are called SQL_AS_AMO.msi and SQL_AS_ADOMD.msi - these are listed twice, once for x64 (64-bit) and once for x86 (32-bit) - you need to download the ones that match the "bitness" of your operative system and then double click on them to install them. 