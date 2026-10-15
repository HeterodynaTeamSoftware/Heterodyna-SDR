using Heterodyna.ProtocolLab.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Heterodyna.ProtocolLab.RfAdapter;

internal static class Program
{
    private const int SampleRate = 24000;
    private const int DefaultPort = LiveReceiver.DefaultPort;
    private static readonly TimeSpan PcmReadTimeout = TimeSpan.FromSeconds(12);

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (Array.Exists(args, a => a == "--selftest"))
            {
                RunSelfTest();
                await RunWatchdogSelfTestAsync();
                Console.WriteLine("PASS: PCM16, fragmentacja pipe, zakres dBFS i watchdog timeout (syntetyczne).");
                return 0;
            }
            string mode = GetArg(args, "--mode", "rtl").ToLowerInvariant();
            if (mode != "rtl" && mode != "demo") throw new ArgumentException("--mode musi mieć wartość rtl lub demo.");
            if (!long.TryParse(GetArg(args, "--freq-hz", "131550000"), out long freq) || freq < 24000000 || freq > 1800000000) throw new ArgumentException("Zakres --freq-hz: 24000000..1800000000 Hz.");
            if (!int.TryParse(GetArg(args, "--port", DefaultPort.ToString()), out int port) || port is < 1 or > 65535) throw new ArgumentException("Niepoprawny port lokalny TCP.");
            using var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
            Console.WriteLine("Heterodyna SDR — RF Adapter 0.3.3");
            Console.WriteLine("Źródło: " + mode.ToUpperInvariant() + " | " + freq / 1000000.0 + " MHz | PCM " + SampleRate + " Hz");
            Console.WriteLine("Dane: wyłącznie poziom audio, NIE zdekodowane wiadomości ACARS/SATVOICE.");
            Console.WriteLine("Aby zatrzymać, naciśnij Ctrl+C.");
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port, cancel.Token);
            using var writer = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false), 8192, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { v = 1, type = "hello", sender = "Heterodyna RF Adapter 0.3.3" }));
            if (mode == "demo") await RunDemoAsync(writer, freq, cancel.Token);
            else await RunRtlAsync(writer, freq, cancel.Token);
            return 0;
        }
        catch (OperationCanceledException) { Console.WriteLine("RF Adapter zatrzymany."); return 0; }
        catch (Exception ex) when (ex is IOException or SocketException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("BŁĄD RF ADAPTERA: " + ex.Message);
            Console.Error.WriteLine("Włącz LIVE w Protocol Lab, sprawdź RTL-SDR/WinUSB i czy inny program nie używa tunera.");
            return 1;
        }
    }

    private static string GetArg(string[] args, string key, string fallback)
    {
        for (int i = 0; i < args.Length; i++) if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new ArgumentException("Brak wartości parametru " + key); return args[i + 1]; }
        return fallback;
    }

    private static async Task RunRtlAsync(StreamWriter writer, long freq, CancellationToken token)
    {
        string rtl = FindRtlFm();
        var start = new ProcessStartInfo(rtl) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(rtl)! };
        start.ArgumentList.Add("-M"); start.ArgumentList.Add("fm");
        start.ArgumentList.Add("-f"); start.ArgumentList.Add(freq.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("-s"); start.ArgumentList.Add(SampleRate.ToString());
        start.ArgumentList.Add("-r"); start.ArgumentList.Add(SampleRate.ToString());
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("Nie można uruchomić rtl_fm.exe");
        var stderrTask = Task.Run(async () => { int lines = 0; while (await process.StandardError.ReadLineAsync(token) is { } line) if (lines++ < 80) Console.Error.WriteLine("rtl_fm: " + line); }, CancellationToken.None);
        var meter = new Pcm16LevelMeter();
        var buffer = new byte[8192];
        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (!token.IsCancellationRequested)
            {
                int read = await ReadWithTimeoutAsync(process.StandardOutput.BaseStream, buffer, PcmReadTimeout, token);
                if (read == 0) break;
                meter.Add(buffer.AsSpan(0, read));
                if (stopwatch.ElapsedMilliseconds >= 1000)
                {
                    var snapshot = meter.TakeSnapshot();
                    if (snapshot.Samples > 0) await SendAsync(writer, freq, snapshot, "rtl_fm");
                    stopwatch.Restart();
                }
            }
            if (!token.IsCancellationRequested)
            {
                await process.WaitForExitAsync(token);
                throw new IOException("rtl_fm zakończył działanie (kod " + process.ExitCode + "). Sprawdź informacje stderr.");
            }
        }
        finally
        {
            if (!process.HasExited) try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { await stderrTask.WaitAsync(TimeSpan.FromSeconds(3)); } catch (TimeoutException) { } catch (OperationCanceledException) { }
        }
    }

    private static async Task<int> ReadWithTimeoutAsync(Stream stream, Memory<byte> buffer, TimeSpan timeout, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        linked.CancelAfter(timeout);
        try { return await stream.ReadAsync(buffer, linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException($"Brak nowych próbek PCM z rtl_fm przez {timeout.TotalSeconds:0} sekund. Sprawdź tuner USB i rtl_fm stderr."); }
    }

    private static async Task RunDemoAsync(StreamWriter writer, long freq, CancellationToken token)
    {
        var meter = new Pcm16LevelMeter();
        var pcm = new byte[SampleRate * 2];
        for (int j = 0; j < SampleRate; j++)
        {
            short sample = (short)(Math.Sin(j * 2 * Math.PI * 1000 / SampleRate) * 6000);
            pcm[2 * j] = (byte)sample;
            pcm[2 * j + 1] = (byte)(sample >> 8);
        }
        while (!token.IsCancellationRequested)
        {
            meter.Add(pcm);
            await SendAsync(writer, freq, meter.TakeSnapshot(), "demo");
            await Task.Delay(1000, token);
        }
    }

    private static async Task SendAsync(StreamWriter writer, long freq, RfLevelSnapshot snapshot, string source)
    {
        var line = JsonSerializer.Serialize(new { v = 1, type = "rf_metrics", frequency_hz = freq, sample_rate = SampleRate, rms_dbfs = snapshot.RmsDbfs, peak_dbfs = snapshot.PeakDbfs, samples = snapshot.Samples, source });
        await writer.WriteLineAsync(line);
        Console.WriteLine($"PCM {freq / 1000000.0:F3} MHz | RMS {snapshot.RmsDbfs:F1} dBFS | Peak {snapshot.PeakDbfs:F1} dBFS | {snapshot.Samples} próbek");
    }

    private static string FindRtlFm()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "tools", "rtl_fm", "rtl_fm.exe");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Nie znaleziono tools\\rtl_fm\\rtl_fm.exe. Rozpakuj pełne ZIP, nie sam katalog RfAdapter.");
    }

    private static async Task RunWatchdogSelfTestAsync()
    {
        using var input = new StallingStream();
        try { await ReadWithTimeoutAsync(input, new byte[16], TimeSpan.FromMilliseconds(40), CancellationToken.None); throw new InvalidOperationException("Watchdog nie wykrył zastoju strumienia PCM."); }
        catch (IOException ex) when (ex.Message.Contains("Brak nowych próbek PCM", StringComparison.Ordinal)) { Console.WriteLine("PASS: wykryto zastój PCM bez RTL-SDR."); }
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        try { await ReadWithTimeoutAsync(input, new byte[16], TimeSpan.FromSeconds(1), cancel.Token); throw new InvalidOperationException("Anulowany odczyt PCM nie zakończył się anulowaniem."); }
        catch (OperationCanceledException) { Console.WriteLine("PASS: normalne anulowanie sesji."); }
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static void RunSelfTest()
    {
        var meter = new Pcm16LevelMeter();
        meter.Add(new byte[] { 0, 0, 0 }); meter.Add(new byte[] { 0 });
        var silence = meter.TakeSnapshot();
        if (silence.Samples != 2 || silence.RmsDbfs != -120) throw new InvalidOperationException("Błędna obsługa fragmentacji PCM.");
        meter.Add(new byte[] { 0xff }); meter.Add(new byte[] { 0x7f });
        var sound = meter.TakeSnapshot();
        if (sound.Samples != 1 || sound.PeakDbfs < -0.1 || sound.RmsDbfs < -0.1) throw new InvalidOperationException("Błędna amplituda PCM 16-bit.");
        Console.WriteLine("PASS: adapter audio PCM / split-byte / clipping test.");
    }
}
