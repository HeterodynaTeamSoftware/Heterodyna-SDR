@echo off
setlocal EnableExtensions
set "RF=%~dp0ProtocolLab\RfAdapter\bin\Release\publish\Heterodyna.RfAdapter.exe"
if not exist "%RF%" (
  echo Brak RF Adapter. Najpierw uruchom KOMPILUJ_WINDOWS.cmd.
  pause
  exit /b 1
)
echo UWAGA: SYNTETYCZNE DANE, NIE ODBIOR RADI0WY.
echo Wlacz LIVE w oknie Protocol Lab.
"%RF%" --mode demo --freq-hz 131550000
set "EXIT_CODE=%ERRORLEVEL%"
pause
exit /b %EXIT_CODE%
