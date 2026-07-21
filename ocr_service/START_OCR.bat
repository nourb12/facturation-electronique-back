@echo off
echo ========================================
echo   Demarrage du service OCR TuniFlow
echo ========================================
echo.

cd /d "%~dp0"

if not exist ".venv" (
    echo [ERREUR] Environnement virtuel Python introuvable!
    echo Veuillez d'abord installer les dependances:
    echo   python -m venv .venv
    echo   .\.venv\Scripts\activate
    echo   pip install -r requirements.txt
    pause
    exit /b 1
)

echo [INFO] Activation de l'environnement virtuel...
call .venv\Scripts\activate.bat

echo [INFO] Demarrage du serveur OCR sur http://localhost:8000...
echo.
uvicorn main:app --host 0.0.0.0 --port 8000 --reload

pause
