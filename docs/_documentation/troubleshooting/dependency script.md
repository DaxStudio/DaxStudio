---
title: Dependency Checks
---

> **Note:** The functionality of the script below has been wrapped up in a small program called [DAX Studio Checker](../daxstudio-checker) which is installed as part of DAX Studio. It has some extended functionality which can check for configuration mismatches. It covers all the items in the legacy script below and more.

The following will print out the versions of the external dependencies. You can just copy and paste it into the PowerShell ISE.
{% highlight powershell %}
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP' -recurse `
|Get-ItemProperty -name Version -EA 0 `
|Where { $_.PSChildName -match '^(?!S)\p{L}'} `
|Select PSChildName, Version
"----"
$assAdomd = [System.Reflection.Assembly](System.Reflection.Assembly)::LoadWithPartialName("Microsoft.AnalysisServices.adomdclient")
"ADOMD: $($assAdomd.fullname)"

$assAmo = [System.Reflection.Assembly](System.Reflection.Assembly)::LoadWithPartialName("Microsoft.AnalysisServices")
"AMO: $($assAmo.fullname)"
{% endhighlight %}}

You should see output like the following:
<pre>
PSChildName                            Version
-----------                            -------
v2.0.50727                             2.0.50727.4927              
v3.0                                   3.0.30729.4926
Windows Communication Foundation       3.0.4506.4926
Windows Presentation Foundation        3.0.6920.4902
v3.5                                   3.5.30729.4926
Client                                 4.5.51641
Full                                   4.5.51641
Client                                 4.0.0.0
----
ADOMD: Microsoft.AnalysisServices.adomdclient, Version=11.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91
AMO: Microsoft.AnalysisServices, Version=11.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91
</pre>

The important parts of the above output is that the .Net version list should include a 4.5 version and the AMO and ADOMD should be at least 11.0.0.0 