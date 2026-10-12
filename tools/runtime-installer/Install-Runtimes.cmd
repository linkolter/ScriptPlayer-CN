@echo off
setlocal
cd /d "%~dp0"
rem This process-only policy permits the reviewed local installer; no persistent policy is changed.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Runtimes.ps1"
set "runtimeInstallExit=%ERRORLEVEL%"
if not "%runtimeInstallExit%"=="0" echo Installation failed. Read the error above. No existing runtime is overwritten.
pause
exit /b %runtimeInstallExit%
