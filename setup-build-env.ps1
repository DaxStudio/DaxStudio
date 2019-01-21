## =====================================================
##
##  DAX Studio - build environment setup script.
##
## this script copies a couple of dlls into the 
## a lib folder so that DaxStudio will build.
## =====================================================

cls


## Declare the required dll's and search folders

$requiredDlls = @("Microsoft.Excel.AdomdClient.dll"
, "Microsoft.Excel.Amo.dll"
, "Microsoft.AnalysisServices.AdomdClient.dll")

$searchFolders = @("${Env:ProgramFiles(x86)}\Common Files\Microsoft Shared\Office15\DataModel\"
, "$Env:ProgramFiles\Microsoft Office 15\root\vfs\ProgramFilesCommonX86\Microsoft Shared\OFFICE15\DataModel\"
, "$Env:ProgramFiles\Common Files\microsoft shared\OFFICE15\DataModel"
, "$Env:ProgramFiles\Common Files\microsoft shared\OFFICE16\DataModel"
, "$Env:ProgramFiles\Microsoft.NET\ADOMD.NET\110\"
, "$Env:ProgramFiles\Microsoft.NET\ADOMD.NET\120\"
, "$Env:ProgramFiles\Microsoft.NET\ADOMD.NET\150\"
)

## 1. Create lib subfolder

$ScriptRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

$libPath = "$ScriptRoot\lib"

if (-not (Test-Path $libPath))
{
	mkdir $libPath | Out-Null	
}

## 2. Try to copy the dlls by searching for it in all the declared folders

$searchFolders |% {
	
	$folder = $_
	
	$requiredDlls |% {
	
		$dllName = $_
		
		$dllPath = Join-Path $folder $dllName		
		
		if (Test-Path $dllPath)
		{
			$newDllPath = Join-Path $libPath $dllName
			
			Copy-Item $dllPath $newDllPath -Verbose
		}		
	}	
}

## 3. Confirms if all the dlls had been copied

$dllsOnLibFolder = gci -Path $libPath -Filter "*.dll" | select  -ExpandProperty Name

$requiredDlls |% {
	
	if (-not ($dllsOnLibFolder -contains $_))
	{
		Write-Warning "Cannot find dependency: $($_)" 
	}
	
}

