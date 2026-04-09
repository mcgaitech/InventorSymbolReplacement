@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

:: ════════════════════════════════════════════════════════════════
:: deploy.bat — Build + Verify DLL
:: Mục đích : Chỉ build và kiểm tra output. KHÔNG đụng Inventor.
:: Gọi bởi  : Claude Code (tự động) hoặc double-click (thủ công)
:: Exit code : 0 = thành công  |  1 = thất bại
:: ════════════════════════════════════════════════════════════════

:: ── CẤU HÌNH ────────────────────────────────────────────────────
set ASSEMBLY=SymbolReplacer
set DOTNET_EXE=C:\Program Files\dotnet\dotnet.exe
set ADDIN_DIR=%APPDATA%\Autodesk\Inventor 2023\Addins
set PROJECT_DIR=%~dp0
:: ────────────────────────────────────────────────────────────────

echo.
echo ════════════════════════════════════════════════════════════════
echo   DEPLOY ^| %ASSEMBLY%
echo ════════════════════════════════════════════════════════════════

:: ── GUARD: Không build khi Inventor đang mở ─────────────────────
echo.
echo [CHECK] Kiem tra Inventor...
tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo.
    echo [LOI] Inventor dang chay!
    echo       Khong the build khi Inventor giu lock DLL.
    echo       Dung restart.bat de dong Inventor truoc.
    echo.
    exit /b 1
)
echo [OK] Inventor chua mo - an toan de build.

:: ── BƯỚC 1: Build ───────────────────────────────────────────────
echo.
echo [1/3] Build project...
cd /d "%PROJECT_DIR%"

if not exist "%DOTNET_EXE%" (
    echo [LOI] Khong tim thay dotnet.exe tai: %DOTNET_EXE%
    echo       Cap nhat bien DOTNET_EXE trong deploy.bat
    exit /b 1
)

"%DOTNET_EXE%" build -c Debug --nologo 2>&1
set BUILD_RESULT=%ERRORLEVEL%

if %BUILD_RESULT% neq 0 (
    echo.
    echo [LOI] Build that bai ^(exit code: %BUILD_RESULT%^).
    echo       Doc loi o tren, fix roi chay lai deploy.bat.
    echo.
    exit /b 1
)
echo [OK] Build thanh cong.

:: ── BƯỚC 2: Kiểm tra DLL đã copy sang Addins chưa ───────────────
echo.
echo [2/3] Kiem tra DLL trong Addins folder...

if not exist "%ADDIN_DIR%\" (
    echo [LOI] Addins folder khong ton tai: %ADDIN_DIR%
    echo       Kiem tra Inventor da cai chua, hoac kiem tra bien ADDIN_DIR.
    exit /b 1
)

if not exist "%ADDIN_DIR%\%ASSEMBLY%.dll" (
    echo [LOI] DLL chua duoc copy:
    echo       Mong doi: %ADDIN_DIR%\%ASSEMBLY%.dll
    echo       Post-build event trong .csproj co the bi loi.
    echo.
    echo       Fix thu cong:
    echo         copy "%PROJECT_DIR%bin\Debug\net48\%ASSEMBLY%.dll" "%ADDIN_DIR%\"
    echo         copy "%PROJECT_DIR%%ASSEMBLY%.addin"                "%ADDIN_DIR%\"
    exit /b 1
)
echo [OK] DLL : %ADDIN_DIR%\%ASSEMBLY%.dll

if not exist "%ADDIN_DIR%\%ASSEMBLY%.addin" (
    echo [WARN] File .addin chua co trong Addins folder.
    echo        Copy thu cong: copy "%PROJECT_DIR%%ASSEMBLY%.addin" "%ADDIN_DIR%\"
) else (
    echo [OK] .addin: %ADDIN_DIR%\%ASSEMBLY%.addin
)

:: ── BƯỚC 3: In thông tin DLL để verify ──────────────────────────
echo.
echo [3/3] Thong tin DLL...
for %%F in ("%ADDIN_DIR%\%ASSEMBLY%.dll") do (
    echo       Kich thuoc : %%~zF bytes
    echo       Thoi gian  : %%~tF
)

:: ── DONE ────────────────────────────────────────────────────────
echo.
echo ════════════════════════════════════════════════════════════════
echo   BUILD HOAN THANH - San sang de restart Inventor
echo   Buoc tiep theo: chay restart.bat
echo ════════════════════════════════════════════════════════════════
echo.
exit /b 0