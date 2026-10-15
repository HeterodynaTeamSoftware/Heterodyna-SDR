using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Heterodyna.ProtocolLab.Core;

public static class InputRouter
{
    public static List<DecodeEvent> ParseText(string text, string source = "tekst")
    {
        var events = new List<DecodeEvent>();
        if (string.IsNullOrWhiteSpace(text)) return events;
        if (text.Length > 16 * 1024 * 1024) throw new FormatException("Limit importu tekstu: 16 MiB.");
        string input = text.Trim();
        var lines = input.Replace("\r", "").Split('\n');
        var expanded = new List<string>();
        var navtex = new StringBuilder();
        foreach (var originalLine in lines)
        {
            string current = originalLine.Trim();
            if (navtex.Length > 0)
            {
                if (current.StartsWith("ZCZC", StringComparison.OrdinalIgnoreCase) ||
                    current.StartsWith("ACARS_HEX:", StringComparison.OrdinalIgnoreCase) ||
                    current.StartsWith("ACARS_BITS:", StringComparison.OrdinalIgnoreCase) ||
                    current.StartsWith("EGC:", StringComparison.OrdinalIgnoreCase) ||
                    current.StartsWith("SATVOICE,", StringComparison.OrdinalIgnoreCase) ||
                    current.StartsWith("$", StringComparison.Ordinal))
                {
                    expanded.Add(navtex.ToString()); navtex.Clear();
                }
                else
                {
                    navtex.AppendLine(current);
                    if (current.Contains("NNNN", StringComparison.OrdinalIgnoreCase))
                    {
                        expanded.Add(navtex.ToString()); navtex.Clear();
                    }
                    continue;
                }
            }
            if (current.StartsWith("ZCZC", StringComparison.OrdinalIgnoreCase))
            {
                navtex.AppendLine(current);
                if (current.Contains("NNNN", StringComparison.OrdinalIgnoreCase))
                {
                    expanded.Add(navtex.ToString()); navtex.Clear();
                }
            }
            else expanded.Add(current);
        }
        if (navtex.Length > 0) expanded.Add(navtex.ToString());
        foreach (string rawLine in expanded)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            DecodeEvent ev = null;
            if (line.StartsWith("ZCZC", StringComparison.OrdinalIgnoreCase))
            {
                Add(events, MaritimeDecoders.ParseNavtex(line, source));
                continue;
            }
            if (line.StartsWith("ACARS_BITS:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var decoded = AcarsBitsDecoder.Decode(line[11..], source);
                    if (decoded.Count == 0)
                        events.Add(new DecodeEvent { Protocol = "ACARS", Kind = "Bitstream", Source = source,
                            Status = "BRAK POPRAWNEJ RAMKI", Summary = "Nie znaleziono ramek z poprawnym BCS." });
                    foreach (var frameEvent in decoded)
                    {
                        Add(events, frameEvent);
                        Add(events, AtsAdscDecoder.Parse(frameEvent.Fields["tekst"], source));
                    }
                }
                catch (FormatException ex)
                {
                    events.Add(new DecodeEvent { Protocol = "ACARS", Kind = "Bitstream", Source = source,
                        Status = "INVALID", Summary = ex.Message });
                }
                continue;
            }
            if (line.StartsWith("ACARS_HEX:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ev = AcarsFrameDecoder.Decode(AcarsFrameDecoder.ParseHex(line[10..].Trim()), source);
                    Add(events, ev);
                    if (ev.Fields.TryGetValue("crc_ok", out var pass) && pass == "true")
                        Add(events, AtsAdscDecoder.Parse(ev.Fields["tekst"], source));
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
                {
                    events.Add(new DecodeEvent { Protocol = "ACARS", Kind = "Błąd wejścia", Source = source,
                        Status = "INVALID", Summary = ex.Message, Payload = line });
                }
                continue;
            }
            if (line.StartsWith("{", StringComparison.Ordinal) && line.EndsWith("}", StringComparison.Ordinal))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string msg = GetString(root, "text", "msg", "message", "payload");
                    string hex = GetString(root, "acars_hex", "frame_hex");
                    if (!string.IsNullOrWhiteSpace(hex))
                    {
                        ev = AcarsFrameDecoder.Decode(AcarsFrameDecoder.ParseHex(hex), source);
                        Add(events, ev);
                        if (ev.Fields.TryGetValue("crc_ok", out var pass) && pass == "true")
                            Add(events, AtsAdscDecoder.Parse(ev.Fields["tekst"], source));
                    }
                    else if (!string.IsNullOrWhiteSpace(msg))
                    {
                        Add(events, new DecodeEvent { Protocol = "IMPORT / JSON", Kind = "Tekst importowany z dekodera",
                            Status = "IMPORT / CRC NIEZWERYFIKOWANE", Summary = msg.Length > 90 ? msg[..90] : msg,
                            Source = source, Payload = msg });
                        Add(events, AtsAdscDecoder.Parse(msg, source));
                        Add(events, MaritimeDecoders.ParseEgc(msg, source));
                        Add(events, MaritimeDecoders.ParseDsc(msg, source));
                        Add(events, MaritimeDecoders.ParseNavtex(msg, source));
                        Add(events, MaritimeDecoders.ParseSatvoice(msg, source));
                    }
                    else
                    {
                        events.Add(new DecodeEvent { Protocol = "INNE", Kind = "JSON", Status = "IMPORT", Source = source,
                            Summary = "Nieznany format JSON", Payload = line });
                    }
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                {
                    events.Add(new DecodeEvent { Protocol = "JSON", Status = "INVALID", Summary = ex.Message, Source = source });
                }
                catch (FormatException ex)
                {
                    events.Add(new DecodeEvent { Protocol = "ACARS", Status = "INVALID", Summary = ex.Message, Source = source });
                }
                continue;
            }
            Add(events, MaritimeDecoders.ParseDsc(line, source));
            Add(events, MaritimeDecoders.ParseEgc(line, source));
            Add(events, MaritimeDecoders.ParseNavtex(line, source));
            Add(events, MaritimeDecoders.ParseSatvoice(line, source));
            Add(events, AtsAdscDecoder.Parse(line, source));
        }
        return events;
    }

    private static void Add(List<DecodeEvent> items, DecodeEvent item)
    {
        if (item != null) items.Add(item);
    }

    private static string GetString(JsonElement obj, params string[] keys)
    {
        if (obj.ValueKind != JsonValueKind.Object) return "";
        foreach (string key in keys)
            if (obj.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        return "";
    }

    public static List<DecodeEvent> ParseFile(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024)
            throw new IOException("Limit pliku: 16 MiB. Podziel duże logi na mniejsze fragmenty.");
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".bin" || extension == ".raw")
        {
            var frame = AcarsFrameDecoder.Decode(File.ReadAllBytes(path), path);
            var results = new List<DecodeEvent> { frame };
            if (frame.Fields.TryGetValue("crc_ok", out var ok) && ok == "true")
            {
                var ats = AtsAdscDecoder.Parse(frame.Fields["tekst"], path);
                if (ats != null) results.Add(ats);
            }
            return results;
        }
        string fileText = File.ReadAllText(path, Encoding.UTF8);
        if (extension == ".hex" && !fileText.TrimStart().StartsWith("ACARS_HEX:", StringComparison.OrdinalIgnoreCase))
        {
            var frame = AcarsFrameDecoder.Decode(AcarsFrameDecoder.ParseHex(fileText), path);
            var result = new List<DecodeEvent> { frame };
            if (frame.Fields.TryGetValue("crc_ok", out var valid) && valid == "true")
                Add(result, AtsAdscDecoder.Parse(frame.Fields["tekst"], path));
            return result;
        }
        return ParseText(fileText, path);
    }
}
