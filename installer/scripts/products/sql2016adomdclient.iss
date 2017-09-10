// SQL Server ADOMDClient is required to query Analysis Services

[CustomMessages]
adomdclient13_title=SQL Server 2016 ADOMD.NET

en.adomdclient13_size=3.1 MB
;de.adomdclient_size=3,0 MB

en.adomdclient13_size_x64=4.9 MB
;de.adomdclient_x64=5,7 MB

;// same links work with https
[Code]
const
	adomdclient13_url =     'http://download.microsoft.com/download/8/7/2/872BCECA-C849-4B40-8EBE-21D48CDF1456/ENU/x86/SQL_AS_ADOMD.msi';
	adomdclient13_url_x64 = 'http://download.microsoft.com/download/8/7/2/872BCECA-C849-4B40-8EBE-21D48CDF1456/ENU/x64/SQL_AS_ADOMD.msi';
  
procedure sql2016adomdClient();
var
	maxVersion: string;
begin
	//CHECK NOT FINISHED YET

	maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices.AdomdClient');

  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);

  // if maxVersion is less than 13.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'13.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices.AdomdClient', '13.0.0.0' ) ) And IsExcel2010Installed()) then begin
    Log('Adding Product SQL 2016 ADOMDClient');
		if (not IsIA64()) then
			AddProduct('SQL_AS_ADOMD.msi',
				' /passive',
				CustomMessage('adomdclient13_title'),
				CustomMessage('adomdclient13_size' + GetArchitectureString()),
				GetString(adomdclient13_url, adomdclient13_url_x64, ''),
				false, false);
	end;
end;
