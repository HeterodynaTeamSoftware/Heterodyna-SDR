@echo off
setlocal
cd /d "%~dp0.."
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Brak .NET 8 SDK. https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet run --project Tests\Heterodyna.ProtocolLab.Tests.csproj -c Release
if errorlevel 1 (
 echo ERROR: testy niezaliczone.
 pause
 exit /b 1
)
echo Testy danych syntetycznych zakonczone.
pause
