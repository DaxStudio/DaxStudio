// SQL Server ADOMDClient is required to query Analysis Services

[CustomMessages]
adomdclient_title=SQL Server 2017 ADOMD.NET

en.adomdclient_size=3.1 MB
;de.adomdclient_size=3,0 MB

en.adomdclient_size_x64=4.9 MB
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

  if WinHttpReq.Status <> 200 then begin
    MsgBox('Could not run service to get encrypted password.', mbError, MB_OK);
  end else
  begin
  //MsgBox('SUCCESS', mbInformation, MB_OK);
  end; 
  if Length(WinHttpReq.ResponseText) > 0 then begin
    MsgBox('Response: ' + WinHttpReq.ResponseText, mbError, MB_OK);
  end;
  Result := 'hello';
end;

  
procedure sql2017adomdClient();
var
	maxVersion: string;
  newurl: string;
begin
	//CHECK NOT FINISHED YET

	maxVersion := GetMaxAssemblyVersion('Microsoft.AnalysisServices.AdomdClient');

  //msgbox('Compare adomdclient ' + IntToStr(CompareAssemblyVersion(maxVersion ,'11.0.0.0000')),mbInformation,MB_OK);
    newurl := getRedirect( adomdclient14_url);
  // if maxVersion is less than 13.0.0.0000
	if (CompareAssemblyVersion(maxVersion ,'14.0.0.0000') < 0 ) or (( not IsAssemblyInstalled('Microsoft.AnalysisServices.AdomdClient', '14.0.0.0' ) ) And IsExcel2010Installed()) then begin
    
		if (not IsIA64()) then
			AddProduct('SQL_AS_ADOMD.msi',
				' /passive',
				CustomMessage('adomdclient_title'),
				CustomMessage('adomdclient_size' + GetArchitectureString()),
				GetString(adomdclient14_url, adomdclient14_url_x64, ''),
				false, false);
	end;
end;




//end;