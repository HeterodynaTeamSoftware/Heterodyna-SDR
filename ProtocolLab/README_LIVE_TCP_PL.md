# Heterodyna SDR — Protocol Lab 0.2.6 LIVE LOCAL TCP

TCP IPv4 tylko 127.0.0.1:17362. JSONL UTF-8, jeden obiekt na linię. Maksymalna długość linii 16384 bajtów; do czterech połączeń i bufor 1024 zdarzeń. Połączenie jest nieszyfrowane i bez uwierzytelniania, dlatego nie wystawiaj portu do LAN/Internetu.

Obsługiwane dane to już zdemodulowane ramki i metadane, nie IQ. Typ `event` jest oznaczany jako UPSTREAM / NIEZWERYFIKOWANE. Typ `rf_metrics` przekazuje poziom audio PCM, nie moc RF w dBm.

To moduł eksperymentalny; nie używać do rzeczywistych alertów GMDSS/SafetyNET, nawigacji ani łączności ratunkowej.
