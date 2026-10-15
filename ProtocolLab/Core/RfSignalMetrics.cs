using System;

namespace Heterodyna.ProtocolLab.Core;

public sealed record RfTelemetry(long FrequencyHz, int SampleRate, double RmsDbfs,
    double PeakDbfs, long Samples, string Source, DateTimeOffset ReceivedUtc);

public sealed record RfLevelSnapshot(long Samples, double RmsDbfs, double PeakDbfs);

public sealed class Pcm16LevelMeter
{
    private long samples;
    private double sumSquares;
    private int peak;
    private int pendingLow = -1;

    public void Add(ReadOnlySpan<byte> data)
    {
        int i = 0;
        if (pendingLow >= 0 && data.Length > 0)
        {
            AddSample(unchecked((short)(pendingLow | (data[0] << 8))));
            pendingLow = -1;
            i = 1;
        }
        for (; i + 1 < data.Length; i += 2)
            AddSample(unchecked((short)(data[i] | (data[i + 1] << 8))));
        if (i < data.Length) pendingLow = data[i];
    }

    private void AddSample(short pcm)
    {
        int v = Math.Abs((int)pcm);
        if (v > peak) peak = v;
        sumSquares += (double)pcm * pcm;
        samples++;
    }

    public RfLevelSnapshot TakeSnapshot()
    {
        var result = new RfLevelSnapshot(samples,
            ToDbfs(samples == 0 ? 0 : Math.Sqrt(sumSquares / samples)), ToDbfs(peak));
        samples = 0;
        sumSquares = 0;
        peak = 0;
        return result;
    }

    private static double ToDbfs(double magnitude)
    {
        if (magnitude <= 0) return -120;
        return Math.Max(-120, Math.Min(0, 20 * Math.Log10(magnitude / 32768.0)));
    }
}
