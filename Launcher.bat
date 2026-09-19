@echo off
REM Serenity dev launcher. Bypasses execution policy for this one script only.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0Launcher.ps1"
