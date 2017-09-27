---
title: Documentation
permalink: /:collection/index.html
---
## Main Screen
The following image outlines the main sections of the DAX Studio user interface
![](index/Documentation_MainScreen.png)

1. [Ribbon Control](features/ribbon-control) / [File Menu](features/file-menu)
1. [Metadata Panes](features/metadata-panes)
1. [Query Editor](features/query-editor)
1. [Output Panes](features/output-panes)
1. [Statusbar](features/statusbar)

## Current feature set
Dax Studio works as both an add-in for Excel 2010/2013 and as a standalone program and provides the following functionality:

- Modern User Interface
  - [Flexible Layout](features/flexible-layout)
  - [Multiple Tabs](features/multiple-tabs)
  - Office 2013 like [Ribbon Control](features/ribbon-control)
- Integrated Tracing
  - [Query Plan Trace](features/query-plan-trace)
  - [Server Timing Trace](features/server-timing-trace)
- [New Version notification](features/new-version-notification)
- [Single Installer](installation/single-installer)

## Proposed future feature set
We are always on the look out for new feature ideas. These are kept on the project's [issue list]({{ site.github.issues_url }}). You can vote for existing ideas/issues or add your own.

## Troubleshooting
If you have issues running Dax Studio there are some [logging settings](logging-settings) that can be enabled to help diagnose the issue.

There is also a small PowerShell [dependency script](installation/dependency-script) which will print out the versions of the .Net framework and the 2 Microsoft dependencies. The links to the 3 external dependencies which Dax Studio requires are listed on the [Installer page](installation/single-installer). 

If you get the error _"Not loaded. A runtime error occurred during the loading of the COM Add-in"_ for the Excel add-in you can try enabling the environment variable as detailed in these instructions [http://www.oneplacesolutions.com/support/0053.html](http://www.oneplacesolutions.com/support/0053.html) to display a more detailed error message

## Credits
Dax Studio is using the following open source libraries, without these it would not exist in it's current state:
- [AvalonEdit](http://http://avalonedit.net/) - main editor control
- [AvalonDock](http://wpftoolkit.codeplex.com) (part of Xceed WPF Toolkit - Community Edition) - Docking UI
- [Caliburn.Micro](http://caliburnmicro.codeplex.com) - MVVM framework
- [Fluent Ribbon](http://fluent.codeplex.com) - Ribbon control and Office 2013 window style
- [Hardcodet NotifyIcon](http://www.hardcodet.net/wpf-notifyicon) - version update notification messages

