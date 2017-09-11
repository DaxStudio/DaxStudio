// SQL Server ADOMDClient is required to query Analysis Services

[CustomMessages]
adomdclient14_title=SQL Server 2017 ADOMD.NET

en.adomdclient14_size=3.1 MB
;de.adomdclient_size=3,0 MB

en.adomdclient14_size_x64=4.9 MB
;de.adomdclient_x64=5,7 MB

;// same links work with https
[Code]
const
	adomdclient14_url =     'https://go.microsoft.com/fwlink/?linkid=829577';
	adomdclient14_url_x64 = 'https://go.microsoft.com/fwlink/?linkid=829577';

function getRedirect(url: string):string;var
  winHttpReq: Variant;
begin
  WinHttpReq := CreateOleObject('WinHttp.WinHttpRequest.5.1');

  WinHttpReq.Open('GET', url, false);
  WinHttpReq.Send();

  if WinHttpReq.Status = 302 then begin
      Log(WinHttpReq.getResponseHeader('Location'));
      Log(WinHttpReq.Status);
      Result := WinHttpReq.getResponseHeader('Location');
    end 
  else
    begin
      Result := '';
    end; 
end;

  
procedure sql2017adomdClient();
var
	maxVersion: string;
  new_adomdclient14_url: string;
  new_adomdclient14_url_x64: string;
begin
	//CHECK NOT FINISHED YET

	maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices.AdomdClient');

  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);
  // if maxVersion is less than 13.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'14.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices.AdomdClient', '14.0.0.0' ) ) And IsExcel2010Installed()) then begin
    Log('Adding Product SQL 2017 ADOMDClient');
    // get download locations
    new_adomdclient14_url := getRedirect( adomdclient14_url);
    new_adomdclient14_url_x64 := getRedirect( adomdclient14_url_x64);

		if (not IsIA64()) then
			AddProduct('SQL_AS_ADOMD.msi',
				' /passive',
				CustomMessage('adomdclient14_title'),
				CustomMessage('adomdclient14_size' + GetArchitectureString()),
				GetString(new_adomdclient14_url, new_adomdclient14_url_x64, ''),
				false, false);
	end;
end;




//end;