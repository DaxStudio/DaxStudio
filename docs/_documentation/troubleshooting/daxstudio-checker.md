---
title: DAX Studio Checker
---

> _**Note:** The output from DaxStudio.Checker is mainly useful in situations where DAX Studio is failing to start (which appear to be getting increasingly rare). The dev team will generally request the output from DaxStudio.Checker if they feel it is required._

In the folder where DAX Studio is installed (usually ```c:\program files\DaxStudio```) you will find a program called ```DaxStudio.Checker.exe```. 

![](daxstudio-checker-location.png)

In cases where DAX Studio fails to start up this utility can be used to identify potential issues with your PC or the Microsoft data providers that DAX Studio relies upon.

When you run ```DaxStudio.Checker.exe``` it will produce output similar to the following.

![](daxstudio-checker.png)

This can then be copied and pasted into an [issue](/issues) on GitHub if required to assist with troubleshooting.

In some cases additional, deeper levels of logging may be required. **DAX Studio Checker** includes some menu items to assist in switching on this logging. This is usually not required, but in some rare cases the developers may request that you enable one or more of these options to assist in finding issues.

![](file-menu.png)