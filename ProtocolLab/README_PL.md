# Heterodyna SDR — Protocol Lab 0.3.3 (źródła Windows)

To jest osobny program C#/.NET 8 (Windows Forms), nie pełne źródła starego HeterodynaSDRScanner.exe v01.001.0062.

Na Windows 10/11 x64 z .NET 8 SDK uruchom `KOMPILUJ_WINDOWS.cmd`. Kod GUI: `Program.cs`, `Gui/*.cs`; logika protokołów: `Core/*.cs`; adapter: `RfAdapter/`; dane testowe: `TestData/`.

Importowane ACARS, NAVTEX, DSC, EGC i ATS/ADS-C są analizowane tylko w zakresie zaimplementowanych parserów. `rf_metrics` to poziom audio PCM, nie dBm. Serwer LIVE słucha tylko 127.0.0.1:17362.
