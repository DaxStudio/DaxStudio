## =====================================================
##
##  DAX Studio - build environment setup script.
##
## this script copies a couple of dlls into the 
## a lib folder so that DaxStudio will build.
## =====================================================

## 1. Create lib subfolder
$ScriptRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$libPath = "$ScriptRoot\lib"
mkdir $libPath

## 2. Copy in Microsoft.Excel.AdomdClient.dll
$srcPath = ${Env:ProgramFiles(x86)}
$srcPath = "$srcPath\Common Files\Microsoft Shared\Office15\DataModel\"

copy-item "$srcPath\Microsoft.Excel.AdomdClient.dll" $libPath
copy-item "$srcPath\Microsoft.Excel.Amo.dll" $libPath

## 3. Copy in C:\Program Files\Microsoft.NET\ADOMD.NET\110\Microsoft.AnalysisServices.AdomdClient.dll
copy-item "$Env:ProgramFiles\Microsoft.NET\ADOMD.NET\110\Microsoft.AnalysisServices.AdomdClient.dll" $libPath