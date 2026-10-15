using System;
using System.Text.RegularExpressions;

namespace Heterodyna.ProtocolLab.Core;

public static class AtsAdscDecoder
{
    // ARINC-622 ATS: / + 7-char ground addr + 3-char IMI + 7-char aircraft addr
    // + HEX data + four HEX digits (trailer). Parsing is not ADS-C semantic decoding.
    private static readonly Regex Ats = new(
        @"/(?<ground>[A-Z0-9]{7})(?<imi>ADS|AT1|CR1|DIS|FML|FMR)(?<air>[A-Z0-9\-]{7})(?<hex>[0-9A-F]{8,4096})(?![0-9A-F])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    public static DecodeEvent Parse(string text, string source)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 65536) return null;
        Match m;
        try { m = Ats.Match(text); }
        catch (RegexMatchTimeoutException) { return null; }
        if (!m.Success) return null;
        string body = m.Groups["hex"].Value;
        if (body.Length < 8 || body.Length % 2 != 0) return null;
        string imi = m.Groups["imi"].Value.ToUpperInvariant();
        var ev = new DecodeEvent
        {
            Protocol = imi == "ADS" ? "ADS-C" : "ATS/ARINC-622",
            Kind = "ARINC-622: obwiednia", Source = source,
            Status = "KONTENER ODCZYTANY / BEZ DEKODOWANIA PÓL BINARNYCH",
            Summary = imi + " " + m.Groups["air"].Value + " → " + m.Groups["ground"].Value,
            Payload = body[..^4]
        };
        ev.Fields["IMI"] = imi;
        ev.Fields["stacja naziemna"] = m.Groups["ground"].Value;
        ev.Fields["adres samolotu"] = m.Groups["air"].Value;
        ev.Fields["dane HEX"] = body[..^4];
        ev.Fields["trailer HEX (niezweryfikowany)"] = body[^4..];
        ev.Fields["pozycja"] = "Nieodczytana – wymaga parsera ADS-C (ARINC 745)";
        ev.Fields["uwaga"] = "Sama obwiednia ARINC-622 nie potwierdza ważności binarnego ADS-C.";
        return ev;
    }
}
