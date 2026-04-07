@echo off
chcp 65001 >nul
:: deploy.bat — Build, deploy, restart Inventor
:: Không cần PowerShell, không cần quyền admin
:: Chạy bằng cách double-click hoặc Claude Code gọi tự động

:: ════════════════════════════════════════════
:: CẤU HÌNH — sửa nếu Inventor cài ở ổ khác
:: ════════════════════════════════════════════
set INVENTOR_EXE=C:\Program Files\Autodesk\Inventor 2023\Bin\Inventor.exe
set ADDIN_DIR=%APPDATA%\Autodesk\Inventor 2023\Addins
set PROJECT_DIR=%~dp0
set ASSEMBLY=SymbolReplacer

echo.
echo ════════════════════════════════════════════
echo   Symbol Replacer - Deploy and Reload
echo ════════════════════════════════════════════

:: ── BƯỚC 1: Build ────────────────────────────
echo.
echo [1/3] Dang build project...
cd /d "%PROJECT_DIR%"
"C:\Program Files\dotnet\dotnet.exe" build -c Debug

if %ERRORLEVEL% neq 0 (
    echo.
    echo [LOI] Build that bai. Dung lai.
    echo       Kiem tra loi ben tren va fix truoc khi deploy lai.
    pause
    exit /b 1
)
echo [OK] Build thanh cong.

:: ── BƯỚC 2: Kiểm tra DLL đã được copy chưa ──
echo.
echo [2/3] Kiem tra deploy...
if not exist "%ADDIN_DIR%\%ASSEMBLY%.dll" (
    echo [LOI] Khong tim thay DLL tai: %ADDIN_DIR%
    echo       Post-build event co the bi loi.
    echo       Copy thu cong:
    echo       copy "bin\Debug\net48\%ASSEMBLY%.dll" "%ADDIN_DIR%\"
    echo       copy "%ASSEMBLY%.addin" "%ADDIN_DIR%\"
    pause
    exit /b 1
)
echo [OK] DLL da duoc copy: %ADDIN_DIR%\%ASSEMBLY%.dll
echo [OK] .addin da duoc copy: %ADDIN_DIR%\%ASSEMBLY%.addin

:: ── BƯỚC 3: Tắt Inventor ─────────────────────
echo.
echo [3/3] Tat Inventor...
tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo       Inventor dang chay, dang tat...
    taskkill /F /IM Inventor.exe >nul 2>&1
    timeout /t 3 /nobreak >nul
    echo [OK] Inventor da tat.
) else (
    echo [OK] Inventor chua mo - bo qua buoc nay.
)

:: ── Mở Inventor ──────────────────────────────
echo.
echo Mo Inventor...
if not exist "%INVENTOR_EXE%" (
    echo [LOI] Khong tim thay: %INVENTOR_EXE%
    echo       Cap nhat bien INVENTOR_EXE trong deploy.bat
    pause
    exit /b 1
)

start "" "%INVENTOR_EXE%"

echo.
echo ════════════════════════════════════════════
echo   Deploy HOAN THANH
echo ════════════════════════════════════════════
echo.
echo Sau khi Inventor mo xong, kiem tra:
echo   1. Tools - Add-In Manager - "Symbol Replacer" co hien?
echo   2. Mo file .idw - Tab "Custom Tools" co tren ribbon?
echo   3. Click button - Palette co mo ra?
echo   4. Xem log trong DebugView
echo.
