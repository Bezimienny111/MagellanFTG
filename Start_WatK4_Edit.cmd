@echo off
setlocal
cd /d "%~dp0"
start "" "%~dp0artifacts\bin\Release\mview.exe" "%~dp0artifacts\watk4-demo\WatK4_Edit.eu2map"
