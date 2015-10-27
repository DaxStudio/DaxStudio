// SQL Server 2012 SP1 AMO (Analysis Management Objects) assemblies are used
// to manage Analysis Services instances

[CustomMessages]
amo_title=SQL Server 2012 Analysis Management Objects

en.amo_size=2.7 MB
;de.amo_size=2,7 MB

en.amo_size_x64=3.5 MB
;de.amo_x64=3,5 MB


[Code]
const
	amo_url =     'http://download.microsoft.com/download/4/B/1/4B1E9B0E-A4F3-4715-B417-31C82302A70A/ENU/x86/SQL_AS_AMO.msi';
	amo_url_x64 = 'http://download.microsoft.com/download/4/B/1/4B1E9B0E-A4F3-4715-B417-31C82302A70A/ENU/x64/SQL_AS_AMO.msi';
   

procedure sql2012sp1amo();
var
//	version: string;
  maxVersion: string;
begin
	//CHECK NOT FINISHED YET

  maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices');
  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);

  // if maxVersion is less than 11.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'11.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices', '11.0.0.0' ) ) And IsExcel2010Installed()) then begin

		if (not IsIA64()) then
			AddProduct('SQL_AS_AMO.msi',
				' /passive',
				CustomMessage('amo_title'),
				CustomMessage('amo_size' + GetArchitectureString()),
				GetString(amo_url, amo_url_x64, ''),
				false, false);
	end;
end;
