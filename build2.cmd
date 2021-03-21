rem @echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)

"f:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\msbuild.exe" build.msproj /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /target:FullBuild
pause