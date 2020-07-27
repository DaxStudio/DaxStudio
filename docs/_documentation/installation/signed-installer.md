---
title: Signed Installer
---

As of v2.12 the DAX Studio installer is now signed with an [Extended Validation (EV) Certificate](https://en.wikipedia.org/wiki/Extended_Validation_Certificate) 

The fact that Installer now signed with an EV certificate should hopefully prevent all the issues and warnings coming from Windows [SmartScreen](/documentation/installation/smart-screen/) when attempting to download and install DAX Studio.

A **HUGE** thank-you goes out to the guys at [SQLBI.com](https://sqlbi.com). To acquire an EV certificate not only requires a financial outlay, but you also need to have a legal company entity with a physical office with financial records and things like that. Both of these things make it pretty much impossible for a free/open source tool to obtain an EV certificate on their own without assistance.

> **Note:** Even though Windows may show SQLBI as the publisher this is just because they have funded the code signing certificate and it is a condition of the EV certificate that it has to bear the legal name of the entity that purchased it. DAX Studio is still an open source tool and all support requests still go through our [github issue register](https://github.com/daxstudio/daxstudio/issues).

![uac-prompt](uac-prompt.png)
