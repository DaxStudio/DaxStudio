---
title: Windows 10 Smartscreen
---
## What is SmartScreen?

SmartScreen is a set of technologies from Microsoft that is designed to protect your pc from malicious software. Unfortunately for specialty products like DAX Studio, one of the criteria that Smart Screen uses to determine if a given download is safe is whether the download is digitally signed and whether or not it has been "frequently dowloaded". And while the executables, Installer and Excel Add-in are all digitally signed apparently the level at which something is considered "frequently downloaded" is quite high.

## Digital Signature

If you right click on the installer or DaxStudio.exe in Windows Explorer and view the properties you should see a Digital Signature signed by "Darren Gosbell" (the primary developer of DAX Studio)

[Digital Signature](digital-signature.png)

## Can I check DAX Studio for malicious software before downloading it?

Yes, you can right click on the download button and choose the option to "copy shortcut" (the exact wording of this my vary between the various browsers). Then go to https://wwww.virustotal.com and you can submit the URL there for scanning. Virus total will the scan using a large number of AV scanners and report any issues. Typically it should report being 100% clean, but from time to time you may see a false positives often these resolve themselves within a few days. 

## Installing DAX Studio on Windows 10 with Microsoft Edge and SmartScreen Warnings

Below are the steps involved to install DAX Studio with all the Smart screen prompts.
Please note that the following steps are using Microsoft Edge to download and install DAX Studio 

Start by downloading the DAX Studio installer from the home page of the website by clicking  on the green button.

![Download Button](download-button.png) 

The following message will appear in the download bar after the downloaded completed.

| ![Edge Download Warning](edge-download-warning.png) |

To keep the download you need to click on the ellipses "…" and selected Keep

![Edge Download Keep](edge-download-keep.png)

Another prompt will now appear saying that "This app might harm your device"

| ![](smart-screen-1.png) |


 To proceed you need to click on **Show more** then click on **Keep anyway**

| ![](smart-screen-keep-anyway.png) |


The installer is now downloaded and you can click on the exe to start the installation

![](run-installer.png)

You may now see another Smart Screen message syaing “Windows protected your PC”

![](windows-protected-your-pc.png)

If you click on **More info** it will then show the option to **Run anyway** if you click this option the installer will now run.

![](install-anyway.png)

The installer is now running and you can choose to installer for All Users or just for the current user.

![](install-mode.png)

The install of DAX Studio should now proceed normally.

![](installed.png)