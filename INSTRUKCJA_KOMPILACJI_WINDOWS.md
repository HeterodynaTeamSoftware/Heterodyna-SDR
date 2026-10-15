# Instrukcja kompilacji — Heterodyna SDR Protocol Lab 0.3.3

**System:** Windows 10/11 x64. **Narzędzia:** .NET 8 SDK (polecenie `dotnet --info` musi działać). Nie potrzebujesz Visual Studio ani SDR do zbudowania i przetestowania parserów. Kompilację przeprowadź ze zwykłego konta użytkownika.

## Najprostszy sposób

Dwukrotnie kliknij `KOMPILUJ_WINDOWS.cmd`. Skrypt wykona testy Core, test adaptera PCM, `dotnet restore`, publikację GUI i RF Adapter, a na Windows uruchomi automatyczny smoke-test okna.

Wyniki przy powodzeniu:

- `ProtocolLab\bin\Release\publish\Heterodyna.ProtocolLab.exe`
- `ProtocolLab\RfAdapter\bin\Release\publish\Heterodyna.RfAdapter.exe`
- `ProtocolLab\build-logs\latest-build.log`
- `ProtocolLab\bin\Release\publish\gui-smoketest.txt`

Uruchom aplikację przez `URUCHOM_PROTOCOLLAB.cmd` (lub wyżej podany EXE). Jeśli podwójny klik pokazuje brak .NET Desktop Runtime, doinstaluj [.NET Desktop Runtime 8 x64](https://dotnet.microsoft.com/download/dotnet/8.0).

## Ręczne polecenia (PowerShell)

Z katalogu głównego rozpakowanej paczki:

```powershell
dotnet --info
dotnet run --project .\ProtocolLab\Tests\Heterodyna.ProtocolLab.Tests.csproj -c Release
dotnet run --project .\ProtocolLab\RfAdapter\Heterodyna.ProtocolLab.RfAdapter.csproj -c Release -- --selftest
dotnet restore .\ProtocolLab\Heterodyna.ProtocolLab.csproj -r win-x64
dotnet publish .\ProtocolLab\Heterodyna.ProtocolLab.csproj -c Release -r win-x64 --no-restore --self-contained false -p:PublishSingleFile=true -o .\ProtocolLab\bin\Release\publish
dotnet publish .\ProtocolLab\RfAdapter\Heterodyna.ProtocolLab.RfAdapter.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\ProtocolLab\RfAdapter\bin\Release\publish
.\ProtocolLab\bin\Release\publish\Heterodyna.ProtocolLab.exe --gui-selftest
```

Jeśli dostaniesz błąd pobierania pakietów, sprawdź połączenie internetowe, źródła NuGet i instalację SDK. Przy błędzie skryptu prześlij `ProtocolLab\build-logs\latest-build.log` bez danych prywatnych.

## Live / sprzęt

Tryb demo bez RTL-SDR: uruchom GUI, kliknij `Włącz LIVE`, a potem `DEMO_RF_PCM.cmd`. Wyjście adaptera jest oznaczone jako `demo`; nie jest to sygnał z eteru. Do prawdziwego testu przygotuj osobno `tools\rtl_fm\rtl_fm.exe` i legalnie pozyskane zależności odpowiedniej kompilacji, sterownik WinUSB dla tunera i uruchom `URUCHOM_RF_ADAPTER.cmd` przy włączonym LIVE. Nie korzystaj z tego do zastosowań ratowniczych.
