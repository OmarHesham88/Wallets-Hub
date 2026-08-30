@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-WalletsHub.ps1"
exit /b %errorlevel%
