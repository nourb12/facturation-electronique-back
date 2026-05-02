@echo off
echo ========================================
echo   Installation du service OCR
echo ========================================
echo.

cd /d "%~dp0"

echo [1/4] Verification de Python...
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERREUR] Python n'est pas installe ou pas dans le PATH!
    echo Telechargez Python depuis: https://www.python.org/downloads/
    pause
    exit /b 1
)
python --version

echo.
echo [2/4] Creation de l'environnement virtuel...
if exist ".venv" (
    echo [INFO] Environnement virtuel deja present, on le garde.
) else (
    python -m venv .venv
    if errorlevel 1 (
        echo [ERREUR] Impossible de creer l'environnement virtuel!
        pause
        exit /b 1
    )
    echo [OK] Environnement virtuel cree.
)

echo.
echo [3/4] Activation de l'environnement virtuel...
call .venv\Scripts\activate.bat
if errorlevel 1 (
    echo [ERREUR] Impossible d'activer l'environnement virtuel!
    pause
    exit /b 1
)

echo.
echo [4/4] Installation des dependances Python...
echo [INFO] Cela peut prendre plusieurs minutes (PaddleOCR est volumineux)...
python -m pip install --upgrade pip
pip install -r requirements.txt
if errorlevel 1 (
    echo [ERREUR] Echec de l'installation des dependances!
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Installation terminee avec succes!
echo ========================================
echo.
echo Le service OCR demarrera automatiquement
echo quand vous lancerez le backend .NET.
echo.
echo Pour tester manuellement:
echo   .\START_OCR.bat
echo.
pause
