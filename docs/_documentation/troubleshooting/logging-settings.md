---
title: Logging Settings
---

## Enabling Diagnostic Logging - Standalone
As of v2.7.0 you can enable diagnostic logging in DAX Studio by holding down the left SHIFT key while starting the application. If you are having issues with the Excel addin you need to hold down the SHIFT key while Excel is starting up. 

## Enabling Diagnostic Logging - Excel Addin
The Excel addin can create it's own separate log file. If you are having issues with the Excel addin you need to hold down the SHIFT key while Excel is starting up. So if you have any Excel windows open you need to close all of those then hold the SHIFT key down while Excel starts up until you see the main Excel window open. If you watch the Excel splash screen carefully you should see a message as it loads the DAX Studio addin - it is at this point that the addin checks to see if the SHIFT key is being held down.  

## Log Folder Location
Logs are stored in the ```%LOCALAPPDATA%\DaxStudio\log``` folder. You can either paste this address into the Windows Explorer address bar or the Help - About window also has a link to this location.

![](Help-About.png)

> **Note:** the following sections are included for completeness, but holding the ```SHIFT``` key at startup and the functionality now available in [DAX Studio Checker](../daxstudio-checker) supercedes the need to manually enable logging or check dependencies



## Manually Enabling Diagnostic Logging
You can manually enable detailed application logging by enabling serilog in the daxstudio.exe.config file.

The start of the daxstudio.exe.config file looks like the following:

{% highlight xml %}
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <!--
  <appSettings>
    <add key="serilog:minimum-level" value="Verbose" />
    <add key="serilog:write-to:RollingFile.pathFormat" value="D:\temp\DaxStudio-{Date}.txt" />
    <add key="serilog:write-to:RollingFile.retainedFileCountLimit" value="10" />
  </appSettings>
  -->
  <runtime>
  .....
{% endhighlight %}

To capture a detailed application log perform the following steps:
1. remove the <code>&lt;!--</code> comment start from before the <code>&lt;appSettings&gt;</code> tag and the <code>--&gt;</code> comment end tag from after the &lt;/appSettings&gt; tag
1. change the value for the <code>pathFormat</code> section to point to a folder on your system
1. repeat the steps that trigger the error
1. add the <code>&lt;!--</code> start and end <code>--&gt;</code> comment tags back to switch off the logging

#

# Manually enabling Excel add-in logging

_Same as above except the config file is called Daxstudio.**dll**.config_

> **Note:** The start-up *fusion* logging detailed below can now be more easily enabled and disabled using the [DAX Studio Checker](../daxstudio-checker) tool

## Start up Logging
If Dax Studio fails to even start up this often points to a problem with the dependencies. If this is the case the application crash happens before the application logging above is initialized so it is not able to trap the error. To capture these issues we need to enable a feature of the .Net called "Fusion" logging. Fusion is the part of the .Net framework that finds and load dependencies.

Start by creating a text file on your desktop called FusionLogOn.txt and paste the following code in

<pre>
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Fusion](HKEY_LOCAL_MACHINE_SOFTWARE_Microsoft_Fusion)
"LogFailures"=dword:00000001
"LogPath"="d:\\data\\fusion\\"
</pre>

Note that the back slashes need to be doubled up and that the LogPath should point to an empty folder (Fusion will create a sub-folder with a file for each binding error.

Then rename the file from a .txt extension to a .reg - the icon should change to document with a stack of blue cubes next to it. Double-clicking on the .reg file will merge these settings into your registry.

Then try to run DaxStudio once, it should generate some logs files in the folder you specified. These are just htm files and you can view their contents with any text editor. 

After this it's important to turn the Fusion logging off as it is a machine wide setting that will log the start up of any .Net program

To do that create a text file called FusionLogOff.txt with the following content

<pre>
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Fusion](HKEY_LOCAL_MACHINE_SOFTWARE_Microsoft_Fusion)
"LogFailures"=dword:00000000
</pre>
And then rename the extension from .txt to .reg and double click the file

You can then zip up the contents of the log folder and add it as an attachment to an issue.

> Note that there should always be some output from the Fusion log as there is an optional theme that the AvalonDock component looks for which is not used in DaxStudio.