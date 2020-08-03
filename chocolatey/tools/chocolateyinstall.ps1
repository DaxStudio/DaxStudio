$ErrorActionPreference = 'Stop'

$toolsDir   = Split-Path -parent $MyInvocation.MyCommand.Definition
$fileLocation = (Get-ChildItem -Path $toolsDir -Filter '*.exe').FullName

$packageArgs = @{
   packageName    = $env:ChocolateyPackageName
   fileType       = 'EXE' 
   file           = "$fileLocation"
   softwareName   = "$env:ChocolateyPackageName*"
   silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
   validExitCodes = @(0)
}

Install-ChocolateyInstallPackage @packageArgs

New-Item "$fileLocation.ignore" -Type file -Force | Out-Null
