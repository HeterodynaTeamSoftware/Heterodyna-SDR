using System;
using System.Collections.Generic;
using System.Linq;

namespace Heterodyna.ProtocolLab.Core;

public static class AcarsBitsDecoder
{
    // Decoder stage AFTER clock recovery/demodulation: searches byte phase,
    // byte bit order and polarity; validates candidates using ACARS BCS.
    // Does not demodulate an analogue IQ/WAV radio signal.
    public static List<DecodeEvent> Decode(string rawBits, string source = "bitstream")
    {
        string bits = new string((rawBits ?? "").Where(c => c == '0' || c == '1').ToArray());
        if (bits.Length < 144) throw new FormatException("Potrzeba przynajmniej 144 bitów.");
        if (bits.Length > 524288) throw new FormatException("Limit: 524288 bitów w jednym zadaniu.");
        var results = new List<DecodeEvent>();
        var fingerprints = new HashSet<string>();
        foreach (bool lsbFirst in new[] { true, false })
        foreach (bool invert in new[] { false, true })
        for (int phase = 0; phase < 8; phase++)
        {
            int total = (bits.Length - phase) / 8;
            byte[] bytes = new byte[total];
            for (int i = 0; i < total; i++)
            {
                byte b = 0;
                int offset = phase + i * 8;
                for (int k = 0; k < 8; k++)
                    if (bits[offset + k] == '1') b |= (byte)(1 << (lsbFirst ? k : 7 - k));
                bytes[i] = invert ? (byte)(b ^ 0xff) : b;
            }
            for (int i = 0; i + 18 <= bytes.Length; i++)
            {
                if ((bytes[i] & 0x7f) != 0x01) continue;
                int stx = -1, end = -1;
                for (int j = i + 13; j < Math.Min(bytes.Length, i + 400); j++)
                {
                    byte ch = (byte)(bytes[j] & 0x7f);
                    if (stx < 0 && ch == 0x02) { stx = j; continue; }
                    if (stx >= 0 && (ch == 0x03 || ch == 0x17)) { end = j; break; }
                }
                if (end < 0 || end + 2 >= bytes.Length) continue;
                byte[] candidate = bytes.Skip(i).Take(Math.Min(end + 4 - i, bytes.Length - i)).ToArray();
                try
                {
                    var ev = AcarsFrameDecoder.Decode(candidate, source);
                    if (ev.Status != "CRC OK") continue;
                    string fingerprint = Convert.ToHexString(candidate);
                    if (!fingerprints.Add(fingerprint)) continue;
                    ev.Kind = "ARINC-618: ramka z bitstreamu";
                    ev.Fields["bit order"] = lsbFirst ? "LSB-first" : "MSB-first";
                    ev.Fields["polarity inverted"] = invert.ToString();
                    ev.Fields["bit phase"] = phase.ToString();
                    results.Add(ev);
                    if (results.Count >= 30) return results;
                    i = end + 2;
                }
                catch (FormatException) { }
            }
        }
        return results;
    }
}
