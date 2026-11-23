@echo off
chcp 65001 >nul
title LanUsb - Otwieranie portów Zapory Windows

:: Sprawdzenie uprawnień Administratora; jeśli brak, uruchom ponownie z UAC
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Proszenie o uprawnienia Administratora...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================
echo   LanUsb - Konfiguracja Zapory Windows
echo ============================================
echo.

:: Usuń stare reguły o tej samej nazwie (aby nie tworzyć duplikatów)
netsh advfirewall firewall delete rule name="LanUsb USB/IP (TCP 3240)" >nul 2>&1
netsh advfirewall firewall delete rule name="LanUsb Discovery (UDP 32400)" >nul 2>&1
netsh advfirewall firewall delete rule name="LanUsb Control (TCP 32401)" >nul 2>&1

:: Dodaj reguły przychodzące
echo Dodawanie reguly TCP 3240 (USB/IP)...
netsh advfirewall firewall add rule name="LanUsb USB/IP (TCP 3240)" dir=in action=allow protocol=TCP localport=3240

echo Dodawanie reguly UDP 32400 (Wykrywanie LAN)...
netsh advfirewall firewall add rule name="LanUsb Discovery (UDP 32400)" dir=in action=allow protocol=UDP localport=32400

echo Dodawanie reguly TCP 32401 (Zdalne sterowanie)...
netsh advfirewall firewall add rule name="LanUsb Control (TCP 32401)" dir=in action=allow protocol=TCP localport=32401

echo.
echo Gotowe! Porty 3240 (TCP), 32400 (UDP) i 32401 (TCP) zostaly otwarte.
echo.
pause
