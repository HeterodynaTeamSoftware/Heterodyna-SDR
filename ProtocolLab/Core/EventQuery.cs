using System;
using System.Linq;

namespace Heterodyna.ProtocolLab.Core;

/// <summary>Filtry wyników operujące wyłącznie na już sparsowanych zdarzeniach.
/// Nie stanowią dekodowania RF ani potwierdzenia treści komunikatu.</summary>
public static class EventQuery
{
    public static bool Matches(DecodeEvent ev, string protocol, string search,
        int integrityIndex, int periodIndex, DateTimeOffset utcNow)
    {
        if (ev == null) return false;
        string p = ev.Protocol ?? "";
        bool matchesProtocol = protocol switch
        {
            "WSZYSTKIE" or null or "" => true,
            "ACARS" => p.StartsWith("ACARS", StringComparison.OrdinalIgnoreCase),
            "ADS-C" => p.Contains("ADS-C", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("ATS", StringComparison.OrdinalIgnoreCase),
            "EGC" => p.Contains("EGC", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("SafetyNET", StringComparison.OrdinalIgnoreCase),
            "GMDSS" => p.StartsWith("GMDSS", StringComparison.OrdinalIgnoreCase),
            _ => p.Equals(protocol, StringComparison.OrdinalIgnoreCase)
        };
        if (!matchesProtocol) return false;

        string st = ev.Status ?? "";
        bool bad = st.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
            st.Contains("BŁĘDNE", StringComparison.OrdinalIgnoreCase) ||
            st.Contains("BLEDNE", StringComparison.OrdinalIgnoreCase);
        bool crcOk = st.Contains("CRC OK", StringComparison.OrdinalIgnoreCase) ||
            st.Contains("CHECKSUM OK", StringComparison.OrdinalIgnoreCase);
        if (integrityIndex == 1 && !crcOk) return false;
        if (integrityIndex == 2 && !bad) return false;
        if (integrityIndex == 3 && (bad || crcOk)) return false;

        TimeSpan? maxAge = periodIndex switch
        {
            1 => TimeSpan.FromHours(24),
            2 => TimeSpan.FromDays(7),
            3 => TimeSpan.FromDays(30),
            _ => null
        };
        if (maxAge.HasValue && (ev.TimestampUtc > utcNow || ev.TimestampUtc < utcNow - maxAge.Value))
            return false;

        search = (search ?? "").Trim();
        if (search.Length == 0) return true;
        if (search.Length > 300) return false;
        if (Contains(p, search) || Contains(st, search) ||
            Contains(ev.Kind, search) || Contains(ev.Summary, search) ||
            Contains(ev.Source, search) || Contains(ev.Payload, search)) return true;
        return ev.Fields != null && ev.Fields.Any(f => Contains(f.Key, search) || Contains(f.Value, search));
    }

    private static bool Contains(string value, string search) =>
        (value ?? "").Contains(search, StringComparison.OrdinalIgnoreCase);
}
