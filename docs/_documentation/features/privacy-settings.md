---
title: Privacy Settings
---

The privacy options exist to allow users to turn off some or all of the internet based functionality. This allows people with highly sensitive information in their data models to eliminate the possibility of any of this information being sent outside of their organization. 

In normal operation we would recomment leaving all these options disabled as enabling them restricts the functionality of DAX Studio.

**Block All Internet Access** this option can only be set by an administrator during an _All Users_ install. It blocks all features that access the internet and to reset this requires and uninstall and reinstall.

This can also be set on the command line while installing DAX Studio using the `/TASKS="blockallinternetaccess"` command line switch

![](install-options.png)

**Block Version Checks** This option blocks DAX Studio from automatically checking for new releases. 

**Block External Services** This option blocks DAX Studio from using external services. Currently the only external service is the daxformatter.com website. There is a small chance of sensitive information being leaked externally only if the queries you are formatting contain sensitive information (like customer names, account numbers, etc)

**Block Crash Reporting** This option blocks DAX Studio from giving the user the option to send crash reports to the development team. There crash reports include a stack trace and an optional screenshot. There is a chance that the screenshot could include query text or partial results that might include sensitive information. It should be noted that the user has the opportunity of reviewing the screenshot before sending it and can choose not to include the screenshot if necessary. 

![](privacy-options.png)
