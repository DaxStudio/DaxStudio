[Code]

// looking for version strings like the following:
// Microsoft.AnalysisServices.AdomdClient,fileVersion="10.0.2531.0",version="10.0.0.0000",culture="neutral",publicKeyToken="89845DCD8080CC91",processorArchitecture="MSIL"

function IsAssemblyInstalled(assemblyName: String; versionString: String): boolean;
var
  J: Integer;
  RegKey: string;
  Names: TArrayOfString;
begin
  RegKey := '\Installer\Assemblies\Global';
  Result := False;

  If  RegKeyExists(HKEY_CLASSES_ROOT, RegKey) then
  begin  
    if RegGetValueNames(HKEY_CLASSES_ROOT, RegKey, Names) then
    begin

      // loop through any disabled add-ins and delete
      // any keys that reference Dax Studio
      for J := 0 to GetArrayLength(Names)-1 do
      begin
        // comma after assembly name prevents partial matches
        if (Pos( assemblyName + ',', Names[J]) > 0) AND (Pos( 'version="' + versionString, Names[J]) > 0) then
        begin  
            //MsgBox( 'Found key: ' + Names[J], mbInformation ,MB_OK)
            Result := True;
            exit;
        end;
      end;
    end;
    
  end;
end;




// Compare version numbers by "walking" down the parts of the version number
//  <major>.<minor>.<revision>.<build> 
// source: http://blog.lextudio.com/2007/09/inno-setup-script-sample-for-version-comparison-advanced-version/
function GetNumber(var temp: String): Integer;
var
  part: String;
  pos1: Integer;
begin
  if Length(temp) = 0 then
  begin
    Result := -1;
    Exit;
  end;
    pos1 := Pos('.', temp);
    if (pos1 = 0) then
    begin
      Result := StrToInt(temp);
      temp := '';
    end
    else
    begin
      part := Copy(temp, 1, pos1 - 1);
      temp := Copy(temp, pos1 + 1, Length(temp));
      Result := StrToInt(part);
    end;
end;
 
function CompareInner(var temp1, temp2: String): Integer;
var
  num1, num2: Integer;
begin
  num1 := GetNumber(temp1);
  num2 := GetNumber(temp2);

  if (num1 = -1) or (num2 = -1) then
  begin
    Result := 0;
    Exit;
  end;
      if (num1 > num2) then
      begin
        Result := 1;
      end
      else if (num1 < num2) then
      begin
        Result := -1;
      end
      else
      begin
        Result := CompareInner(temp1, temp2);
      end;
end;
 
function CompareAssemblyVersion(str1, str2: String): Integer;
var
  temp1, temp2: String;
begin
    temp1 := str1;
    temp2 := str2;
    if ((length(trim(temp1)) = 0) AND (length(trim(temp2)) > 0)) then
    begin
      Result := -1;
      exit;
    end;

    if ((length(trim(temp1)) > 0) AND (length(trim(temp2)) = 0)) then
    begin
      Result := 1;
      exit;
    end;


    Result := CompareInner(temp1, temp2);
end;


function GetMaxAssemblyVersion(assemblyName: String): String;
var
  
  J: Integer;
  RegKey: string;
  Names: TArrayOfString;
//  ResultStr: AnsiString;
  verStr: string;
  verStart: Integer;
  verEnd: Integer;
  tmp: String;
  maxVer: String;
begin
  RegKey := '\Installer\Assemblies\Global';
  
  //found := False;
  // for each version of Excel
  Result := '';
  maxVer := '';

  Log('Entering GetMaxAssemblyVersion()');

  If  RegKeyExists(HKEY_CLASSES_ROOT, RegKey) then
  begin  
    Log('  Found RegKey : \Installer\Assemblies\Global' );
    if RegGetValueNames(HKEY_CLASSES_ROOT, RegKey, Names) then
    begin
      Log('  Processing Array of Values ');
      if  (GetArrayLength(Names) > 0) then
        begin
          // loop through any disabled add-ins and delete
          // any keys that reference Dax Studio
          
          for J := 0 to GetArrayLength(Names)-1 do
          begin

            // comma after assembly name prevents partial matches
            if (Pos( assemblyName + ',', Names[J]) > 0) then
            begin  
                try
                  verStart := Pos('version="',Names[J]);
                  if (verStart > 0) then
                  begin
                    tmp := Copy(Names[J],verStart+9,10000);
                    verEnd := Pos('"',tmp);
                    if (verEnd > 0) then
                    begin
                      verStr := Copy(tmp,0,verEnd-1)

                      Log( '    Found key: ' + Names[J] + ' version= ' + verStr);
                      
                      if (CompareAssemblyVersion(verStr, maxVer) > 0) then
                      begin
                        maxVer:=verStr;
                        Log('    GetMaxAssemblyVersion: New max ' + verStr);
                      end;
                      
                      Result := maxVer;
                      //exit;
                    end; // verEnd > 0
                  end; // verStart > 0
                except
                    // Catch the exception, show it, and continue
                    ShowExceptionMessage;
                end; // end try
            end; // if (Pos( assemblyName + ',', Names[J]) > 0) then
          end; // for loop
        end
      else begin
         Log('    No keys found under \\HKEY_CLASSES_ROOT\Installer\Assemblies\Global');
      end;
    end;
    
  end;

  Log('Exiting GetMaxAssemblyVersion()');
end;



function GetAllAssemblyVersions(assemblyName: String): array of String;
var
  
  J: Integer;
  RegKey: string;
  Names: TArrayOfString;
  verStr: string;
  verStart: Integer;
  verEnd: Integer;
  tmp: String;
  maxVer: String;
  len: Integer;
begin
  RegKey := '\Installer\Assemblies\Global';
  Log('Start: GetMaxAssemblyVersion(''' + assemblyName + ''')');
  
  //found := False;
  // for each version of Excel
  //Result := '';
  maxVer := '';
  len := 0;

  If  RegKeyExists(HKEY_CLASSES_ROOT, RegKey) then
  begin  
    if RegGetValueNames(HKEY_CLASSES_ROOT, RegKey, Names) then
    begin
      Log('  Processing Array of Values ');
      if  (GetArrayLength(Names) > 0) then
        begin
        // loop through any disabled add-ins and delete
        // any keys that reference Dax Studio
        for J := 0 to GetArrayLength(Names)-1 do
        begin
          //Log('    Processing: ' + Names[J]);
          // comma after assembly name prevents partial matches
          if (Pos( assemblyName + ',', Names[J]) > 0) then
          begin  
              verStart := Pos('version="',Names[J]);
              if (verStart) > 0 then
              begin
                tmp := Copy(Names[J],verStart+9,10000);
                verEnd := Pos('"',tmp);
                verStr := Copy(tmp,0,verEnd-1);

                Log( '    Found key: ' + Names[J] + ' version= ' + verStr);

                SetArrayLength(Result, len + 1);
                Result[len] := verStr;
                len := len + 1;
              end; // end If verStart > 0
          end;  // end if Pos ','
        end;  // For J
      end; // end If GetArrayLength(Names) > 0
    end; // end If GetRegValueNames()
    
  end; // end If regKeyExists
  Log('End: GetMaxAssemblyVersion(''' + assemblyName + ''')');
end;



function GetMaxCommonSsasAssemblyVersionInternal(): String;
var 
  amo: array of String;
  adomd: array of String;
  amoLen: Integer;
  adomdLen: Integer;
  I: Integer;
  J: Integer;
begin
  Log('Start: GetMaxAssemblyVersionInternal()');
  amo := GetAllAssemblyVersions('Microsoft.AnalysisServices');
  adomd := GetAllAssemblyVersions('Microsoft.AnalysisServices.AdomdClient');

  amoLen := GetArrayLength(amo);
  adomdLen := GetArrayLength(adomd);
  Result := '';
  for I := 0 to amoLen - 1 do
  begin
    for J := 0 to adomdLen - 1 do
    begin
      if (amo[I] = adomd[J]) then
      begin
        Result := amo[I];
      end;
    end;
  end;
  Log('End: GetMaxAssemblyVersionInternal() Result=' + Result );
end;
