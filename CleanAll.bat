@ECHO OFF
pushd "%~dp0"
ECHO.
ECHO.
ECHO.
ECHO This script deletes all temporary build files in their
ECHO corresponding BIN and OBJ Folder contained in the following projects
ECHO.
ECHO DaxStudio.Standalone
ECHO DaxStudio.ExcelAddin
ECHO DaxStudio.Checker
ECHO.
ECHO DaxStudio.UI
ECHO DaxStudio.Interfaces
ECHO DaxStudio.Common
ECHO DaxStudio.QueryTrace
ECHO DaxStudio.QueryTrace.Excel
ECHO UnitComboLib
ECHO.
ECHO.
REM Ask the user if hes really sure to continue beyond this point XXXXXXXX
set /p choice=Are you sure you want to continue (Y/N)?
if not '%choice%'=='Y' Goto EndOfBatch
REM Script does not continue unless user types 'Y' in upper case letter
ECHO.
ECHO XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
ECHO.
ECHO XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
ECHO.
ECHO Removing vs settings folder with *.sou file
ECHO.
RMDIR /S /Q .vs

ECHO Deleting BIN and OBJ Folders in DaxEditor
ECHO.
RMDIR /S /Q src\DaxEditor\bin
RMDIR /S /Q src\DaxEditor\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.Checker
ECHO.
RMDIR /S /Q src\DaxStudio.Checker\bin
RMDIR /S /Q src\DaxStudio.Checker\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.Common
ECHO.
RMDIR /S /Q src\DaxStudio.Common\bin
RMDIR /S /Q src\DaxStudio.Common\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.Controls.DataGridFilter
ECHO.
RMDIR /S /Q src\DaxStudio.Controls.DataGridFilter\bin
RMDIR /S /Q src\DaxStudio.Controls.DataGridFilter\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.ExcelAddin
ECHO.
RMDIR /S /Q src\DaxStudio.ExcelAddin\bin
RMDIR /S /Q src\DaxStudio.ExcelAddin\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.Interfaces
ECHO.
RMDIR /S /Q src\DaxStudio.Interfaces\bin
RMDIR /S /Q src\DaxStudio.Interfaces\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.QueryTrace
ECHO.
RMDIR /S /Q src\DaxStudio.QueryTrace\bin
RMDIR /S /Q src\DaxStudio.QueryTrace\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.QueryTrace.Excel

ECHO.
RMDIR /S /Q src\DaxStudio.QueryTrace.Excel\bin
RMDIR /S /Q src\DaxStudio.QueryTrace.Excel\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.Standalone
ECHO.
RMDIR /S /Q src\DaxStudio.Standalone\bin
RMDIR /S /Q src\DaxStudio.Standalone\obj
RMDIR /S /Q src\bin
RMDIR /S /Q src\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.UI
ECHO.
RMDIR /S /Q src\DaxStudio.UI\bin
RMDIR /S /Q src\DaxStudio.UI\obj

ECHO Deleting BIN and OBJ Folders in UnitComboLib
ECHO.
RMDIR /S /Q src\UnitComboLib\bin
RMDIR /S /Q src\UnitComboLib\obj

ECHO Deleting BIN and OBJ Folders in DaxStudio.Tests
ECHO.
RMDIR /S /Q tests\DaxStudio.Tests\bin
RMDIR /S /Q tests\DaxStudio.Tests\obj


PAUSE

:EndOfBatch
