### This script creates a new winget manifest based off the latest release on Github


function Get-RemoteChecksum( [string] $Url, $Algorithm='sha256' ) {
    $fn = [System.IO.Path]::GetTempFileName()
    Invoke-WebRequest $Url -OutFile $fn -UseBasicParsing
    $res = Get-FileHash $fn -Algorithm $Algorithm | % Hash
    rm $fn -ea ignore
    return $res.ToLower()
}

function Get-WingetManifest([string] $url, [string]$version, [string]$checksum)
{
$manifest = @"
Id: DaxStudio.DaxStudio
Version: $version
Name: DAX Studio
Publisher: DaxStudio.org
License: Ms-RL
LicenseUrl: https://daxstudio.org/documentation/license/
Tags: dax powerbi
Description: The ultimate tool for working with DAX queries
Homepage: https://daxstudio.org
Installers:
  - Arch: Neutral
    Url: $url
    Sha256: $checksum
    InstallerType: inno
"@
return $manifest
}

$manifestPath = "c:\users\darren.gosbell\documents\github\winget-pkgs\manifests\DaxStudio\DaxStudio"
$Release = 'https://github.com/DaxStudio/DaxStudio/releases/latest'

[Net.ServicePointManager]::SecurityProtocol = "tls12"

$download_page = Invoke-WebRequest -Uri "$Release" -UseBasicParsing

$urlstub = $download_page.rawcontent.split('"') | 
            Where-Object {$_ -match '\.exe$'} |
            Select-Object -First 1
$url = "https://github.com$urlstub"

$checksum = Get-RemoteChecksum $url

$version = $urlstub.split('/') | ? {$_ -match '^v?[0-9.]+$'} | select -Last 1
$version = $version.trim('v')

$manifestFile = join-path $manifestPath "$($version).yaml"

if (-not (test-path $manifestFile )) {
Get-WingetManifest $url $version $checksum | Out-File $manifestFile -Force
}