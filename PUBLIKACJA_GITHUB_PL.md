# Publikacja źródeł na GitHub — bez dodawania samych plików ZIP

**Ważne:** GitHub powinien zawierać rozpakowane pliki `.cs`, `.csproj` i skrypty. Wgranie tylko archiwum ZIP nie udostępni przeglądalnego kodu.

1. Rozpakuj `Heterodyna_SDR_Windows_ProtocolLab_0.3.3_GPLv3_SOURCE_GITHUB.zip`.
2. Na Windows zainstaluj [Git for Windows](https://git-scm.com/downloads/win).
3. Otwórz PowerShell i pobierz istniejące repo:

```powershell
git clone https://github.com/HeterodynaTeamSoftware/Heterodyna-SDR.git
cd Heterodyna-SDR
```

4. Skopiuj **zawartość rozpakowanego katalogu** `Heterodyna-SDR-ProtocolLab-0.3.3-SOURCE` do tego repozytorium (nie dodatkowy katalog nadrzędny). Nie usuwaj istniejącego README, zrzutów ekranu ani SECURITY.md.
5. Sprawdź, co będzie wysłane, a potem zatwierdź:

```powershell
git status --short
git diff --check
git add .github/workflows/protocollab-windows.yml .gitignore LICENSE ProtocolLab tools/rtl_fm README_SOURCE_PL.md INSTRUKCJA_KOMPILACJI_WINDOWS.md PUBLIKACJA_GITHUB_PL.md KOMPILUJ_WINDOWS.cmd URUCHOM_PROTOCOLLAB.cmd TEST_WINDOWS.cmd URUCHOM_RF_ADAPTER.cmd DEMO_RF_PCM.cmd MANIFEST_SHA256.txt
git diff --cached --stat
git diff --cached --check
git commit -m "Publish Protocol Lab 0.3.3 Windows source and build instructions"
git push origin main
```

**Przed `git push`:** przeczytaj diff, dodaj licencję dopiero po wyborze właściciela, sprawdź licencje kodu/dependencji i upewnij się, że nie publikujesz własnych danych czy kluczy. Jeżeli domyślna gałąź jest chroniona, pracuj na nowej gałęzi i otwórz Pull Request. Konto GitHub użyte do wypchnięcia musi mieć prawo zapisu do repo.

Po publikacji dodaj do istniejącego `README.md` repo zdanie `Kod źródłowy Protocol Lab jest udostępniony na GNU GPL v3 (GPL-3.0-only).` oraz krótki link: `Źródła Protocol Lab (Windows): [README_SOURCE_PL.md](README_SOURCE_PL.md)`.
