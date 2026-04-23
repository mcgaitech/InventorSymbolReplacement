@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

:: ════════════════════════════════════════════════════════════════
:: restart.bat — Đóng Inventor, mở lại với file test
:: Cách dùng : restart.bat [đường_dẫn_file_test]
::             Nếu không truyền tham số → dùng TEST_FILE_DEFAULT
:: Gọi bởi  : Claude Code (tự động) sau khi deploy.bat thành công
:: Exit code : 0 = Inventor đã mở  |  1 = thất bại
:: ════════════════════════════════════════════════════════════════

:: ── CẤU HÌNH ────────────────────────────────────────────────────
set INVENTOR_EXE=C:\Program Files\Autodesk\Inventor 2023\Bin\Inventor.exe
set TEST_FILE_DEFAULT=C:\MacGregor_CAS_WF\Designs\40 Products\DOOR\EXTERNAL DOOR\Vertical Sliding Door\Part Complete\400284387- X2 WWL\Mech\CAS-0033254.idw
:: ────────────────────────────────────────────────────────────────

:: Nhận file test từ tham số, fallback về default
set TEST_FILE=%~1
if "%TEST_FILE%"=="" set TEST_FILE=%TEST_FILE_DEFAULT%

echo.
echo ════════════════════════════════════════════════════════════════
echo   RESTART INVENTOR
echo ════════════════════════════════════════════════════════════════

:: ── GUARD: Kiểm tra Inventor.exe tồn tại ───────────────────────
echo.
echo [CHECK] Kiem tra Inventor tai: %INVENTOR_EXE%
if not exist "%INVENTOR_EXE%" (
    echo [LOI] Khong tim thay Inventor.exe.
    echo       Cap nhat bien INVENTOR_EXE trong restart.bat.
    exit /b 1
)
echo [OK] Tim thay Inventor.exe.

:: ── GUARD: Kiểm tra file test tồn tại ──────────────────────────
echo.
echo [CHECK] Kiem tra file test: %TEST_FILE%
if not exist "%TEST_FILE%" (
    echo [LOI] Khong tim thay file test:
    echo       %TEST_FILE%
    echo.
    echo       Truyen duong dan cu the: restart.bat "C:\path\to\file.idw"
    echo       Hoac cap nhat bien TEST_FILE_DEFAULT trong restart.bat.
    exit /b 1
)
echo [OK] Tim thay file test.

:: ── BƯỚC 1: Đóng Inventor nếu đang chạy ────────────────────────
echo.
tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo [1/3] Inventor dang chay - dang dong...

    :: Thử graceful terminate qua WMI trước (ít bạo lực hơn taskkill /F)
    wmic process where "name='Inventor.exe'" call terminate >nul 2>&1
    timeout /t 4 /nobreak >nul

    :: Nếu vẫn còn thì mới force kill
    tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
    if !ERRORLEVEL! equ 0 (
        echo       Graceful close that bai, dang force kill...
        taskkill /F /IM Inventor.exe >nul 2>&1
        timeout /t 3 /nobreak >nul
    )

    :: Xác nhận đã tắt hẳn
    tasklist /FI "IMAGENAME eq Inventor.exe" 2>nul | find /I "Inventor.exe" >nul
    if !ERRORLEVEL! equ 0 (
        echo [LOI] Khong the tat Inventor. Thu dong tay truoc.
        exit /b 1
    )
    echo [OK] Inventor da dong.
) else (
    echo [1/3] Inventor chua mo - bo qua buoc dong.
)

:: ── BƯỚC 2: Dọn dẹp process còn sót ────────────────────────────
echo.
echo [2/3] Don dep process...
:: Đợi thêm để Windows giải phóng lock file DLL
timeout /t 2 /nobreak >nul
echo [OK] Cho process giai phong xong.

:: ── BƯỚC 3: Mở Inventor với file test ──────────────────────────
echo.
echo [3/3] Mo Inventor voi file test...
echo       File: %TEST_FILE%

start "" "%INVENTOR_EXE%" "%TEST_FILE%"

if %ERRORLEVEL% neq 0 (
    echo [LOI] Khong the khoi dong Inventor.
    exit /b 1
)

:: ── DONE ────────────────────────────────────────────────────────
echo.
echo ════════════════════════════════════════════════════════════════
echo   RESTART HOAN THANH
echo ════════════════════════════════════════════════════════════════
echo.
echo   Inventor dang khoi dong voi file:
echo   %TEST_FILE%
echo.
echo   - Doi khoang 20-30 giay de Inventor load xong
echo   - Kiem tra Add-In Manager: add-in co hien khong?
echo   - Kiem tra Ribbon: tab "MCG TOOLS" co xuat hien?
echo   - Mo DebugView de xem log realtime
echo.
echo   == SAU KHI TEST XONG ==
echo   Bao lai ket qua de Claude Code xu ly tiep.
echo.
exit /b 0