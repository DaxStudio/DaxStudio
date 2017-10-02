@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)

rem %WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild build.msproj /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /target:Installer
"C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe" build.msproj /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /target:Installer /p:DefineConstants="BETA"
pause