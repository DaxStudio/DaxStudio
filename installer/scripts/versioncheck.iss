[CustomMessages]
en.NewerVersionInstalled=DAX Studio %1 is already installed. Setup cannot install the older version %2. Uninstall the newer version first if you want to downgrade.
en.InstalledVersionUnreadable=Setup could not determine the installed DAX Studio version (%1). Repair or uninstall the existing installation before continuing.

[Code]
function InstalledVersionError(InstalledVersion: String): String;
var
  Installed, Incoming: Int64;
begin
  Result := '';
  if (Trim(InstalledVersion) = '') or not StrToVersion(InstalledVersion, Installed) then
    Result := FmtMessage(CustomMessage('InstalledVersionUnreadable'), [InstalledVersion])
  else begin
    if not StrToVersion('{#MyAppVersion}', Incoming) then
      RaiseException('Invalid installer version: {#MyAppVersion}');
    if ComparePackedVersion(Installed, Incoming) > 0 then
      Result := FmtMessage(CustomMessage('NewerVersionInstalled'), [InstalledVersion, '{#MyAppVersion}']);
  end;
end;

function RegisteredVersionError(RootKey: Integer; UninstallKey: String): String;
var
  InstalledVersion: String;
begin
  Result := '';
  if not RegKeyExists(RootKey, UninstallKey) then
    Exit;

  if not RegQueryStringValue(RootKey, UninstallKey, 'DisplayVersion', InstalledVersion) then
    Result := FmtMessage(CustomMessage('InstalledVersionUnreadable'), [UninstallKey])
  else
    Result := InstalledVersionError(InstalledVersion);
end;

function InstalledDaxStudioVersionError(): String;
var
  UninstallKey: String;
begin
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' +
    ExpandConstant('{#SetupSetting("AppId")}') + '_is1';
  Result := RegisteredVersionError(HKLM32, UninstallKey);
  if Result = '' then
    Result := RegisteredVersionError(HKCU32, UninstallKey);
  if IsWin64 then begin
    if Result = '' then
      Result := RegisteredVersionError(HKLM64, UninstallKey);
    if Result = '' then
      Result := RegisteredVersionError(HKCU64, UninstallKey);
  end;
  if Result <> '' then
    Log(Result);
end;

function TargetDaxStudioVersionError(): String;
var
  ExePath, InstalledVersion: String;
begin
  Result := '';
  ExePath := ExpandConstant('{app}\{#MyAppExeName}');
  if not FileExists(ExePath) then
    Exit;

  if not GetVersionNumbersString(ExePath, InstalledVersion) then
    Result := FmtMessage(CustomMessage('InstalledVersionUnreadable'), [ExePath])
  else
    Result := InstalledVersionError(InstalledVersion);
  if Result <> '' then
    Log(Result);
end;
