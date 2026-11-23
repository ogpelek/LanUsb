@echo off
chcp 65001 >nul
title LanUsb - Publikacja na GitHub (Open Source)
color 0B

echo =====================================================================
echo           LanUsb - Automatyczna publikacja na GitHubie
echo =====================================================================
echo.

set "PATH=C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI;%PATH%"

cd /d "%~dp0"

echo [1/3] Sprawdzanie logowania do GitHub...
gh auth status >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo.
    echo Nie jestes jeszcze zalogowany do GitHub CLI.
    echo Za chwile otworzy sie strona logowania w przegladarce (wprowadz kod jednorazowy).
    echo.
    gh auth login -p https -w -s repo
    if %ERRORLEVEL% neq 0 (
        echo [BLAD] Logowanie przerwane.
        pause
        exit /b 1
    )
)

echo.
echo [2/3] Tworzenie publicznego repozytorium GitHub (Open Source)...
git remote get-url origin >nul 2>&1
if %ERRORLEVEL% neq 0 (
    gh repo create LanUsb --public --source=. --remote=origin --description "LanUsb - Darmowy, otwartoźródłowy program USB-over-LAN do streamingu z dwóch PC (alternatywa dla VirtualHere)"
) else (
    echo Zdalne repozytorium origin jest juz skonfigurowane.
)

echo.
echo [3/3] Wysylanie kodu (git push)...
git branch -M main
git push -u origin main

if %ERRORLEVEL% equ 0 (
    echo.
    echo =====================================================================
    echo   SUKCES! Projekt LanUsb zostal opublikowany na Twoim GitHubie!
    echo =====================================================================
) else (
    echo.
    echo [UWAGA] Jesli repozytorium juz istnieje na Twoim koncie GitHub, uzyj:
    echo git push -u origin main --force
)

echo.
pause
