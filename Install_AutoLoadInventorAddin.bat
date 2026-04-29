@echo off
setlocal enabledelayedexpansion

:: 1. Define paths
set "SOURCE_DIR=C:\CustomTools\Inventor"
:: You can change "Inventor 2023" to 2024, 2025, etc., as needed
set "INVENTOR_USER_ADDIN=%APPDATA%\Autodesk\Inventor 2023\Addins"

echo =====================================================
echo    SCANNING AND ACTIVATING ALL CUSTOM TOOLS ADD-INS
echo =====================================================

:: 2. Check source directory
if not exist "%SOURCE_DIR%" (
    echo [ERROR] Source directory not found: %SOURCE_DIR%
    pause
    exit /b
)

:: 3. Create Inventor Addins folder if it doesn't exist
if not exist "%INVENTOR_USER_ADDIN%" (
    echo [+] Creating Addins directory...
    mkdir "%INVENTOR_USER_ADDIN%"
)

:: 4. Loop to scan and copy all .addin files
echo [+] Starting scan for .addin files...
set /a count=0

for %%F in ("%SOURCE_DIR%\*.addin") do (
    echo -- Copying: %%~nxF
    copy /Y "%%F" "%INVENTOR_USER_ADDIN%\" >nul
    if !errorlevel! equ 0 (
        set /a count+=1
    ) else (
        echo [!] Error copying file: %%~nxF
    )
)

echo -----------------------------------------------------
if %count% gtr 0 (
    echo [OK] Success! %count% Add-ins have been activated.
    echo Target folder: %INVENTOR_USER_ADDIN%
) else (
    echo [!] No .addin files found in %SOURCE_DIR%
)

echo -----------------------------------------------------
echo Please restart Inventor 2023 to apply changes.
pause