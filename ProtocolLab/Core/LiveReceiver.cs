using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Heterodyna.ProtocolLab.Core;

public sealed class LiveReceiver : IDisposable
{
    public const int DefaultPort = 17362;
    public const int MaxQueue = 1024;
    public const int MaxClients = 4;
    private readonly ConcurrentQueue<DecodeEvent> queue = new();
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private CancellationTokenSource cancellation;
    private TcpListener listener;
    private int pending;
    private int active;
    private int running;
    private long lines;
    private long accepted;
    private long rejected;
    private long dropped;
    private long connections;
    private long disconnections;
    private long oversizedLines;
    private long lastPacketTicks;
    private string lastError = "";
    private string lastRejectedReason = "";
    private RfTelemetry lastRf;
    private int disposed;
    private int sessionId;

    public bool IsRunning => Volatile.Read(ref running) == 1;
    public int Port { get; private set; }
    public int ActiveClients => Volatile.Read(ref active);
    public int PendingEvents => Volatile.Read(ref pending);
    public long ReceivedLines => Interlocked.Read(ref lines);
    public long AcceptedMessages => Interlocked.Read(ref accepted);
    public long RejectedMessages => Interlocked.Read(ref rejected);
    public long DroppedEvents => Interlocked.Read(ref dropped);
    public long TotalConnections => Interlocked.Read(ref connections);
    public long TotalDisconnections => Interlocked.Read(ref disconnections);
    public long OversizedLines => Interlocked.Read(ref oversizedLines);
    public DateTimeOffset? LastPacketUtc { get { long ticks = Interlocked.Read(ref lastPacketTicks); return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero); } }
    public string LastError => Volatile.Read(ref lastError);
    public string LastRejectedReason => Volatile.Read(ref lastRejectedReason);
    public RfTelemetry LastRfTelemetry => Volatile.Read(ref lastRf);

    public LiveDiagnostics GetDiagnostics()
    {
        var rf = LastRfTelemetry;
        return new LiveDiagnostics(DateTimeOffset.UtcNow, IsRunning, Port, ActiveClients,
            TotalConnections, TotalDisconnections, ReceivedLines, AcceptedMessages,
            RejectedMessages, OversizedLines, DroppedEvents, PendingEvents,
            LastPacketUtc, LastError, rf?.ReceivedUtc, rf?.Source ?? "", LastRejectedReason);
    }

    public void Start(int port = DefaultPort)
    {
        if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(LiveReceiver));
        if (IsRunning) throw new InvalidOperationException("Odbiornik już pracuje.");
        if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var tcp = new TcpListener(IPAddress.Loopback, port);
        tcp.Start(MaxClients);
        var cts = new CancellationTokenSource();
        int session = Interlocked.Increment(ref sessionId);
        Interlocked.Exchange(ref lastPacketTicks, 0);
        Volatile.Write(ref lastRf, null);
        listener = tcp;
        cancellation = cts;
        Port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        Volatile.Write(ref lastError, "");
        Volatile.Write(ref lastRejectedReason, "");
        Volatile.Write(ref running, 1);
        _ = AcceptLoopAsync(tcp, cts.Token, session);
    }

    private async Task AcceptLoopAsync(TcpListener tcp, CancellationToken token, int session)
    {
        try
        {
            while (!token.IsCancellationRequested && session == Volatile.Read(ref sessionId))
            {
                TcpClient client = await tcp.AcceptTcpClientAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested || session != Volatile.Read(ref sessionId))
                {
                    client.Dispose();
                    break;
                }
                if (Interlocked.Increment(ref active) > MaxClients)
                {
                    Interlocked.Decrement(ref active);
                    client.Dispose();
                    Interlocked.Increment(ref rejected);
                    continue;
                }
                client.NoDelay = true;
                clients.TryAdd(client, 0);
                Interlocked.Increment(ref connections);
                _ = ReadClientAsync(client, token, session);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        catch (SocketException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (session == Volatile.Read(ref sessionId))
            {
                Volatile.Write(ref lastError, "Błąd gniazda: " + ex.Message);
                Volatile.Write(ref running, 0);
            }
        }
    }

    private async Task ReadClientAsync(TcpClient client, CancellationToken token, int session)
    {
        var buffer = new byte[4096];
        using var line = new MemoryStream();
        bool overflow = false;
        try
        {
            using var stream = client.GetStream();
            while (!token.IsCancellationRequested && session == Volatile.Read(ref sessionId))
            {
                int count = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (count == 0) break;
                for (int i = 0; i < count; i++)
                {
                    if (session != Volatile.Read(ref sessionId) || token.IsCancellationRequested) break;
                    byte b = buffer[i];
                    if (b == (byte)'\n')
                    {
                        Interlocked.Increment(ref lines);
                        if (!overflow)
                        {
                            var bytes = line.ToArray();
                            if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
                                Array.Resize(ref bytes, bytes.Length - 1);
                            ProcessLine(bytes, session);
                        }
                        line.SetLength(0);
                        overflow = false;
                    }
                    else if (!overflow)
                    {
                        if (line.Length >= LiveWireProtocol.MaxLineBytes)
                        {
                            overflow = true;
                            Interlocked.Increment(ref rejected);
                            Interlocked.Increment(ref oversizedLines);
                            if (session == Volatile.Read(ref sessionId))
                                Volatile.Write(ref lastRejectedReason, "Linia JSONL przekracza 16384 bajtów.");
                            line.SetLength(0);
                        }
                        else line.WriteByte(b);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        catch (SocketException) { }
        finally
        {
            clients.TryRemove(client, out _);
            client.Dispose();
            if (session == Volatile.Read(ref sessionId))
            {
                if (Interlocked.Decrement(ref active) == 0)
                {
                    Interlocked.Exchange(ref lastPacketTicks, 0);
                    Volatile.Write(ref lastRf, null);
                }
            }
            Interlocked.Increment(ref disconnections);
        }
    }

    private void ProcessLine(byte[] bytes, int session)
    {
        if (session != Volatile.Read(ref sessionId) || !IsRunning) return;
        if (!LiveWireProtocol.TryParse(bytes, out var packet, out string reason))
        {
            if (session != Volatile.Read(ref sessionId)) return;
            Volatile.Write(ref lastRejectedReason, reason);
            Interlocked.Increment(ref rejected);
            return;
        }
        if (session != Volatile.Read(ref sessionId) || !IsRunning) return;
        Interlocked.Exchange(ref lastPacketTicks, DateTimeOffset.UtcNow.UtcTicks);
        if (packet.Type == "hello")
        {
            Interlocked.Increment(ref accepted);
            return;
        }
        if (packet.Type == "rf_metrics")
        {
            Volatile.Write(ref lastRf, packet.Telemetry with { ReceivedUtc = DateTimeOffset.UtcNow });
            Interlocked.Increment(ref accepted);
            return;
        }
        List<DecodeEvent> events;
        try
        {
            if (packet.Type == "text")
                events = InputRouter.ParseText(packet.Text, "LIVE TCP 127.0.0.1");
            else
            {
                events = new List<DecodeEvent> { new DecodeEvent
                {
                    Protocol = packet.Protocol,
                    Kind = "Zdarzenie z zewnątrz",
                    Status = "UPSTREAM / NIEZWERYFIKOWANE",
                    Source = "LIVE TCP 127.0.0.1",
                    Summary = packet.Summary,
                    Payload = packet.Payload,
                    Fields = new Dictionary<string, string> {
                        ["deklarowany_status_nadawcy"] = packet.ClaimedStatus,
                        ["weryfikacja"] = "Brak weryfikacji źródła radiowego / FEC / CRC przez Protocol Lab"
                    }
                } };
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            if (session != Volatile.Read(ref sessionId)) return;
            Volatile.Write(ref lastRejectedReason, "Błąd dekodowania tekstu / niepoprawne pola wejściowe.");
            Interlocked.Increment(ref rejected);
            return;
        }
        if (events.Count == 0)
        {
            Volatile.Write(ref lastRejectedReason, "Brak rozpoznanych zdarzeń w poprawnej ramce.");
            Interlocked.Increment(ref rejected);
            return;
        }
        Interlocked.Increment(ref accepted);
        foreach (var ev in events)
        {
            if (Interlocked.Increment(ref pending) > MaxQueue)
            {
                Interlocked.Decrement(ref pending);
                Interlocked.Increment(ref dropped);
                continue;
            }
            queue.Enqueue(ev);
        }
    }

    public bool TryDequeue(out DecodeEvent ev)
    {
        if (queue.TryDequeue(out ev))
        {
            Interlocked.Decrement(ref pending);
            return true;
        }
        ev = null;
        return false;
    }

    public void Stop()
    {
        Interlocked.Increment(ref sessionId);
        Volatile.Write(ref running, 0);
        Interlocked.Exchange(ref active, 0);
        Interlocked.Exchange(ref lastPacketTicks, 0);
        Volatile.Write(ref lastRf, null);
        try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
        try { listener?.Stop(); } catch (ObjectDisposedException) { }
        foreach (var client in clients.Keys) client.Dispose();
        listener = null;
        cancellation = null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Stop();
    }
}
