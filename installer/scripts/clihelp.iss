// ----------------------------------------------------------------------------
//
// Inno Setup Ver:	5.4.2
// Script Version:	1.4
// Author:			Jared Breland <jbreland@legroom.net>
// Homepage:		http://www.legroom.net/mysoft
// License:			GNU Lesser General Public License (LGPL), version 3
//						http://www.gnu.org/licenses/lgpl.html
//
// Script Function:
//	Disaply command line arguments available for Inno Setup installers
//
// Instructions:
//	Copy clihelp.iss to the same directory as your setup script
//
//	Add the following to the end of your [Code] section
//	Constants should be set according to the instructions below
//		const
//			ComponentList = '';
//			TaskList = '';
//			ParameterList = '';
//		#include "clihelp.iss"
//
//	The ComponentList, TaskList, and ParameterList constansts must follow a
//	specific naming convention:
//		Name1 - Description1 | Name2 - Description2
//
//	Use a hyphon (-) to separate the name from the description, and use a pipe
//	(|) to delineate between each item.  For example;
//		ComponentList = '';
//		TaskList = 'associate - Enable context menu integration | modifypath - Add application to your system path | desktopicon - Create a desktop icon';
//		ParameterList = '/NOHISTORY - Disables history functionality.';
//
//	This states that there are no components available for this app, three
//	tasks are available (associate, modifypath, and desktopicon), and it
//	supports a custom '/NOHISTORY' parameter.
//
// ----------------------------------------------------------------------------


const 
   ComponentList = 'CORE - core components| EXCEL - Excel Addin'; 
   TaskList = 'DESKTOPICON - adds a desktop icon'; 
   ParameterList = '/SKIPDEPENDENCIES=True/False - Skips the standard dependency checks';  

// Define Escape key and form controls
const
	VK_ESCAPE = $1B;
var
	frmHelp:		TForm;
	rtfHelpText:	TRichEditViewer;

// Split a string into an array using passed delimeter
procedure Explode(var Dest: TArrayOfString; Text: String; Separator: String);
var
	i: Integer;
begin
	i := 0;
	repeat
		SetArrayLength(Dest, i+1);
		if Pos(Separator,Text) > 0 then	begin
			Dest[i] := Copy(Text, 1, Pos(Separator, Text)-1);
			Text := Copy(Text, Pos(Separator,Text) + Length(Separator), Length(Text));
			i := i + 1;
		end else begin
			 Dest[i] := Text;
			 Text := '';
		end;
	until Length(Text)=0;
end;

// Set available tasks and columns list to a common width
function StringPad(var curStr: String; var maxLen: Integer): String;
var
	i: Integer;
	output: String;
begin
	output := '';
	StringChange(curStr, '\\', '\');
	for i := 0 to maxLen - Length(curStr) do begin
		output := output + ' ';
	end;
	Result := output;
end;

// Italicize parameter arguments (anything to the right side of a =)
function ItalicizeArgs(var curStr: String): String;
var
	i: Integer;
	tmpArr: TArrayOfString;
	output: String;
begin
	output := curStr;
	Explode(tmpArr, curStr, '=');
	if GetArrayLength(tmpArr) > 1 then begin
		output := tmpArr[0] + '=\i '
		for i := 1 to GetArrayLength(tmpArr)-1 do begin
			output := output + tmpArr[i];
		end;
		output := output + ' \i0 '
	end;
	Result := output;
end;
// Instruct help box to close on Escape
procedure HelpFormOnKeyDown(Sender: TObject; var Key: Word; Shift: TShiftState);
begin
	if (Key = VK_ESCAPE) then
	begin
		frmHelp.Close;
	end
end;

// Resize text box as window is resized
procedure HelpFormOnResize(Sender: TObject);
begin
	rtfHelpText.Width := TForm(Sender).ClientWidth;
	rtfHelpText.Height := TForm(Sender).ClientHeight;
end;

// Generate list of available tasks, components, and custom parameters
function HelpFormListOptions(listType: String): String;
var
	i, maxLen:	Integer;
	listStr:	String;
	listArr, listItem:	Array of String;

begin
	if listType = 'Components' then begin
		listStr := ComponentList;
	end else if listType = 'Tasks' then begin
		listStr := TaskList;
	end else if listType = 'Parameters' then begin
		listStr := ParameterList;
	end else begin
		Result := '\cf2\b No Available ' + listType + '  \cf0\b0\par'#13'\par'#13#10
	end;

	// Generate component and task lists
	if (listType = 'Components') OR (listType = 'Tasks') then begin
		if Length(listStr) > 0 then begin
			Explode(listArr, listStr, '|');
			Result := '\cf1\b Available ' + listType + ':  \cf0\b0\par\li568'#13
			maxLen := 0;
			for i:=0 to GetArrayLength(listArr)-1 do begin
				Explode(listItem, Trim(listArr[i]), ' - ');
				if Length(listItem[0]) > maxLen then
					maxLen := Length(listItem[0]);
			end;
			for i:=0 to GetArrayLength(listArr)-1 do begin
				StringChange(listArr[i], '\', '\\');
				Explode(listItem, Trim(listArr[i]), ' - ');
				Result := Result + '\f1 ' + listItem[0] + StringPad(listItem[0], maxLen) + ' \f0 ' + listItem[1] + '\par'#13;
			end;
			Result := Result + '\par'#13#10;
		end else begin
			Result := '\cf2\b No Available ' + listType + '  \cf0\b0\par'#13'\par'#13#10
		end;

	// Generate custom parameter stanzas
	end else if listType = 'Parameters' then begin
		if Length(listStr) > 0 then begin
			Explode(listArr, listStr, '|');
			Result := '\cf1\b Available Custom Parameters  \cf0\b0\par'#13#10'\pard\par'#13#10
			for i:=0 to GetArrayLength(listArr)-1 do begin
				StringChange(listArr[i], '\', '\\');
				Explode(listItem, Trim(listArr[i]), ' - ');
				Result := Result + '\b ' + ItalicizeArgs(listItem[0]) + '\par\li284\b0 '#13#10 + listItem[1] + '\par'#13#10'\pard\par'#13#10
			end;
		end else begin
			Result := '\cf2\b No Available Custom Parameters  \cf0\b0\par'#13#10'\pard\par'#13#10
		end;
	end;
end;


// Display command line usage when help option passed
procedure DisplayHelp();
var
//	i, maxLen:	Integer;
	RTFHeaderM, RTFBody:	String;
//	compArr, compItem, taskArr, taskItem:	Array of String;

begin
	// Setup help message box
	RTFHeaderM := '{\rtf1\deff0{\fonttbl{\f0\fswiss\fprq2\fcharset0 Tahoma;}{\f1\fmodern\fprq2\fcharset128 Courier New;}}{\colortbl ;\red0\green0\blue255;\red255\green0\blue0;}\viewkind4\uc1\fs20';
	frmHelp := TForm.Create(nil);
	with frmHelp do begin
		ClientWidth := ScaleX(780);
		ClientHeight := ScaleX(560);
		BorderIcons := [biSystemMenu];
		BorderStyle := bsSizeable;
		Caption := 'Inno Setup Command Line Parameters';
		Position := poScreenCenter;
		KeyPreview := True;
		OnResize := @HelpFormOnResize;
		OnKeyDown := @HelpFormOnKeyDown;
	end;

	// Build help text body
	RTFBody := 'The Setup program accepts optional command line parameters. These can be useful to system administrators, and to other programs calling the Setup program.\par'+#13#10'\pard\par'#13#10+

		'\b /SP-\par\li284\b0 '#13#10+
		'Disables the \i This will install... Do you wish to continue? \i0 prompt at the beginning of Setup. Of course, this will have no effect if the \f1DisableStartupPrompt [Setup]\f0  section directive was set to \f1yes\f0.\par'#13#10'\pard\par'#13#10+

		'\b /SILENT, /VERYSILENT\par\li284\b0 '#13#10+
		'Instructs Setup to be silent or very silent. When Setup is silent the wizard and the background window are not displayed but the installation progress window is. When a setup is very silent this installation progress window is not displayed. Everything else is normal so for example error messages during installation are displayed and the startup prompt is (if you haven''t disabled it with \f1DisableStartupPrompt\f0  or the ''/SP-'' command line option explained above)\par'#13#10'\par'#13#10+
		'If a restart is necessary and the ''/NORESTART'' command isn''t used (see below) and Setup is silent, it will display a \i Reboot now? \i0 message box. If it''s very silent it will reboot without asking.\par'#13#10'\pard\par'#13#10+

		'\b /SUPPRESSMSGBOXES\par\li284\b0 '#13#10+
		'Instructs Setup to suppress message boxes. Only has an effect when combined with ''/SILENT'' and ''/VERYSILENT''.\par'#13#10'\par'#13#10+
		'The default response in situations where there''s a choice is:\par'#13+
		'-Yes in a ''Keep newer file?'' situation.\par'#13+
		'-No in a ''File exists, confirm overwrite.'' situation.\par'#13+
		'-Abort in Abort/Retry situations.\par'#13+
		'-Cancel in Retry/Cancel situations.\par'#13+
		'-Yes (=continue) in a DiskSpaceWarning/DirExists/DirDoesntExist/NoUninstallWarning/ExitSetupMessage/ConfirmUninstall situation.\par'#13+
		'-Yes (=restart) in a FinishedRestartMessage/UninstalledAndNeedsRestart situation.\par'#13#10'\par'#13#10+
		'5 message boxes are not suppressible:\par'#13+
		'-The About Setup message box.\par'#13+
		'-The Exit Setup? message box.\par'#13+
		'-The FileNotInDir2 message box displayed when Setup requires a new disk to be inserted and the disk was not found.\par'#13+
		'-Any (error) message box displayed before Setup (or Uninstall) could read the command line parameters.\par'#13+
		'-Any message box displayed by [Code] support function \f1MsgBox\f0.\par'#13#10'\pard\par'#13#10+

		'\b /LOG\par\li284\b0 '#13#10+
		'Causes Setup to create a log file in the user''s TEMP directory detailing file installation and [Run] actions taken during the installation process. This can be a helpful debugging aid. For example, if you suspect a file isn''t being replaced when you believe it should be (or vice versa), the log file will tell you if the file was really skipped, and why.\par'#13#10'\par'#13#10+
		'The log file is created with a unique name based on the current date. (It will not overwrite or append to existing files.)\par'#13#10'\par'#13#10+
		'The information contained in the log file is technical in nature and therefore not intended to be understandable by end users. Nor is it designed to be machine-parseable; the format of the file is subject to change without notice.\par'#13#10'\pard\par'#13#10+

		'\b /LOG="\i filename \i0 "\par\li284\b0 '#13#10+
		'Same as /LOG, except it allows you to specify a fixed path/filename to use for the log file. If a file with the specified name already exists it will be overwritten. If the file cannot be created, Setup will abort with an error message.\par'#13#10'\pard\par'#13#10+

		'\b /NOCANCEL\par\li284\b0 '#13#10+
		'Prevents the user from cancelling during the installation process, by disabling the Cancel button and ignoring clicks on the close button. Useful along with ''/SILENT'' or ''/VERYSILENT''.\par'#13#10'\pard\par'#13#10+

		'\b /NORESTART\par\li284\b0 '#13#10+
		'Prevents Setup from restarting the system following a successful installation, or after a \i Preparing to Install \i0 failure that requests a restart. Typically used along with /SILENT or /VERYSILENT.\par'#13#10'\pard\par'#13#10+

		'\b /RESTARTEXITCODE=\i exit code \i0\par\li284\b0 '#13#10+
		'Specifies a custom exit code that Setup is to return when the system needs to be restarted following a successful installation. (By default, 0 is returned in this case.) Typicaly used along with with ''/NORESTART''.\par'#13#10'\pard\par'#13#10+

		'\b /LOADINF="\i filename \i0 "\par\li284\b0 '#13#10+			
		'Instructs Setup to load the settings from the specified file after having checked the command line. This file can be prepared using the ''/SAVEINF='' command as explained below.\par'#13#10'\par'#13#10+
		'Don''t forget to use quotes if the filename contains spaces.\par'#13#10'\pard\par'#13#10+

		'\b /SAVEINF="\i filename \i0 "\par\li284\b0 '#13#10+
		'Instructs Setup to save installation settings to the specified file.\par'#13#10'\par'#13#10+
		'Don''t forget to use quotes if the filename contains spaces.\par'#13#10'\pard\par'#13#10+

		'\b /LANG=\i language \i0\par\li284\b0 '#13#10+
		'Specifies the language to use. \i language \i0 specifies the internal name of the language as specified in a [Languages] section entry.\par'#13#10'\par'#13#10+
		'When a valid /LANG parameter is used, the \i Select Language \i0 dialog will be suppressed.\par'#13#10'\pard\par'#13#10+

		'\b /DIR="\i x:\\dirname \i0 "\par\li284\b0 '#13#10+
		'Overrides the default directory name displayed on the \i Select Destination Location \i0 wizard page. A fully qualified pathname must be specified. May include an "expand;" prefix which instructs Setup to expand any constants in the name.  For example: ''/DIR=expand:\{pf\}\\My Program''.\par'#13#10'\pard\par'#13#10+

		'\b /GROUP="\i folder name \i0"\par\li284\b0 '#13#10+
		'Overrides the default folder name displayed on the \i Select Start Menu Folder \i0 wizard page. May include an "expand:" prefix, see ''/DIR=''. If the \f1[Setup]\f0  section directive \f1DisableProgramGroupPage\f0  was set to \f1yes\f0, this command line parameter is ignored.\par'#13#10'\pard\par'#13#10+

		'\b /NOICONS\par\li284\b0 '#13#10+
		'Instructs Setup to initially check the \i Don''t create any icons \i0 check box on the \i Select Start Menu Folder \i0 wizard page.\par'#13#10'\pard\par'#13#10+

		'\b /TYPE=\i type name \i0\par\li284\b0 '#13#10+
		'Overrides the default setup type.\par'#13#10'\par'#13#10+
		'If the specified type exists and isn''t a custom type, then any /COMPONENTS parameter will be ignored.\par'#13#10'\pard\par'#13#10+

		'\b /COMPONENTS="\i comma separated list of component names \i0 "\par\li284\b0 '#13#10+
		'Overrides the default component settings. Using this command line parameter causes Setup to automatically select a custom type. If no custom type is defined, this parameter is ignored.\par'#13#10'\par'#13#10+
		'Only the specified components will be selected; the rest will be deselected.\par'#13#10'\par'#13#10+
		'If a component name is prefixed with a "*" character, any child components will be selected as well (except for those that include the \f1dontinheritcheck\f0  flag). If a component name is prefixed with a "!" character, the component will be deselected.\par'#13#10'\par'#13#10+
		'This parameter does not change the state of components that include the fixed flag.\par'#13#10'\par'#13#10+
		'\i Examples: \i0\par'#13#10'\par'#13#10+
		'Deselect all components, then select the "help" and "plugins" components:\par'#13+
		'/COMPONENTS="help,plugins"\par'#13#10'\par'#13#10+
		'Deselect all components, then select a parent component and all of its children with the exception of one:\par'#13+
		'/COMPONENTS="*parent,!parent\\child"\par'#13#10'\par'#13#10

	// Display available components for this installer
	RTFBody := RTFBody + HelpFormListOptions('Components');

	RTFBody := RTFBody + '\pard'#13#10'\b /TASKS="\i comma separated list of task names \i0 "\par\li284\b0 '#13#10+
		'Specifies a list of tasks that should be initially selected.\par'#13#10'\par'#13#10+
		'Only the specified tasks will be selected; the rest will be deselected. Use the /MERGETASKS parameter instead if you want to keep the default set of tasks and only select/deselect some of them.\par'#13#10'\par'#13#10+
		'If a task name is prefixed with a "*" character, any child tasks will be selected as well (except for those that include the dontinheritcheck flag). If a task name is prefixed with a "!" character, the task will be deselected.\par'#13#10'\par'#13#10+
		'\i Examples: \i0\par'#13#10'\par'#13#10+
		'Deselect all tasks, then select the "desktopicon" and "fileassoc" tasks:\par'#13+
		'/TASKS="desktopicon,fileassoc"\par'#13#10'\par'#13#10+
		'Deselect all tasks, then select a parent task item, but exclude one of its children:\par'#13+
		'/TASKS="parent,!parent\\child"\par'#13#10'\par'#13#10

	// Display available tasks for this installer
	RTFBody := RTFBody + HelpFormListOptions('Tasks');

	RTFBody := RTFBody + '\pard'#13#10'\b /MERGETASKS="\i comma separated list of task names \i0 "\par\li284\b0 '#13#10+
		'Like the /TASKS parameter, except the specified tasks will be merged with the set of tasks that would have otherwise been selected by default.\par'#13#10'\par'#13#10+
		'If UsePreviousTasks is set to yes, the specified tasks will be selected/deselected after any previous tasks are restored.\par'#13#10'\par'#13#10+
		'\i Examples: \i0\par'#13#10'\par'#13#10+
		'Keep the default set of selected tasks, but additionally select the "desktopicon" and "fileassoc" tasks\par'#13+
		'/MERGETASKS="desktopicon,fileassoc"\par'#13#10'\par'#13#10+
		'Keep the default set of selected tasks, but deselect the "desktopicon" task:\par'#13+
		'/MERGETASKS="!desktopicon"\par'#13#10'\par'#13#10

	// Display available tasks for this installer
	RTFBody := RTFBody + HelpFormListOptions('Tasks');

	RTFBody := RTFBody + '\pard'#13#10'\b /PASSWORD=\i password \i0\par\li284\b0 '#13#10+
		'Specifies the password to use. If the \f1[Setup]\f0  section directive \f1Password\f0  was not set, this command line parameter is ignored.\par'#13#10'\par'#13#10+
		'When an invalid password is specified, this command line parameter is also ignored.\par'#13#10'\pard\par'#13#10

	// Display custom parameters for this installer
	RTFBody := RTFBody + HelpFormListOptions('Parameters');
	RTFBody := RTFBody + '}';

	// Display rich text control
	rtfHelpText := TRichEditViewer.Create(frmHelp);
	with rtfHelpText do begin
		Parent := frmHelp;
		Left :=	0;
		Top := 0;
		Width := frmHelp.ClientWidth;
		Height := frmHelp.ClientHeight;
		Scrollbars := ssVertical;
		ReadOnly := True;
		UseRichEdit := True;
		RTFText := RTFHeaderM + RTFBody;
	end;
	frmHelp.ShowModal;
	frmHelp.Free;

	// Exit installer
	abort;
end;

// Changed to InitializeWizard() for Universal Extractor
// function InitializeSetup: Boolean;
//procedure InitializeWizard();
//end;

