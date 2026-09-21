@echo off
setlocal
title Build Viper Disk Harvester
cd /d "%~dp0"

if not exist "src\ViperDiskHarvester.cs" goto MISSING
if not exist "assets\viper_icon.ico" goto MISSING
if not exist "assets\woz_reaper_logo.png" goto MISSING

set "CSC64=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "CSC32=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if exist "%CSC64%" (
    set "CSC=%CSC64%"
) else if exist "%CSC32%" (
    set "CSC=%CSC32%"
) else (
    echo.
    echo ERROR: Microsoft .NET Framework C# compiler was not found.
    echo Enable or install .NET Framework 4.x and try again.
    echo.
    pause
    exit /b 1
)

if not exist "dist" mkdir "dist"

echo.
echo ============================================================
echo          VIPER DISK HARVESTER v1.0.2 BUILD
echo ============================================================
echo Compiler: %CSC%
echo.

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /win32icon:"assets\viper_icon.ico" /resource:"assets\woz_reaper_logo.png",ViperDiskHarvester.Branding.WozReaperLogo /out:"dist\Viper-Disk-Harvester.exe" "src\ViperDiskHarvester.cs"

if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    pause
    exit /b 1
)

echo.
echo BUILD COMPLETE:
echo   %CD%\dist\Viper-Disk-Harvester.exe
echo.
echo Both the Viper icon and Woz Reaper header are embedded in the EXE.
echo.
pause
exit /b 0

:MISSING
echo.
echo ERROR: Required repository files are missing.
echo Make sure you extracted or cloned the COMPLETE repository.
echo.
pause
exit /b 1
