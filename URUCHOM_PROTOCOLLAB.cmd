@echo off
setlocal EnableExtensions
set "APP=%~dp0ProtocolLab\bin\Release\publish\Heterodyna.ProtocolLab.exe"
if not exist "%APP%" (
  echo Brak skompilowanej aplikacji. Najpierw uruchom KOMPILUJ_WINDOWS.cmd.
  pause
  exit /b 1
)
start "" "%APP%"
if errorlevel 1 (
  echo Nie mozna uruchomic aplikacji. Sprawdz .NET Desktop Runtime 8 x64.
  pause
  exit /b 1
)
exit /b 0
