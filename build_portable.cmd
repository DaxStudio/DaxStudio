@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)

rem %WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild build.msproj /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /target:Installer
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\msbuild.exe" build.msproj /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /target:Portable
pause