@echo off
echo ========================================
echo   Demarrage Backend TunisFlow (Reseau)
echo ========================================
echo.
echo [INFO] Demarrage sur 0.0.0.0:5051 (accessible depuis mobile)...
echo.

cd /d "%~dp0\Einvoicing.Api"

dotnet run --launch-profile http

pause
