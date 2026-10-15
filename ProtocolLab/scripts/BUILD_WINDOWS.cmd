@echo off
setlocal EnableExtensions DisableDelayedExpansion
pushd "%~dp0.."
if errorlevel 1 (
  echo BLAD: nie moge wejsc do katalogu ProtocolLab.
  if /I not "%~1"=="--auto" pause
  exit /b 1
)
if not exist "build-logs" mkdir "build-logs"
set "LOG=%CD%\build-logs\latest-build.log"
> "%LOG%" echo HETERODYNA PROTOCOL LAB 0.3.3 - WINDOWS BUILD
>> "%LOG%" echo Katalog: %CD%
>> "%LOG%" echo Data: %DATE% %TIME%
echo.
echo [0/7] Sprawdzam .NET SDK...
where dotnet >nul 2>nul
if errorlevel 1 (
  >> "%LOG%" echo BLAD: polecenie dotnet nie jest dostepne w PATH.
  goto :sdkfail
)
dotnet --list-sdks >> "%LOG%" 2>&1
if errorlevel 1 goto :sdkfail
dotnet --list-sdks | findstr /r /c:"^[89]\." /c:"^1[0-9]\." >nul
if errorlevel 1 (
  >> "%LOG%" echo BLAD: Brak .NET SDK 8 lub nowszego.
  goto :sdkfail
)
echo [1/7] Przygotowuje i uruchamiam testy Core...
dotnet run --project "Tests\Heterodyna.ProtocolLab.Tests.csproj" -c Release >> "%LOG%" 2>&1
if errorlevel 1 goto :fail
echo [2/7] Przywracam zaleznosci aplikacji Windows x64...
dotnet restore "Heterodyna.ProtocolLab.csproj" -r win-x64 >> "%LOG%" 2>&1
if errorlevel 1 goto :fail
echo [3/7] Testuje PCM RF Adapter (.NET 8, bez dongla)...
dotnet run --project "RfAdapter\Heterodyna.ProtocolLab.RfAdapter.csproj" -c Release -- --selftest >> "%LOG%" 2>&1
if errorlevel 1 goto :fail
echo [4/7] Kompiluje i publikuje Heterodyna Protocol Lab...
dotnet publish "Heterodyna.ProtocolLab.csproj" -c Release -r win-x64 --no-restore --self-contained false -p:PublishSingleFile=true -o "bin\Release\publish" >> "%LOG%" 2>&1
if errorlevel 1 goto :fail
if not exist "bin\Release\publish\Heterodyna.ProtocolLab.exe" (
  >> "%LOG%" echo BLAD: brak EXE mimo udanego publish.
  goto :fail
)
echo [5/7] Publikuje RF Adapter win-x64...
dotnet publish "RfAdapter\Heterodyna.ProtocolLab.RfAdapter.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "RfAdapter\bin\Release\publish" >> "%LOG%" 2>&1
if errorlevel 1 goto :fail
if not exist "RfAdapter\bin\Release\publish\Heterodyna.RfAdapter.exe" (
  >> "%LOG%" echo BLAD: nie utworzono RF Adapter EXE.
  goto :fail
)
echo [6/7] Uruchamiam test okna GUI na Windows (automatyczne zamkniecie)...
if exist "bin\Release\publish\gui-smoketest.txt" del /q "bin\Release\publish\gui-smoketest.txt" >nul 2>nul
start "" /wait "%CD%\bin\Release\publish\Heterodyna.ProtocolLab.exe" --gui-selftest
if errorlevel 1 (
  if exist "bin\Release\publish\gui-smoketest.txt" type "bin\Release\publish\gui-smoketest.txt" >> "%LOG%"
  >> "%LOG%" echo BLAD: test startu GUI nie przeszedl; sprawdz runtime .NET Desktop i raport.
  goto :fail
)
if not exist "bin\Release\publish\gui-smoketest.txt" (
  >> "%LOG%" echo BLAD: nie powstal raport testu GUI.
  goto :fail
)
findstr /b /c:"PASS:" "bin\Release\publish\gui-smoketest.txt" >nul
if errorlevel 1 (
  type "bin\Release\publish\gui-smoketest.txt" >> "%LOG%"
  >> "%LOG%" echo BLAD: raport GUI nie ma wyniku PASS.
  goto :fail
)
type "bin\Release\publish\gui-smoketest.txt" >> "%LOG%"
> "bin\Release\publish\build-version.txt" echo 0.3.3
echo [7/7] SUKCES - Core, kompilacja, publikacja i uruchomienie GUI poprawne.
>> "%LOG%" echo SUKCES: Core, kompilacja, publikacja i uruchomienie GUI poprawne.
echo EXE: ProtocolLab\bin\Release\publish\Heterodyna.ProtocolLab.exe
echo RF: ProtocolLab\RfAdapter\bin\Release\publish\Heterodyna.RfAdapter.exe
echo LOG: ProtocolLab\build-logs\latest-build.log
if /I not "%~1"=="--auto" pause
popd
exit /b 0

:sdkfail
echo.
echo BLAD: nie znaleziono .NET SDK 8 x64 lub nowszego.
echo Pobierz SDK: https://dotnet.microsoft.com/download/dotnet/8.0
:fail
echo.
echo ===========================================================
echo KOMPILACJA NIEUDANA - pelny log ponizej:
echo ===========================================================
type "%LOG%"
echo ===========================================================
echo ZAPISANO: "%LOG%"
echo W razie bledu skopiuj log i wyslij go do analizy.
if /I not "%~1"=="--auto" pause
popd
exit /b 1
