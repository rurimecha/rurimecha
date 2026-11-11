@echo off
setlocal
set "dataDir=%~1"
if not defined dataDir set "dataDir=%LOCALAPPDATA%\Rurimecha\data"
"%~dp0rurimecha-core.exe" -data-dir "%dataDir%" -reset-admin-password
set "result=%errorlevel%"
pause
exit /b %result%
