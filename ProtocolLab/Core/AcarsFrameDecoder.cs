using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Heterodyna.ProtocolLab.Core;

public static class AcarsFrameDecoder
{
    public static ushort Crc16(byte[] data)
    {
        // Reflected CRC-16/CCITT, initial 0, polynomial 0x8408.
        ushort crc = 0;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0x8408 : crc >> 1);
        }
        return crc;
    }

    public static DecodeEvent Decode(byte[] raw, string source = "wejście")
    {
        if (raw == null || raw.Length == 0) throw new FormatException("Pusta ramka ACARS.");
        int start = Array.FindIndex(raw, b => (b & 0x7f) == 0x01);
        if (start < 0) throw new FormatException("Brak SOH (0x01) w ramce ACARS.");
        if (raw.Length - start < 18) throw new FormatException("Za krótka ramka ACARS.");
        if (raw.Length - start > 8192) throw new FormatException("Ramka ACARS przekracza limit 8192 bajtów.");
        byte[] slice = raw.Skip(start).ToArray();
        byte[] clean = slice.Select(x => (byte)(x & 0x7f)).ToArray();
        int stx = Array.IndexOf(clean, (byte)0x02);
        if (stx != 13) throw new FormatException("STX nie znajduje się po 12-bajtowym nagłówku ACARS.");
        int finish = -1;
        for (int i = stx + 1; i < clean.Length; i++)
            if (clean[i] == 0x03 || clean[i] == 0x17) { finish = i; break; }
        if (finish < 0 || finish + 2 >= slice.Length)
            throw new FormatException("Brak ETX/ETB albo 2 bajtów BCS.");

        ushort suppliedBcs = (ushort)(slice[finish + 1] | (slice[finish + 2] << 8));
        byte[] crcAreaRaw = slice.Skip(1).Take(finish).ToArray();
        ushort bcsRaw = Crc16(crcAreaRaw);
        // Nigdy nie akceptuj dopasowania BCS po zamaskowaniu bitów 7/15:
        // takie porównanie traci 2 bity i potrafi fałszywie zatwierdzić uszkodzoną ramkę.
        bool valid = suppliedBcs == bcsRaw;
        string crcMode = valid ? "CRC-16 (pełne 16 bitów)" : "CRC-16 / niedopasowanie";
        var ev = new DecodeEvent
        {
            Protocol = "ACARS", Kind = "ARINC-618: ramka", Source = source,
            Status = valid ? "CRC OK" : "CRC BŁĘDNE",
            Payload = Encoding.ASCII.GetString(clean, stx + 1, finish - stx - 1)
        };
        ev.Fields["tryb"] = CleanText(clean, 1, 1);
        ev.Fields["rejestracja"] = CleanText(clean, 2, 7).Trim('.');
        ev.Fields["ACK/NAK"] = "0x" + clean[9].ToString("X2");
        ev.Fields["etykieta"] = CleanText(clean, 10, 2);
        ev.Fields["blok"] = CleanText(clean, 12, 1);
        ev.Fields["ramka koncowa"] = clean[finish] == 0x03 ? "ETX" : "ETB (kolejny blok)";
        ev.Fields["BCS"] = suppliedBcs.ToString("X4");
        ev.Fields["CRC wariant"] = crcMode;
        ev.Fields["crc_ok"] = valid ? "true" : "false";
        ev.Fields["MSN"] = ev.Payload.Length >= 4 ? ev.Payload[..4] : "";
        ev.Fields["lot"] = ev.Payload.Length >= 10 ? ev.Payload.Substring(4, 6) : "";
        ev.Fields["tekst"] = ev.Payload.Length > 10 ? ev.Payload[10..] : ev.Payload;
        ev.Summary = ev.Fields["rejestracja"] + " | " + ev.Fields["etykieta"] + " | " + ev.Fields["lot"] + " | " + ev.Status;
        return ev;
    }

    private static string CleanText(byte[] b, int offset, int length)
    {
        return Encoding.ASCII.GetString(b, offset, length).Replace('\0', ' ').Trim();
    }

    public static byte[] ParseHex(string text)
    {
        if (text == null) throw new FormatException("Brak danych HEX.");
        string s = new string(text.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != ':' && c != ',').ToArray());
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.Length > 16384) throw new FormatException("Limit ramki HEX: 8192 bajtów.");
        if (s.Length == 0 || s.Length % 2 != 0 || s.Any(c => !Uri.IsHexDigit(c)))
            throw new FormatException("Podaj parzystą liczbę cyfr HEX (opcjonalne spacje).");
        return Convert.FromHexString(s);
    }
}
