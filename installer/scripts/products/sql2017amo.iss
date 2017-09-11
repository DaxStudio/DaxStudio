// SQL Server 2012 SP1 AMO (Analysis Management Objects) assemblies are used
// to manage Analysis Services instances

[CustomMessages]
amo14_title=SQL Server 2017 Analysis Management Objects

en.amo14_size=4.1 MB
;de.amo_size=2,7 MB

en.amo14_size_x64=6.5 MB
;de.amo_x64=3,5 MB


[Code]
const
	amo14_url =     'https://go.microsoft.com/fwlink/?linkid=829578';
	amo14_url_x64 = 'https://go.microsoft.com/fwlink/?linkid=829578';
   
procedure sql2017amo();
var
//	version: string;
  maxVersion: string;
  new_amo14_url: string;
  new_amo14_url_x64: string;
begin
	//CHECK NOT FINISHED YET

  maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices');
  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);

  // if maxVersion is less than 13.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'14.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices', '14.0.0.0' ) ) And IsExcel2010Installed()) then begin
    
    // get download locations
    new_amo14_url := getRedirect( amo14_url);
    new_amo14_url_x64 := getRedirect( amo14_url_x64);
		
    if (not IsIA64()) then
			AddProduct('SQL_AS_AMO.msi',
				' /passive',
				CustomMessage('amo14_title'),
				CustomMessage('amo14_size' + GetArchitectureString()),
				GetString(new_amo14_url, new_amo14_url_x64, ''),
				false, false);
	end;
end;
