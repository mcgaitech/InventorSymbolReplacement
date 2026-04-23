@echo off
chcp 65001 >nul
setlocal

:: ════════════════════════════════════════════════════════════════
:: MCGInventorPlugin — Installer
:: Chạy với quyền Administrator
:: ════════════════════════════════════════════════════════════════

set ASSEMBLY=MCGInventorPlugin
set ADDIN_DIR=%APPDATA%\Autodesk\Inventor 2023\Addins
set REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set SCRIPT_DIR=%~dp0

echo.
echo ════════════════════════════════════════════════════════════════
echo   MCGInventorPlugin — Install
echo ════════════════════════════════════════════════════════════════

:: ── Kiểm tra Inventor không đang chạy ──────────────────────────
tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo.
    echo [LOI] Inventor dang chay! Dong Inventor truoc khi cai dat.
    echo.
    pause
    exit /b 1
)

:: ── Tạo thư mục Addins nếu chưa có ────────────────────────────
if not exist "%ADDIN_DIR%\" mkdir "%ADDIN_DIR%"

:: ── Copy files ─────────────────────────────────────────────────
echo.
echo [1/3] Copy files...
copy /Y "%SCRIPT_DIR%%ASSEMBLY%.dll"   "%ADDIN_DIR%\" >nul
copy /Y "%SCRIPT_DIR%%ASSEMBLY%.addin" "%ADDIN_DIR%\" >nul
echo [OK] Files copied to: %ADDIN_DIR%

:: ── COM Registration ───────────────────────────────────────────
echo.
echo [2/3] COM Registration...
if not exist "%REGASM%" (
    echo [LOI] Khong tim thay RegAsm.exe tai: %REGASM%
    echo       Can cai dat .NET Framework 4.8
    pause
    exit /b 1
)
"%REGASM%" "%ADDIN_DIR%\%ASSEMBLY%.dll" /codebase /nologo
if %ERRORLEVEL% neq 0 (
    echo [WARN] RegAsm can quyen Administrator.
    echo        Click phai file install.bat -^> Run as Administrator
    pause
    exit /b 1
)
echo [OK] COM registration thanh cong.

:: ── Done ───────────────────────────────────────────────────────
echo.
echo [3/3] Kiem tra...
echo [OK] %ADDIN_DIR%\%ASSEMBLY%.dll
echo [OK] %ADDIN_DIR%\%ASSEMBLY%.addin
echo.
echo ════════════════════════════════════════════════════════════════
echo   CAI DAT THANH CONG!
echo.
echo   Mo Inventor -^> Drawing -^> Tab "MCG TOOLS" -^> Symbol Handler
echo ════════════════════════════════════════════════════════════════
echo.
pause
