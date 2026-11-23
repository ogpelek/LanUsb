@echo off
chcp 65001 >nul
title LanUsb - Instalator sterowników USB/IP (Open Source, podpisane)

:: Sprawdzenie uprawnień Administratora
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Proszenie o uprawnienia Administratora...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:menu
cls
echo ========================================================
echo   LanUsb - Oddzielny instalator sterowników USB/IP
echo ========================================================
echo.
echo   [1] Zainstaluj sterownik SERWERA (usbipd-win)
echo       - na komputer, do którego podpięte są urządzenia USB
echo.
echo   [2] Zainstaluj sterownik KLIENTA (usbip-win2)
echo       - na komputer do streamowania (wirtualny kontroler USB)
echo.
echo   [3] Zainstaluj OBA sterowniki (Serwer + Klient)
echo.
echo   [0] Wyjście
echo.
set /p choice=Wybierz opcję (0-3): 

if "%choice%"=="1" goto install_server
if "%choice%"=="2" goto install_client
if "%choice%"=="3" goto install_both
if "%choice%"=="0" exit /b
goto menu

:install_server
echo.
echo Instalowanie sterownika serwera (dorssel.usbipd-win)...
winget install --exact --id dorssel.usbipd-win --accept-package-agreements --accept-source-agreements
echo.
pause
exit /b

:install_client
echo.
echo Instalowanie sterownika klienta (vadimgrn.usbip-win2)...
winget install --exact --id vadimgrn.usbip-win2 --accept-package-agreements --accept-source-agreements
echo.
pause
exit /b

:install_both
echo.
echo Instalowanie sterownika serwera (dorssel.usbipd-win)...
winget install --exact --id dorssel.usbipd-win --accept-package-agreements --accept-source-agreements
echo.
echo Instalowanie sterownika klienta (vadimgrn.usbip-win2)...
winget install --exact --id vadimgrn.usbip-win2 --accept-package-agreements --accept-source-agreements
echo.
pause
exit /b
