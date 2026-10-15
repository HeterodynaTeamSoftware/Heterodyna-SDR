@echo off
setlocal EnableExtensions
set "RF=%~dp0ProtocolLab\RfAdapter\bin\Release\publish\Heterodyna.RfAdapter.exe"
if not exist "%RF%" (
  echo Brak RF Adapter. Najpierw uruchom KOMPILUJ_WINDOWS.cmd.
  pause
  exit /b 1
)
set "FREQ=131550000"
if not "%~1"=="" set "FREQ=%~1"
echo Potrzebny jest rtl_fm.exe i odpowiednie zaleznosci w tools\rtl_fm.
echo Wlacz LIVE w GUI i zwolnij RTL-SDR w innych programach.
"%RF%" --mode rtl --freq-hz "%FREQ%"
set "EXIT_CODE=%ERRORLEVEL%"
pause
exit /b %EXIT_CODE%
