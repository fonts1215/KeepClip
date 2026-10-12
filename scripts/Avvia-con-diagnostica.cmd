@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Diagnose-Portable.ps1" -Launch
echo.
pause
