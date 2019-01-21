// requires Windows 7 Service Pack 1, Windows 8, Windows 8.1, Windows Server 2008 R2 SP1, Windows Server 2008 Service Pack 2, Windows Server 2012, Windows Server 2012 R2, Windows Vista Service Pack 2
// WARNING: express setup (downloads and installs the components depending on your OS) if you want to deploy it on cd or network download the full bootsrapper on website below
// http://www.microsoft.com/en-us/download/details.aspx?id=42642

[CustomMessages]
dotnetfx471_title=.NET Framework 4.7.1
dotnetfx472_title=.NET Framework 4.7.2

dotnetfx471_size=1 MB - 124 MB
dotnetfx472_size=1 MB - 124 MB

;http://www.microsoft.com/globaldev/reference/lcid-all.mspx
en.dotnetfx47_lcid=''
;de.dotnetfx45_lcid='/lcid 1031 '


[Code]
const
  dotnetfx471_url = 'http://download.microsoft.com/download/8/E/2/8E2BDDE7-F06E-44CC-A145-56C6B9BBE5DD/NDP471-KB4033344-Web.exe';
	dotnetfx472_url = 'http://download.microsoft.com/download/0/5/C/05C1EC0E-D5EE-463B-BFE3-9311376A6809/NDP472-KB4054531-Web.exe';
                     
procedure dotnetfx47(MinVersion: integer);
begin
	if (not netfxinstalled(NetFx47, '') or (netfxspversion(NetFx47, '') < MinVersion)) then
		AddProduct('dotnetfx471.exe',
			CustomMessage('dotnetfx47_lcid') + '/q /passive /norestart',
			CustomMessage('dotnetfx471_title'),
			CustomMessage('dotnetfx471_size'),
			dotnetfx471_url,
			false, false);
end;