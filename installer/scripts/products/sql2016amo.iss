// SQL Server 2012 SP1 AMO (Analysis Management Objects) assemblies are used
// to manage Analysis Services instances

[CustomMessages]
amo13_title=SQL Server 2016 Analysis Management Objects

en.amo13_size=4.1 MB
;de.amo_size=2,7 MB

en.amo13_size_x64=6.5 MB
;de.amo_x64=3,5 MB


[Code]
const
	amo13_url =     'http://download.microsoft.com/download/8/7/2/872BCECA-C849-4B40-8EBE-21D48CDF1456/ENU/x86/SQL_AS_AMO.msi';
	amo13_url_x64 = 'http://download.microsoft.com/download/8/7/2/872BCECA-C849-4B40-8EBE-21D48CDF1456/ENU/x64/SQL_AS_AMO.msi';
   
procedure sql2016amo();
var
//	version: string;
  maxVersion: string;
begin
	//CHECK NOT FINISHED YET

  maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices');
  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);

  // if maxVersion is less than 13.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'13.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices', '13.0.0.0' ) ) And IsExcel2010Installed()) then begin

		if (not IsIA64()) then
			AddProduct('SQL_AS_AMO.msi',
				' /passive',
				CustomMessage('amo13_title'),
				CustomMessage('amo13_size' + GetArchitectureString()),
				GetString(amo13_url, amo13_url_x64, ''),
				false, false);
	end;
end;
