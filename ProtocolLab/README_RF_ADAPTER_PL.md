# Heterodyna SDR Protocol Lab 0.3.3 — RF Adapter

Adapter odczytuje PCM 16-bit LE z opcjonalnego `tools/rtl_fm/rtl_fm.exe`, oblicza RMS/Peak dBFS i wysyła telemetrię po TCP 127.0.0.1:17362.

To nie jest pełny dekoder ACARS, ADS-C, SATVOICE, SafetyNET ani GMDSS. dBFS dotyczy audio po demodulacji, nie mocy RF w dBm.

Kompilacja: `KOMPILUJ_WINDOWS.cmd`. Demo bez sprzętu: `DEMO_RF_PCM.cmd`. Sprzętowy test wymaga osobno pozyskanego rtl_fm i zależności oraz sterownika WinUSB.
