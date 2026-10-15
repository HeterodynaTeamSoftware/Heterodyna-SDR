@echo off
setlocal EnableExtensions
call "%~dp0ProtocolLab\scripts\BUILD_WINDOWS.cmd" --auto
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo BLAD KOMPILACJI. Sprawdz ProtocolLab\build-logs\latest-build.log
) else (
  echo.
  echo GOTOWE. Aplikacja: ProtocolLab\bin\Release\publish\Heterodyna.ProtocolLab.exe
)
if /I not "%~1"=="--auto" pause
exit /b %EXIT_CODE%
