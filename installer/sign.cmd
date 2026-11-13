@echo off
rem Sign Tool wrapper invoked by ISCC (see SignTool in DaxStudio.iss and the
rem InstallerWithSignedAssemblies target in build.msproj). Credentials are read
rem from environment variables so they are not echoed in the ISCC build output.
rem Usage: sign.cmd "<file to sign>"
AzureSignTool sign -kvu "https://daxstudiocert.vault.azure.net/" -kvi "%KVI_PARAMETER%" -kvs "%KVS_PARAMETER%" -kvc "%KVC_PARAMETER%" -kvt "%KVT_PARAMETER%" -tr "http://timestamp.globalsign.com/tsa/r45standard" -td sha256 %1
exit /b %ERRORLEVEL%
