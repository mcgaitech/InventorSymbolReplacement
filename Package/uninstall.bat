@echo off
chcp 65001 >nul
setlocal

:: ════════════════════════════════════════════════════════════════
:: MCGInventorPlugin — Uninstaller
:: Chạy với quyền Administrator
:: ════════════════════════════════════════════════════════════════

set ASSEMBLY=MCGInventorPlugin
set ADDIN_DIR=%APPDATA%\Autodesk\Inventor 2023\Addins
set REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe

echo.
echo ════════════════════════════════════════════════════════════════
echo   MCGInventorPlugin — Uninstall
echo ════════════════════════════════════════════════════════════════

:: ── Kiểm tra Inventor không đang chạy ──────────────────────────
tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo.
    echo [LOI] Inventor dang chay! Dong Inventor truoc khi go cai dat.
    echo.
    pause
    exit /b 1
)

:: ── Unregister COM ─────────────────────────────────────────────
echo.
echo [1/2] COM Unregistration...
if exist "%REGASM%" (
    "%REGASM%" "%ADDIN_DIR%\%ASSEMBLY%.dll" /unregister /nologo 2>nul
    echo [OK] COM unregistered.
) else (
    echo [SKIP] RegAsm khong tim thay.
)

:: ── Xóa files ──────────────────────────────────────────────────
echo.
echo [2/2] Xoa files...
del /F /Q "%ADDIN_DIR%\%ASSEMBLY%.dll"   2>nul
del /F /Q "%ADDIN_DIR%\%ASSEMBLY%.addin" 2>nul
del /F /Q "%ADDIN_DIR%\%ASSEMBLY%.pdb"   2>nul
echo [OK] Files removed.

echo.
echo ════════════════════════════════════════════════════════════════
echo   GO CAI DAT THANH CONG!
echo ════════════════════════════════════════════════════════════════
echo.
pause
