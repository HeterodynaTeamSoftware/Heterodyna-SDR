using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Heterodyna.ProtocolLab.Core;

public static class MaritimeDecoders
{
    private static readonly Regex EgcHeader = new(
        @"^\s*EGC[\s,:=-]*(?:C1\s*=\s*)?(?<p>[0-3])\s*[:;,]\s*(?:C2\s*=\s*)?(?<svc>\d{2})\s*[:;,]\s*(?:C3\s*=\s*)?(?<area>[A-Z0-9]{2,12})\s*[:;,]\s*(?:C4\s*=\s*)?(?<rep>\d{2})\s*[:;,]\s*(?:C5\s*=\s*)?(?<pres>\d{1,2})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static DecodeEvent ParseEgc(string input, string source)
    {
        if (input == null || input.Length > 65536 || input.IndexOf("EGC", StringComparison.OrdinalIgnoreCase) < 0)
            return null;
        Match m = EgcHeader.Match(input);
        if (!m.Success) return null;
        string pri = m.Groups["p"].Value;
        string label = pri switch { "3" => "DISTRESS", "2" => "URGENCY", "1" => "SAFETY", _ => "ROUTINE" };
        string svc = m.Groups["svc"].Value;
        string family = svc switch { "14" => "Distress alert relay", "13" => "Coastal warning/forecast", "04" => "Area warning", "24" => "Circular area warning", "31" => "NAV/MET/Safety traffic", "34" => "SAR rectangular area", "44" => "SAR circular area", "00" => "General call", _ => "Service " + svc };
        return new DecodeEvent
        {
            Protocol = "STD-C / EGC SafetyNET", Kind = "Nagłówek C1–C5 (tekst po demodulacji)",
            Status = "NAGŁÓWEK ROZPOZNANY / NIE ODEBRANO WARSTWY RADIOWEJ",
            Source = source, Summary = label + " | " + family + " | area " + m.Groups["area"].Value,
            Payload = input,
            Fields = {
                ["C1 priorytet"] = pri + " " + label,
                ["C2 usługa"] = svc + " " + family,
                ["C3 obszar"] = m.Groups["area"].Value,
                ["C4 retransmisje"] = m.Groups["rep"].Value,
                ["C5 prezentacja"] = m.Groups["pres"].Value,
                ["dekodowanie STD-C BPSK/FEC"] = "NIEZAIMPLEMENTOWANE"
            }
        };
    }

    public static DecodeEvent ParseDsc(string input, string source)
    {
        string s = (input ?? "").Trim();
        if (s.Length > 2048 || s.Any(c => c > 127)) return null;
        if (!s.StartsWith("$", StringComparison.Ordinal) || s.Length < 10) return null;
        int star = s.IndexOf('*');
        if (star < 0 || star + 3 != s.Length) return null;
        string talker = s.Length >= 6 ? s.Substring(3, 3).ToUpperInvariant() : "";
        if (talker != "DSC" && talker != "DSE") return null;
        byte sum = 0;
        foreach (char c in s.Substring(1, star - 1)) sum ^= (byte)c;
        if (!byte.TryParse(s.Substring(star + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out byte expected))
            return null;
        bool ok = sum == expected;
        string[] fields = s.Substring(1, star - 1).Split(',');
        if (fields.Length < 3) return null;
        string station = fields[2].Trim();
        string mmsi = station.Length == 10 && station.EndsWith("0") && station.All(char.IsDigit) ? station[..^1] : station;
        string format = fields.Length > 1 ? fields[1] : "";
        string category = fields.Length > 3 ? fields[3] : "";
        string nature = fields.Length > 4 ? fields[4] : "";
        string natureText = nature switch { "05" => "Sinking (tonięcie)", "00" => "Unspecified (nieokreślone)", _ => "kod " + nature };
        return new DecodeEvent
        {
            Protocol = "GMDSS / DSC", Kind = talker == "DSC" ? "NMEA 0183 DSC" : "NMEA 0183 DSE",
            Status = ok ? "NMEA CHECKSUM OK" : "NMEA CHECKSUM BŁĘDNE", Source = source,
            Summary = "DSC " + format + " | MMSI " + mmsi + " | " + natureText,
            Payload = s,
            Fields = {
                ["format"] = format,
                ["MMSI (normalizowany gdy 10 cyfr i zero końcowe)"] = mmsi,
                ["kategoria"] = category,
                ["natura zagrożenia"] = natureText,
                ["checksum obliczona"] = sum.ToString("X2"),
                ["checksum z ramki"] = expected.ToString("X2"),
                ["odbiór radiowy DSC FSK"] = "NIEZAIMPLEMENTOWANY"
            }
        };
    }

    public static DecodeEvent ParseNavtex(string input, string source)
    {
        string s = input ?? "";
        if (s.Length > 65536) return null;
        Match m = Regex.Match(s, @"\bZCZC\s+(?<station>[A-Z])(?<category>[A-Z])(?<num>\d{2})\b", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        string category = m.Groups["category"].Value.ToUpperInvariant();
        string type = category switch { "A" => "Ostrzeżenie nawigacyjne", "B" => "Ostrzeżenie meteorologiczne", "D" => "SAR / distress", "E" => "Prognoza meteorologiczna", _ => "Kategoria " + category };
        return new DecodeEvent
        {
            Protocol = "GMDSS / NAVTEX", Kind = "Treść NAVTEX", Source = source,
            Status = "NAGŁÓWEK ROZPOZNANY / BEZ ODBIORU RADIOWEGO",
            Summary = m.Groups["station"].Value + category + m.Groups["num"].Value + " | " + type,
            Payload = s,
            Fields = {
                ["stacja"] = m.Groups["station"].Value,
                ["kategoria"] = category + " " + type,
                ["numer"] = m.Groups["num"].Value,
                ["stopka NNNN"] = Regex.IsMatch(s, @"\bNNNN\b") ? "obecna" : "brak",
                ["demodulacja NAVTEX 100 baud"] = "NIEZAIMPLEMENTOWANA"
            }
        };
    }

    public static DecodeEvent ParseSatvoice(string input, string source)
    {
        if (input == null || input.Length > 65536 || !input.TrimStart().StartsWith("SATVOICE,", StringComparison.OrdinalIgnoreCase)) return null;
        var items = input.Split(',', 5, StringSplitOptions.TrimEntries);
        if (items.Length < 3 || string.IsNullOrWhiteSpace(items[1]) || string.IsNullOrWhiteSpace(items[2])) return null;
        string id = items[1], state = items[2];
        return new DecodeEvent
        {
            Protocol = "SATVOICE", Kind = "Sygnalizacja z importowanego logu", Source = source,
            Status = "METADANE / BRAK DEKODERA MOWY",
            Summary = "Call " + id + " | " + state, Payload = input,
            Fields = {
                ["call ID"] = id, ["stan"] = state,
                ["częstotliwość"] = items.Length > 3 ? items[3] : "",
                ["kontekst"] = items.Length > 4 ? items[4] : "",
                ["audio / szyfrowanie"] = "NIEDEKODOWANE"
            }
        };
    }
}
