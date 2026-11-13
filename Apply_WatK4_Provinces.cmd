@echo off
setlocal
cd /d "%~dp0"
"%~dp0artifacts\bin\Release\mprovince.exe" /I "%~dp0artifacts\watk4-demo\provinces.txt" "%~dp0artifacts\watk4-demo\WatK4_Edit.eu2map" /TXT
pause
