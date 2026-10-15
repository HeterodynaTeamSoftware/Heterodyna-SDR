# Kod źródłowy Heterodyna SDR — wydanie Windows / Protocol Lab 0.3.3

Ten katalog jest przygotowany do dodania do publicznego repozytorium:
https://github.com/HeterodynaTeamSoftware/Heterodyna-SDR

## Co zawiera

- **Protocol Lab 0.3.3** — kod źródłowy dostępnego modułu C# (.NET 8, WinForms): GUI, parsery, lokalny LIVE TCP, adapter audio PCM, testy syntetyczne, dane testowe i skrypty Windows.
- **Nie zawiera kodu głównej aplikacji `HeterodynaSDRScanner.exe v01.001.0062`**. Starszy dostarczony ZIP miał tylko EXE/DLL tego programu. Nie twierdzimy, że publikacja tego modułu oznacza udostępnienie całego SDR.
- **Bez binarek i danych użytkownika**: nie dołączamy EXE, DLL, PDB, nagrań IQ, plików profili, starych logów, build/obj, narzędzi stron trzecich.

## Uruchomienie na Windows

1. Zainstaluj [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) na Windows 10/11 x64.
2. Uruchom `KOMPILUJ_WINDOWS.cmd`.
3. Po powodzeniu uruchom `URUCHOM_PROTOCOLLAB.cmd`.
4. Szczegóły: [INSTRUKCJA_KOMPILACJI_WINDOWS.md](INSTRUKCJA_KOMPILACJI_WINDOWS.md).

## Licencja — GNU GPL v3

**Kod źródłowy Protocol Lab jest udostępniany przez Heterodyna Team Software na warunkach GNU General Public License, wersja 3 (`GPL-3.0-only`).** Pełny tekst znajduje się w [LICENSE](LICENSE).

Copyright (c) 2026 Heterodyna Team Software — w zakresie przysługujących praw autorskich.

Licencja obejmuje kod, do którego Heterodyna Team Software ma prawo udzielić licencji. Nie obejmuje cudzych sterowników, bibliotek ani opcjonalnego `rtl_fm`.
