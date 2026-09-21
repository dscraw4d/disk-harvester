@echo off
call "%~dp0Build-Viper-Disk-Harvester.cmd"
if errorlevel 1 exit /b 1
start "" "%~dp0dist\Viper-Disk-Harvester.exe"
