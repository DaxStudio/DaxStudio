// SQL Server ADOMDClient is required to query Analysis Services

[CustomMessages]
adomdclient_title=SQL Server 2016 ADOMD.NET

en.adomdclient_size=3.1 MB
;de.adomdclient_size=3,0 MB

en.adomdclient_size_x64=4.9 MB
;de.adomdclient_x64=5,7 MB

;// same links work with https
[Code]
const
	adomdclient_url =     'http://download.microsoft.com/download/8/7/2/872BCECA-C849-4B40-8EBE-21D48CDF1456/ENU/x86/SQL_AS_ADOMD.msi';
	adomdclient_url_x64 = 'http://download.microsoft.com/download/8/7/2/872BCECA-C849-4B40-8EBE-21D48CDF1456/ENU/x64/SQL_AS_ADOMD.msi';
  
procedure sql2016adomdClient();
var
	maxVersion: string;
begin
	//CHECK NOT FINISHED YET

	maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices.AdomdClient');

  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);

  // if maxVersion is less than 13.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'13.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices.AdomdClient', '13.0.0.0' ) ) And IsExcel2010Installed()) then begin
  
		if (not IsIA64()) then
			AddProduct('SQL_AS_ADOMD.msi',
				' /passive',
				CustomMessage('adomdclient_title'),
				CustomMessage('adomdclient_size' + GetArchitectureString()),
				GetString(adomdclient_url, adomdclient_url_x64, ''),
				false, false);
	end;
end;
