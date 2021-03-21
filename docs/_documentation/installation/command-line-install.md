---
title: Command Line Parameters for the Installer
---

The Setup program accepts optional command line parameters which can be useful for scripted or automated installs.

| Command | Description
|---|---|
| /HELP, /? | Shows this information. |
| /SP- | Disables the This will install... Do you wish to continue? prompt at the beginning of Setup. |
| /SILENT, /VERYSILENT | Instructs Setup to be silent or very silent. |
| /SUPPRESSMSGBOXES | Instructs Setup to suppress message boxes. |
| /LOG | Causes Setup to create a log file in the user's TEMP directory. |
| /LOG="filename" | Same as /LOG, except it allows you to specify a fixed path/filename to use for the log file. |
| /NOCANCEL | Prevents the user from cancelling during the installation process. |
| /NORESTART | Prevents Setup from restarting the system following a successful installation, or after a Preparing to Install failure that requests a restart. |
| /RESTARTEXITCODE=exit code | Specifies a custom exit code that Setup is to return when the system needs to be restarted. |
| /CLOSEAPPLICATIONS | Instructs Setup to close applications using files that need to be updated. |
| /NOCLOSEAPPLICATIONS | Prevents Setup from closing applications using files that need to be updated. |
| /FORCECLOSEAPPLICATIONS | Instructs Setup to force close when closing applications. |
| /FORCENOCLOSEAPPLICATIONS | Prevents Setup from force closing when closing applications. |
| /LOGCLOSEAPPLICATIONS | Instructs Setup to create extra logging when closing applications for debugging purposes. |
| /RESTARTAPPLICATIONS | Instructs Setup to restart applications. |
| /NORESTARTAPPLICATIONS | Prevents Setup from restarting applications. |
| /LOADINF="filename" | Instructs Setup to load the settings from the specified file after having checked the command line. |
| /SAVEINF="filename" | Instructs Setup to save installation settings to the specified file. |
| /LANG=language | Specifies the internal name of the language to use. |
| /DIR="x:\dirname" | Overrides the default directory name. |
| /GROUP="folder name" | Overrides the default folder name. |
| /NOICONS | Instructs Setup to initially check the Don't create a Start Menu folder check box. |
| /TYPE=type name | Overrides the default setup type. |
| /COMPONENTS="comma separated list of component names" | Overrides the default component settings. |
| /TASKS="comma separated list of task names" | Specifies a list of tasks that should be initially selected. |
| /MERGETASKS="comma separated list of task names" | Like the /TASKS parameter, except the specified tasks will be merged with the set of tasks that would have otherwise been selected by default. |
| /PASSWORD=password | Specifies the password to use. |
| /ALLUSERS | Instructs Setup to install in administrative install mode. |
| /CURRENTUSER | Instructs Setup to install in non administrative install mode. |


The DAX Studio installer uses InnoSetup. For more detailed information, please visit https://jrsoftware.org/ishelp/index.php?topic=setupcmdline

## Components

The following are DAX Studio specific options that can be used in the `/COMPONENTS=` parameter. By default all components are installed

| Task | Default | Description |
|---|---|---|
| Core | Yes | [Required] This is the core DAX Studio program |
| Excel | Yes | [Optional] This component installs the Excel Addin |


## Tasks

The following are DAX Studio specific options that can be used in the `/TASKS=` or `/MERGETASKS=`

| Task | Default | Description |
|---|---|---|
| desktopicon | Yes | Creates an Icon on the Desktop |
| blockallinternetaccess | No | [NOT RECOMMENDED] Blocks all features requiring internet access including version checks, dax formatting and crash reporting. This setting requires a re-install to change. |
