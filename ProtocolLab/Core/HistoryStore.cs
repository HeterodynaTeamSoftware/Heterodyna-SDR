using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Heterodyna.ProtocolLab.Core;

/// <summary>Ręczny zapis/odczyt historii. Bez automatycznego zapisu danych odbiorcy.</summary>
public static class HistoryStore
{
    public const int MaxEvents = 10000;
    public const long MaxFileBytes = 32L * 1024 * 1024;

    public static void Save(string path, IEnumerable<DecodeEvent> items)
    {
        string target = Path.GetFullPath(path);
        string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                int count = 0;
                foreach (var item in items)
                {
                    if (++count > MaxEvents) throw new ArgumentException("Limit historii: 10 000 zdarzeń.");
                    if (item == null) continue;
                    writer.WriteLine(item.ToJson());
                    writer.Flush();
                    if (stream.Length > MaxFileBytes) throw new IOException("Historia przekracza 32 MiB.");
                }
            }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static List<DecodeEvent> Load(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Nie znaleziono pliku historii.", path);
        if (info.Length > MaxFileBytes) throw new IOException("Limit historii: 32 MiB.");
        var output = new List<DecodeEvent>();
        int lineNumber = 0;
        foreach (string line in File.ReadLines(path, Encoding.UTF8))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (output.Count >= MaxEvents) throw new FormatException("Historia przekracza 10 000 zdarzeń.");
            if (line.Length > 4 * 1024 * 1024)
                throw new FormatException($"Wiersz {lineNumber} jest zbyt długi.");
            DecodeEvent parsed;
            try { parsed = JsonSerializer.Deserialize<DecodeEvent>(line); }
            catch (JsonException ex) { throw new FormatException($"Nieprawidłowy JSON w wierszu {lineNumber}.", ex); }
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Protocol))
                throw new FormatException($"Nieprawidłowe zdarzenie historii w wierszu {lineNumber}.");
            parsed.Fields ??= new Dictionary<string, string>();
            output.Add(parsed);
        }
        return output;
    }

    public static void SaveCsv(string path, IEnumerable<DecodeEvent> items)
    {
        string target = Path.GetFullPath(path);
        string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                writer.WriteLine("UTC;Protokol;Typ;Status;Opis;Zrodlo;Dane");
                int count = 0;
                foreach (var item in items)
                {
                    if (++count > MaxEvents) throw new ArgumentException("Limit eksportu: 10 000 zdarzeń.");
                    writer.WriteLine(string.Join(";", new[] {
                        Escape(item.TimestampUtc.UtcDateTime.ToString("O")), Escape(item.Protocol),
                        Escape(item.Kind), Escape(item.Status), Escape(item.Summary),
                        Escape(item.Source), Escape(item.Payload) }));
                    if (count % 256 == 0)
                    {
                        writer.Flush();
                        if (stream.Length > MaxFileBytes) throw new IOException("CSV przekracza 32 MiB.");
                    }
                }
                writer.Flush();
                if (stream.Length > MaxFileBytes) throw new IOException("CSV przekracza 32 MiB.");
            }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static string Escape(string value)
    {
        value ??= "";
        string candidate = value.TrimStart(' ', '\t', '\r', '\n');
        if (candidate.Length > 0 && (candidate[0] is '=' or '+' or '-' or '@'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
