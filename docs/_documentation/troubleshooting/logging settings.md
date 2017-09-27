---
title: Logging Settings
---

## Application Logging
If Dax Studio crashes or reports errors while running if you enable detailed application logging by enabling serilog in the daxstudio.exe.config file.

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

## Excel add-in logging
_Same as above except the config file is called Daxstudio.**dll**.config_

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