using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Heterodyna.ProtocolLab.Core;

public sealed class DecodeEvent
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Protocol { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Source { get; set; } = "";
    public string Payload { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = new();
    public string ToJson() => JsonSerializer.Serialize(this);
}

public static class TestFrames
{
    public static byte[] Acars()
    {
        var bytes = new List<byte> { 0x01 };
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("2.G-ABCD"));
        bytes.Add(0x15);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("H11"));
        bytes.Add(0x02);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("M01ALO1234/LPAFAYAADSVQ-BPJX01020304ABCD"));
        bytes.Add(0x03);
        ushort bcs = AcarsFrameDecoder.Crc16(bytes.Skip(1).ToArray());
        bytes.Add((byte)(bcs & 0xff));
        bytes.Add((byte)(bcs >> 8));
        bytes.Add(0x7f);
        return bytes.ToArray();
    }
}
