@echo off
chcp 65001 >nul
title LanUsb - Pobieranie i instalacja sterownika na komputer do streamowania

:: Sprawdzenie uprawnień Administratora
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Proszenie o uprawnienia Administratora...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ====================================================================
echo   LanUsb - Instalacja sterownika klienta (usbip-win2)
echo   Przeznaczone na komputer do streamowania (Klient)
echo ====================================================================
echo.

if exist "C:\Program Files\usbip-win2\usbip.exe" (
    echo Sterownik usbip-win2 jest juz zainstalowany w C:\Program Files\usbip-win2\
    echo.
    pause
    exit /b 0
)

echo [1/2] Próba instalacji przez winget...
where winget >nul 2>&1
if %errorlevel% equ 0 (
    winget install --exact --id vadimgrn.usbip-win2 --accept-package-agreements --accept-source-agreements
    if exist "C:\Program Files\usbip-win2\usbip.exe" (
        echo.
        echo Gotowe! Sterownik usbip-win2 zostal zainstalowany.
        pause
        exit /b 0
    )
)

echo.
echo [2/2] Pobieranie najnowszego instalatora bezposrednio z GitHub (vadimgrn/usbip-win2)...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/vadimgrn/usbip-win2/releases/latest' -Headers @{ 'User-Agent' = 'LanUsb' };" ^
  "$asset = $rel.assets | Where-Object { $_.name -match '\.(msi|exe)$' } | Select-Object -First 1;" ^
  "if (-not $asset) { throw 'Nie znaleziono instalatora .msi/.exe w wydaniu GitHub.'; }" ^
  "$out = Join-Path $env:TEMP $asset.name;" ^
  "Write-Host ('Pobieranie ' + $asset.name + '...');" ^
  "Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $out;" ^
  "Write-Host 'Uruchamianie instalatora...';" ^
  "if ($out.EndsWith('.msi')) { Start-Process msiexec.exe -ArgumentList ('/i \"' + $out + '\"') -Wait } else { Start-Process $out -Wait }"

echo.
if exist "C:\Program Files\usbip-win2\usbip.exe" (
    echo Gotowe! Sterownik usbip-win2 zostal zainstalowany.
) else (
    echo Jesli instalator zakonczyl sie pomyslnie, mozesz teraz uruchomic LanUsb.Client.exe.
)
pause
