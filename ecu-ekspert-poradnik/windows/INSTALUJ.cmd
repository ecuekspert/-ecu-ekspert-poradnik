@echo off
title Instalacja - ECU EKSPERT Poradnik elektryka samochodowego
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Nie znaleziono .NET Framework 4 w Windows.
  echo Wlacz .NET Framework 4.8 w "Wlacz lub wylacz funkcje systemu Windows".
  pause
  exit /b 1
)

set "DEST=%LOCALAPPDATA%\EcuEkspert\Poradnik"
set "EXE=%DEST%\ECU Ekspert Poradnik.exe"
if not exist "%DEST%" mkdir "%DEST%"

echo Budowanie programu...
"%CSC%" /nologo /target:winexe /optimize+ /out:"%EXE%" /win32icon:ikona.ico /resource:poradnik.html,poradnik.html /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs
if errorlevel 1 (
  echo.
  echo BLAD budowania. Zrob zdjecie tego okna i wyslij.
  pause
  exit /b 1
)

rem adres aktualizacji (jesli wpisany)
findstr /i "http" "adres-aktualizacji.txt" >nul 2>&1 && copy /y "adres-aktualizacji.txt" "%DEST%\adres-aktualizacji.txt" >nul

rem przycisk "Aktualizacja" w programie
reg add "HKCU\Software\Classes\ecuporadnik" /ve /d "URL:ECU Ekspert Poradnik" /f >nul
reg add "HKCU\Software\Classes\ecuporadnik" /v "URL Protocol" /d "" /f >nul
reg add "HKCU\Software\Classes\ecuporadnik\DefaultIcon" /ve /d "\"%EXE%\",0" /f >nul
reg add "HKCU\Software\Classes\ecuporadnik\shell\open\command" /ve /d "\"%EXE%\" \"%%1\"" /f >nul

rem stara wersja
del /q "%DEST%\Poradnik czujnikow.exe" >nul 2>&1

echo Tworzenie skrotow na pulpicie i w menu Start...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0skroty.ps1" -Exe "%EXE%"

echo.
echo GOTOWE. Program jest na pulpicie i w menu Start:
echo   ECU EKSPERT - Poradnik elektryka samochodowego
echo.
start "" "%EXE%"
pause
