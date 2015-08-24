
function IsExcel2010Installed(): Boolean;
 var
  key: string;
  
begin
    //HKEY_CLASSES_ROOT\Excel.Application\CurVer

    //if RegQueryDWordValue(HKCR, 'Excel.Application\\CurVer\\','', CurVer) then
  if RegQueryStringValue(HKCR, 'Excel.Application\CurVer\','', key) then
  begin
    // Successfully read the value
      //MsgBox('Excel Version: ' + key,mbInformation, MB_OK);
      if key = 'Excel.Application.14' then begin
        Result := true;
      end else begin
        Result := false;
      end;
  end 
  else begin
      //MsgBox('Excel Not installed',mbInformation, MB_OK);
      Result := false;
  end 
  
end;