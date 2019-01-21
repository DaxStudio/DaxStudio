Building Dax Studio
===================

Before attempting to build DAX Studio, you will need to run the powershell 
script, `setup-build-env.ps1`, located in the root of the repository.

This script attempts to find a few of binary dependencies and copies them to a
local 'lib' folder.  These dependencies are:

    Microsoft.AnalysisServices.AdomdClient.dll
    Microsoft.Excel.AdomdClient.dll
    Microsoft.Excel.Amo.dll

