using System;
using System.Text.Json;

namespace Heterodyna.ProtocolLab.Core;

/// <summary>Wlasny, wersjonowany format komunikatow JSONL na lokalnym porcie TCP.</summary>
public sealed record LiveWirePacket(string Type, string Text, string Protocol, string Summary, string Payload,
    string ClaimedStatus, string Sender, RfTelemetry Telemetry = null);

public static class LiveWireProtocol
{
    public const int Version = 1;
    public const int MaxLineBytes = 16384;

    public static bool TryParse(ReadOnlySpan<byte> utf8, out LiveWirePacket packet, out string error)
    {
        packet = null;
        error = "";
        if (utf8.IsEmpty || utf8.Length > MaxLineBytes) { error = "Niepoprawny rozmiar ramki JSONL."; return false; }
        try
        {
            using var doc = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            var obj = doc.RootElement;
            if (obj.ValueKind != JsonValueKind.Object) { error = "Ramka nie jest obiektem."; return false; }
            if (!obj.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number ||
                !v.TryGetInt32(out int ver) || ver != Version)
            { error = "Nieobsługiwana wersja protokołu (wymagane v=1)."; return false; }
            string type = StringField(obj, "type");
            if (type == "hello")
            {
                string sender = StringField(obj, "sender");
                if (sender.Length > 80) { error = "Zbyt długa nazwa nadawcy."; return false; }
                packet = new LiveWirePacket(type, "", "", "", "", "", sender);
                return true;
            }
            if (type == "text")
            {
                string text = StringField(obj, "text");
                if (string.IsNullOrWhiteSpace(text) || text.Length > 12000)
                { error = "Niepoprawne lub zbyt długie pole text."; return false; }
                packet = new LiveWirePacket(type, text, "", "", "", "", "");
                return true;
            }
            if (type == "event")
            {
                string proto = StringField(obj, "protocol");
                string summary = StringField(obj, "summary");
                string payload = StringField(obj, "payload");
                string status = StringField(obj, "status");
                if (string.IsNullOrWhiteSpace(proto) || proto.Length > 80 ||
                    string.IsNullOrWhiteSpace(summary) || summary.Length > 512 || payload.Length > 12000 || status.Length > 160)
                { error = "Niepoprawne pola event."; return false; }
                packet = new LiveWirePacket(type, "", proto, summary, payload, status, "");
                return true;
            }
            if (type == "rf_metrics")
            {
                if (!TryLong(obj, "frequency_hz", out long freq) || freq < 24000000 || freq > 1800000000 ||
                    !TryInt(obj, "sample_rate", out int rate) || rate < 8000 || rate > 288000 ||
                    !TryDouble(obj, "rms_dbfs", out double rms) || rms < -120 || rms > 0 ||
                    !TryDouble(obj, "peak_dbfs", out double peak) || peak < -120 || peak > 0 || peak + 0.05 < rms ||
                    !TryLong(obj, "samples", out long count) || count < 1 || count > 10000000)
                { error = "Niepoprawna telemetria PCM / zakres liczb."; return false; }
                string source = StringField(obj, "source");
                if (source != "rtl_fm" && source != "demo")
                { error = "Nieobsługiwane źródło telemetrii RF."; return false; }
                packet = new LiveWirePacket(type, "", "", "", "", "", "",
                    new RfTelemetry(freq, rate, rms, peak, count, source, DateTimeOffset.MinValue));
                return true;
            }
            error = "Nieznany typ wiadomości.";
            return false;
        }
        catch (JsonException) { error = "Błędny JSON lub UTF-8."; return false; }
        catch (InvalidOperationException) { error = "Niepoprawny typ pola JSON."; return false; }
    }

    private static bool TryLong(JsonElement obj, string name, out long result)
    {
        result = 0;
        return obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out result);
    }
    private static bool TryInt(JsonElement obj, string name, out int result)
    {
        result = 0;
        return obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }
    private static bool TryDouble(JsonElement obj, string name, out double result)
    {
        result = 0;
        return obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out result) && double.IsFinite(result);
    }
    private static string StringField(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value)) return "";
        if (value.ValueKind != JsonValueKind.String) throw new InvalidOperationException("Pole " + name + " musi być tekstem.");
        return value.GetString() ?? "";
    }
}
