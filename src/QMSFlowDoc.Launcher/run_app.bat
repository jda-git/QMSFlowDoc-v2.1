@echo off
echo [1/3] Deteniendo procesos anteriores...
taskkill /F /IM QMSFlowDoc.Api.exe >nul 2>&1
taskkill /F /IM QMSFlowDoc.Client.exe >nul 2>&1

echo [2/3] Preparando entorno...
cd /d "%~dp0"

echo [3/3] Iniciando Launcher...
dotnet run

echo.
echo [Finish] Launcher finalizado.
pause
