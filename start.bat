@echo off
title ERPES Operasyon Kulesi
cd /d "%~dp0"
echo ERPES Operasyon Kulesi baslatiliyor...
dotnet run --launch-profile http
pause
