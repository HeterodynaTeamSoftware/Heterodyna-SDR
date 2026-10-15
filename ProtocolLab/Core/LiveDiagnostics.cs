using System;
using System.Text.Json;

namespace Heterodyna.ProtocolLab.Core;

/// <summary>Snapshot tylko-do-odczytu licznikow lokalnego mostu TCP JSONL.</summary>
/// <remarks>Nie zawiera surowych ramek, danych IQ, identyfikatorow nadawcow ani hasel.</remarks>
public sealed record LiveDiagnostics(
    DateTimeOffset SampledUtc,
    bool Listening,
    int Port,
    int ConnectedClients,
    long Connections,
    long Disconnections,
    long Lines,
    long Accepted,
    long Rejected,
    long Oversized,
    long Dropped,
    int Pending,
    DateTimeOffset? LastPacketUtc,
    string LastError,
    DateTimeOffset? LastRfUtc,
    string LastRfSource,
    string LastRejectedReason)
{
    public string State => DetermineState(SampledUtc, Listening, ConnectedClients, LastPacketUtc, LastError);

    public static string DetermineState(DateTimeOffset now, bool listening, int clients,
        DateTimeOffset? lastPacket, string error)
    {
        if (!string.IsNullOrWhiteSpace(error)) return "BŁĄD";
        if (!listening) return "WYŁĄCZONY";
        if (clients <= 0) return "OCZEKIWANIE NA KLIENTA";
        if (lastPacket is null) return "KLIENT POŁĄCZONY — BRAK PAKIETÓW";
        if ((now - lastPacket.Value) > TimeSpan.FromSeconds(10)) return "BRAK NOWYCH PAKIETÓW";
        return "ODBIÓR JSONL";
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
