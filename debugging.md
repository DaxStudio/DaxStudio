## Debugging DAX Studio

The DAX Studio solution is broken up into a number of different projects

| **Project** | **Entry Point** | **Description**|
| --- | --- | --- |
| DaxStudio.ADOTabular | No | This is a wrapper over AdomdClient that gives a tabular abstraction (ie. Models/Tables/Columns) over the top of the  |
| DaxStudio.AvalonDock.Theme | No | Theme support for the AvalonDock component |
| DaxStudio.Checker | **Yes** | This is a standalone project used for running environment checks on end user PCs. The primary use case for this utility is to help diagnosing issues with users PCs when DAX Studio fails to start. Once DAX Studio is up and running the normal diagnostic logging usually provides more actionable information. |
| DaxStudio.CommandLine| **Yes**| This is the command-line project that creates dscmd |
| DaxStudio.Common | No | This contains various classes that are used between both the Excel Addin and Standalone projects |
| DaxStudio.Controls.DataGridFilter | No | This project contains a custom WPF DataGrid Filter control |
| DaxStudio.DaxEditor | No | This is a wrapper project over AvalonEdit which customizes it for DAX Studio |
| DaxStudio.ExcelAddin | **Yes** | This project is the Excel addin used to enable connectivity to PowerPivot models. If you need to debug an issue with PowerPivot you should set this as your startup project in visual studio.  |
| DaxStudio.FileIcons | No | A resource dll holding the various file icons |
| DaxStudio.Interfaces | No | This project contains interfaces that are shared by two or more of the other projects |
| DaxStudio.QueryTrace | No | This project contains the core query tracing engine |
| DaxStudio.QueryTrace.Excel | No | This project contains the core query tracing engine used by the Excel Addin |
| DaxStudio.SqlFormatter | No | Holds a fork of the SQL formatter project. Used for formatting xmSQL and T-SQL for direct query scans |
| DaxStudio.Standalone | **Yes** | This project is the entry point for the standalone .exe version of DAX Studio. If you are debugging issues with anything other than PowerPivot models this project should be set as your startup project in Visual Studio |
| DaxStudio.UI | No | This project contains all the User Interface objects |
| DaxStudio.UnitComboLib | No | This project contains the custom combo control that contains the zoom percentage control at the bottom of the Query editor |

## DaxStudio.UI

This is the core project that contains all of the User Interface. It uses [Caliburn.Micro](https://caliburnmicro.com) to implement the MVVM pattern and is using MEF as a sort of light-weight IoC container. 

### Application Startup Flow

1. DaxStudio.Startup runs `static void Main()` in EntryPoint.cs, this sets up logging, creates a WPF and runs it
1. When the app from the previous step is run it creates an `AppBootstrapper` from `DaxStudio.UI\AppBootstrapper.cs`
1. This first runs the `Configure` method to setup the MEF container
1. Then the `OnStartup` method is called which creates a `ShellViewModel` object
1. The `ShellViewModel` then gets a `RibbonViewModel` and `StatusBarViewModel` from MEF
1. The `RibbonViewModel` then gets a `DocumentTabViewModel` from MEF (this represents the collection of open documents)
1. The `DocumentTabViewModel` will then create a new `DocumentViewModel` 
1. The `DocumentViewModel` is the core part of the application, it will create a collection of toolwindows including:
    - `MetadataPaneViewModel`
    - `DmvPaneViewModel`
    - `FunctionPaneViewModel`
    - `OutputPaneViewModel`
    - `QueryHistoryPaneViewModel`
    - `QueryResultsPaneViewModel`
1. Once the `DocumentViewModel` has been created it will open and display a `ConnectionDialogViewModel`


## Excel Add-in Signing

### updating debug self-signed certificate

```
$Certificate = new-selfsignedcertificate -subject daxstudio.org -Type CodeSigning -CertStoreLocation cert:\CurrentUser\My -KeyFriendlyName "DAX Studio Excel Addin Debug Cert"

$Pwd = ConvertTo-SecureString -String "<Password>" -Force -AsPlainText 

Export-PfxCertificate -Cert $Certificate -FilePath "c:\temp\DaxStudioSelfSigned.pfx" -Password $Pwd 
```

### Encrypting the Cert

see https://www.appveyor.com/docs/how-to/secure-files/
