using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Heterodyna.ProtocolLab.Core;
public static class CoreSelfTests
{
public static int Run(Action<string> log = null)
{
int passed = 0;
void Check(bool condition, string name)
{
if (!condition) throw new InvalidOperationException("FAIL: " + name);
passed++;
log?.Invoke("OK " + passed + ": " + name);
}
void MustThrow<T>(Action action, string name) where T : Exception
{
try { action(); }
catch (T) { Check(true, name); return; }
Check(false, name);
}
byte[] frame = TestFrames.Acars();
var decoded = AcarsFrameDecoder.Decode(frame, "synthetic");
Check(decoded.Status == "CRC OK", "ACARS: pełny 16-bit BCS");
Check(decoded.Fields["rejestracja"] == "G-ABCD", "ACARS: identyfikator");
Check(decoded.Fields["lot"] == "LO1234", "ACARS: numer lotu");
Check(decoded.Fields["etykieta"] == "H1", "ACARS: label H1");
Check(decoded.Fields["ramka koncowa"] == "ETX", "ACARS: zakończenie ETX");
var corrupted = (byte[])frame.Clone();
corrupted[25] ^= 0x10;
Check(AcarsFrameDecoder.Decode(corrupted).Status == "CRC BŁĘDNE", "ACARS: wykrycie modyfikacji payload");
var bcsChanged = (byte[])frame.Clone();
bcsChanged[^3] ^= 0x80;
Check(AcarsFrameDecoder.Decode(bcsChanged).Status == "CRC BŁĘDNE", "ACARS: nieakceptowanie częściowego CRC");
MustThrow<FormatException>(() => AcarsFrameDecoder.Decode(new byte[] { 0x00, 0x02 }), "ACARS: brak SOH");
MustThrow<FormatException>(() => AcarsFrameDecoder.ParseHex("F"), "HEX: nieparzysta liczba cyfr");
MustThrow<FormatException>(() => AcarsFrameDecoder.ParseHex("01ZX"), "HEX: niedozwolony znak");
Check(AcarsFrameDecoder.ParseHex("0x01-AB,2F").SequenceEqual(new byte[] { 0x01, 0xAB, 0x2F }), "HEX: separatory");
MustThrow<FormatException>(() => AcarsFrameDecoder.ParseHex(new string('A', 16386)), "HEX: limit rozmiaru");
var ats = AtsAdscDecoder.Parse("/LPAFAYAADSVQ-BPJX01020304ABCD", "synthetic");
Check(ats != null && ats.Protocol == "ADS-C", "ADS-C: nagłówek ARINC-622");
Check(ats != null && ats.Fields["dane HEX"] == "01020304", "ADS-C: obwiednia i dane");
Check(ats != null && ats.Status.Contains("BEZ DEKODOWANIA"), "ADS-C: brak fałszywej deklaracji pełnego dekodera");
Check(AtsAdscDecoder.Parse("/LPAFAYAADSVQ-BPJX01020304ABCDX", "synthetic") != null,
"ADS-C: tekst logu za kontenerem");
Check(AtsAdscDecoder.Parse("niepoprawny", "synthetic") == null, "ADS-C: brak fałszywych wykryć");
var egc = MaritimeDecoders.ParseEgc("EGC:3:14:01A0:11:00 DISTRESS", "synthetic");
Check(egc != null && egc.Fields["C1 priorytet"].Contains("DISTRESS"), "EGC: priorytet distress");
Check(MaritimeDecoders.ParseEgc("1:31:01A0:11:00", "synthetic") == null, "EGC: wymagana sygnatura EGC");
Check(MaritimeDecoders.ParseEgc("EGC:1:31:01A0:11:00", "synthetic")?.Protocol.Contains("SafetyNET") == true,
"EGC: wykrycie SafetyNET");
var nav = MaritimeDecoders.ParseNavtex("ZCZC DA01 TEST NNNN", "synthetic");
Check(nav != null && nav.Fields["kategoria"].Contains("Ostrzeżenie"), "NAVTEX: nagłówek");
Check(MaritimeDecoders.ParseNavtex("brak ZCZC", "synthetic") == null, "NAVTEX: brak nagłówka");
var dsc = MaritimeDecoders.ParseDsc("$CDDSC,12,3380400790,12,05,00,1423108312,2019,,,S,E*69", "synthetic");
Check(dsc != null && dsc.Status == "NMEA CHECKSUM OK", "DSC: suma NMEA");
Check(dsc != null && dsc.Fields["MMSI (normalizowany gdy 10 cyfr i zero końcowe)"] == "338040079", "DSC: MMSI");
var wrongDsc = MaritimeDecoders.ParseDsc("$CDDSC,12,3380400790,12,05,00,1423108312,2019,,,S,E*68", "synthetic");
Check(wrongDsc != null && wrongDsc.Status == "NMEA CHECKSUM BŁĘDNE", "DSC: błędna suma NMEA");
Check(MaritimeDecoders.ParseDsc("$GPGGA,stuff*00", "synthetic") == null, "DSC: inny protokół NMEA");
var sat = MaritimeDecoders.ParseSatvoice("SATVOICE,xyz,CALL_SETUP,1550 MHz", "synthetic");
Check(sat != null && sat.Protocol == "SATVOICE", "SATVOICE: metadane");
Check(MaritimeDecoders.ParseSatvoice("SATVOICE-MOCK", "synthetic") == null, "SATVOICE: brak fałszywego zdarzenia");
var router = InputRouter.ParseText("ACARS_HEX: " + Convert.ToHexString(frame));
Check(router.Count >= 1 && router[0].Status == "CRC OK", "Router: ACARS_HEX");
string bits = new string(frame.SelectMany(b => Convert.ToString(b, 2).PadLeft(8, '0').Reverse()).ToArray());
Check(AcarsBitsDecoder.Decode(bits).Any(e => e.Status == "CRC OK"), "Router: ACARS_BITS (już zdemodulowane)");
Check(InputRouter.ParseText("ACARS_BITS: " + bits).Any(e => e.Status == "CRC OK"), "Router: ACARS_BITS prefix");
Check(InputRouter.ParseText("[1,2,3]\n{\"text\":\"TEST\"}").Count == 1, "Router: ignorowanie nierozpoznanej tablicy JSON");
Check(InputRouter.ParseText("{\"text\":\"TEST\"}").Count == 1, "Router: obsługa obiektu JSON");
Check(InputRouter.ParseText("{\"text\":").Count == 0, "Router: ignorowanie niekompletnej linii JSON");
Check(InputRouter.ParseText("{\"payload\":\"TEST\",\"items\":[1,2]}").Count == 1, "Router: JSON z tablicą w innych polach");
var mixed = InputRouter.ParseText("ZCZC DA01\nNAVTEX TEST\nNNNN\nACARS_HEX: " + Convert.ToHexString(frame));
Check(mixed.Count == 3 &&
mixed.Count(e => e.Protocol.Contains("NAVTEX")) == 1 &&
mixed.Count(e => e.Protocol == "ACARS" && e.Status == "CRC OK") == 1 &&
mixed.Count(e => e.Protocol == "ADS-C") == 1,
"Router: NAVTEX + ACARS + ADS-C jako trzy osobne zdarzenia");
var navtexPair = InputRouter.ParseText("ZCZC DA01\nFIRST\nNNNN\nZCZC DB02\nSECOND\nNNNN");
Check(navtexPair.Count == 2 && navtexPair.All(e => e.Protocol.Contains("NAVTEX")),
"Router: dwa kolejne wieloliniowe komunikaty NAVTEX");
var mixedAfterNavtex = InputRouter.ParseText("ZCZC DA01\nBODY\nNNNN\nSATVOICE,call-2,CALL_SETUP,1545MHz\nEGC:1:31:01A0:11:00");
Check(mixedAfterNavtex.Count == 3 &&
mixedAfterNavtex.Count(e => e.Protocol.Contains("NAVTEX")) == 1 &&
mixedAfterNavtex.Count(e => e.Protocol == "SATVOICE") == 1 &&
mixedAfterNavtex.Count(e => e.Protocol.Contains("SafetyNET")) == 1,
"Router: NAVTEX nie blokuje kolejnych SATVOICE i EGC");
var demo = InputRouter.ParseText("EGC:1:31:01A0:11:00 SAFETYNET\nSATVOICE,abc,CALL_SETUP,1545MHz");
Check(demo.Count == 2, "Router: wieloprotokołowy import");
MustThrow<FormatException>(() => AcarsBitsDecoder.Decode("001010"), "Bitstream: zbyt krótkie dane");
MustThrow<FormatException>(() => InputRouter.ParseText(new string('A', 16 * 1024 * 1024 + 1)), "Router: limit 16 MiB");
var now = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
var h1 = new DecodeEvent { TimestampUtc = now.AddHours(-1), Protocol = "ACARS", Status = "CRC OK",
Summary = "SP-ABC", Source = "test-1", Payload = "Hello", Fields = new() { ["lot"] = "LO1234" } };
var h2 = new DecodeEvent { TimestampUtc = now.AddDays(-10), Protocol = "GMDSS / NAVTEX", Status = "NAGŁÓWEK ROZPOZNANY",
Summary = "Ostrzeżenie", Payload = "ZCZC DA01" };
var h3 = new DecodeEvent { TimestampUtc = now.AddHours(-2), Protocol = "ACARS", Status = "CRC BŁĘDNE",
Summary = "Niepoprawna ramka" };
Check(EventQuery.Matches(h1, "ACARS", "sp-abc", 0, 0, now), "Historia: szukanie opisu");
Check(EventQuery.Matches(h1, "WSZYSTKIE", "lo1234", 0, 0, now), "Historia: szukanie pól wiadomości");
Check(!EventQuery.Matches(h1, "ADS-C", "", 0, 0, now), "Historia: filtr protokołu");
Check(EventQuery.Matches(h1, "WSZYSTKIE", "", 1, 1, now), "Historia: CRC OK i 24 godziny");
Check(EventQuery.Matches(h3, "ACARS", "", 2, 1, now), "Historia: filtr błędów");
Check(!EventQuery.Matches(h3, "ACARS", "", 1, 0, now), "Historia: zły CRC nie jest dobrym CRC");
Check(EventQuery.Matches(h2, "GMDSS", "", 3, 0, now), "Historia: status bez weryfikacji CRC");
Check(!EventQuery.Matches(h2, "GMDSS", "", 0, 2, now), "Historia: starszy niż tydzień");
Check(!EventQuery.Matches(h1, "ACARS", "", 0, 1, now.AddDays(-2)), "Historia: przyszłe zdarzenie ignorowane tylko w filtrowaniu okresu") ;
Check(HistoryStore.Escape("=cmd").StartsWith("\"'="), "CSV: zabezpieczenie przed formułą");
Check(HistoryStore.Escape("a;\"b").Contains("\"\""), "CSV: cudzysłowy");
Check(HistoryStore.Escape("  =cmd").StartsWith("\"'  ="), "CSV: formuła z odstępami");
string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "heterodyna_protocol_history_" + System.Guid.NewGuid().ToString("N") + ".jsonl");
try
{
HistoryStore.Save(temp, new[] { h1, h2, h3 });
var roundtrip = HistoryStore.Load(temp);
Check(roundtrip.Count == 3, "Historia: zapis/odczyt trzech zdarzeń");
Check(roundtrip[0].Fields["lot"] == "LO1234", "Historia: zachowanie pól wiadomości");
Check(roundtrip[0].TimestampUtc == h1.TimestampUtc, "Historia: zachowanie czasu UTC");
System.IO.File.WriteAllText(temp, "{brak-json}");
MustThrow<FormatException>(() => HistoryStore.Load(temp), "Historia: odmowa odczytu niepoprawnego JSONL");
}
finally { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
byte[] wireOk = System.Text.Encoding.UTF8.GetBytes("{\"v\":1,\"type\":\"text\",\"text\":\"ZCZC DA01 TEST NNNN\"}");
Check(LiveWireProtocol.TryParse(wireOk, out var wire, out _) && wire.Type == "text", "LIVE: ramka tekstowa JSONL v1");
Check(!LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes("{\"v\":2,\"type\":\"text\",\"text\":\"X\"}"), out _, out _), "LIVE: odmowa niezgodnej wersji");
Check(!LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes("{\"v\":1,\"type\":\"unknown\"}"), out _, out _), "LIVE: odmowa nieznanego typu");
Check(!LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes("[1,2]"), out _, out _), "LIVE: odmowa tablicy JSON");
Check(!LiveWireProtocol.TryParse(new byte[] { 0xff, 0xfe }, out _, out _), "LIVE: odmowa błędnego UTF-8");
Check(!LiveWireProtocol.TryParse(new byte[LiveWireProtocol.MaxLineBytes + 1], out _, out _), "LIVE: limit rozmiaru wiadomości");
Check(LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes("{\"v\":1,\"type\":\"event\",\"protocol\":\"ACARS\",\"summary\":\"test\",\"status\":\"CRC OK\"}"), out var claimed, out _) && claimed.ClaimedStatus == "CRC OK", "LIVE: deklaracja statusu nadawcy parsowana oddzielnie");
Check(!LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes("{\"v\":1,\"type\":\"event\",\"protocol\":true,\"summary\":\"test\"}"), out _, out _), "LIVE: walidacja typów pól");
using (var bridge = new LiveReceiver())
{
bridge.Start(0);
Check(bridge.IsRunning && bridge.Port > 0, "LIVE: nasłuch tylko localhost na porcie testowym");
using (var client = new System.Net.Sockets.TcpClient())
{
client.Connect(System.Net.IPAddress.Loopback, bridge.Port);
byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
"{\"v\":1,\"type\":\"hello\",\"sender\":\"HeterodynaDemo\"}\n" +
"{\"v\":1,\"type\":\"text\",\"text\":\"ZCZC DA01 TEST NNNN\"}\n" +
"{broken}\n" +
"{\"v\":1,\"type\":\"event\",\"protocol\":\"ACARS\",\"summary\":\"z zewnatrz\",\"status\":\"CRC OK\"}\n");
client.GetStream().Write(bytes, 0, bytes.Length);
Check(System.Threading.SpinWait.SpinUntil(() => bridge.ReceivedLines >= 4 && bridge.AcceptedMessages + bridge.RejectedMessages >= 4 && bridge.PendingEvents >= 2, 4000), "LIVE: odbiór czterech linii TCP");
Check(bridge.AcceptedMessages == 3 && bridge.RejectedMessages == 1, "LIVE: liczniki wiadomości OK / błędne");
Check(bridge.LastRejectedReason.Contains("JSON", StringComparison.OrdinalIgnoreCase),
"LIVE: diagnostyka podaje przyczynę odrzucenia, bez pełnej wiadomości");
var incoming = new List<DecodeEvent>();
while (bridge.TryDequeue(out var rec)) incoming.Add(rec);
Check(incoming.Count == 2 && incoming.Any(x => x.Protocol.Contains("NAVTEX")), "LIVE: transport danych do dekodera NAVTEX");
Check(incoming.Any(x => x.Protocol == "ACARS" && x.Status == "UPSTREAM / NIEZWERYFIKOWANE" &&
x.Fields["deklarowany_status_nadawcy"] == "CRC OK"), "LIVE: zewnętrzne CRC OK nie jest zaufane");
}
bridge.Stop();
Check(!bridge.IsRunning, "LIVE: zatrzymanie nasłuchu");
bridge.Start(0);
Check(string.IsNullOrEmpty(bridge.LastRejectedReason), "LIVE: nowa sesja usuwa stary komunikat walidacji");
bridge.Stop();
}
var tnow = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
Check(LiveDiagnostics.DetermineState(tnow, false, 0, null, "") == "WYŁĄCZONY", "DIAG: zatrzymany serwer");
Check(LiveDiagnostics.DetermineState(tnow, true, 0, null, "") == "OCZEKIWANIE NA KLIENTA", "DIAG: serwer bez klienta");
Check(LiveDiagnostics.DetermineState(tnow, true, 1, null, "") == "KLIENT POŁĄCZONY — BRAK PAKIETÓW", "DIAG: klient bez pakietu");
Check(LiveDiagnostics.DetermineState(tnow, true, 1, tnow.AddSeconds(-1), "") == "ODBIÓR JSONL", "DIAG: aktualne pakiety");
Check(LiveDiagnostics.DetermineState(tnow, true, 1, tnow.AddSeconds(-11), "") == "BRAK NOWYCH PAKIETÓW", "DIAG: pakiety nieaktywne");
Check(LiveDiagnostics.DetermineState(tnow, true, 1, tnow, "problem") == "BŁĄD", "DIAG: błąd ma priorytet");
using (var diag = new LiveReceiver())
{
var inactive = diag.GetDiagnostics();
Check(!inactive.Listening && inactive.State == "WYŁĄCZONY", "DIAG: snapshot wyłączonego odbiornika");
Check(inactive.ToJson().Contains("\"State\""), "DIAG: poprawny eksport JSON ze stanem");
diag.Start(0);
Check(diag.GetDiagnostics().Listening && diag.GetDiagnostics().Port > 0, "DIAG: lokalny adres i stan oczekiwania");
using (var first = new System.Net.Sockets.TcpClient())
{
first.Connect(System.Net.IPAddress.Loopback, diag.Port);
Check(System.Threading.SpinWait.SpinUntil(() => diag.ActiveClients == 1, 4000), "DIAG: klient pierwszego połączenia");
}
Check(System.Threading.SpinWait.SpinUntil(() => diag.TotalDisconnections >= 1 && diag.ActiveClients == 0, 4000),
"DIAG: wykrycie rozłączenia, serwer działa nadal");
Check(diag.LastPacketUtc == null && diag.LastRfTelemetry == null,
"DIAG: pusty serwer nie pokazuje starych pakietów ani PCM");
using (var second = new System.Net.Sockets.TcpClient())
{
second.Connect(System.Net.IPAddress.Loopback, diag.Port);
Check(System.Threading.SpinWait.SpinUntil(() => diag.TotalConnections >= 2 && diag.ActiveClients == 1, 4000),
"DIAG: przyjęcie ponownego połączenia lokalnego nadawcy");
}
diag.Stop();
}
using (var restart = new LiveReceiver())
{
restart.Start(0);
using (var first = new System.Net.Sockets.TcpClient())
{
first.Connect(System.Net.IPAddress.Loopback, restart.Port);
var hello = System.Text.Encoding.UTF8.GetBytes("{\"v\":1,\"type\":\"hello\",\"sender\":\"test\"}\n");
first.GetStream().Write(hello, 0, hello.Length);
Check(System.Threading.SpinWait.SpinUntil(() => restart.LastPacketUtc != null, 4000),
"LIVE: pakiet z pierwszej sesji otrzymany");
}
restart.Stop();
restart.Start(0);
Check(restart.LastPacketUtc == null && restart.LastRfTelemetry == null,
"LIVE: ponowny Start usuwa stare czasy pakietow i RF");
using (var next = new System.Net.Sockets.TcpClient())
{
next.Connect(System.Net.IPAddress.Loopback, restart.Port);
Check(System.Threading.SpinWait.SpinUntil(() => restart.TotalConnections >= 2 && restart.ActiveClients >= 1, 4000),
"LIVE: po restarcie przyjmuje nowe polaczenie");
Check(restart.GetDiagnostics().State == "KLIENT POŁĄCZONY — BRAK PAKIETÓW",
"LIVE: bez nowych ramek nie pokazuje falszywego odbioru");
Check(restart.ActiveClients == 1, "LIVE: licznik klientów nie dziedziczy starych zadań");
}
restart.Stop();
}
var meter = new Pcm16LevelMeter();
meter.Add(new byte[] { 0x00, 0x00, 0xff });
meter.Add(new byte[] { 0x7f });
var pcmSample = meter.TakeSnapshot();
Check(pcmSample.Samples == 2 && pcmSample.PeakDbfs > -0.1, "RF: fragmentacja próbek PCM między odczytami");
var afterReset = meter.TakeSnapshot();
Check(afterReset.Samples == 0 && afterReset.RmsDbfs == -120, "RF: zerowanie licznika PCM");
meter.Add(new byte[] { 0x00, 0x00, 0x00, 0x00 });
Check(meter.TakeSnapshot().RmsDbfs == -120, "RF: cisza PCM -120 dBFS");
byte[] rfWire = System.Text.Encoding.UTF8.GetBytes(
"{\"v\":1,\"type\":\"rf_metrics\",\"frequency_hz\":131550000,\"sample_rate\":24000,\"rms_dbfs\":-18.2,\"peak_dbfs\":-8.1,\"samples\":24000,\"source\":\"rtl_fm\"}");
Check(LiveWireProtocol.TryParse(rfWire, out var rfPacket, out _) &&
rfPacket.Telemetry.FrequencyHz == 131550000 && rfPacket.Telemetry.RmsDbfs == -18.2,
"RF: walidacja metryk JSONL v1");
Check(!LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes(
"{\"v\":1,\"type\":\"rf_metrics\",\"frequency_hz\":131550000,\"sample_rate\":24000,\"rms_dbfs\":1,\"peak_dbfs\":-8,\"samples\":24000,\"source\":\"rtl_fm\"}"), out _, out _),
"RF: odrzucanie niefizycznego poziomu PCM");
Check(!LiveWireProtocol.TryParse(System.Text.Encoding.UTF8.GetBytes(
"{\"v\":1,\"type\":\"rf_metrics\",\"frequency_hz\":131550000,\"sample_rate\":24000,\"rms_dbfs\":-18,\"peak_dbfs\":-8,\"samples\":24000,\"source\":\"nieznane\"}"), out _, out _),
"RF: odrzucanie nieznanego źródła metryk");
using (var rfBridge = new LiveReceiver())
{
rfBridge.Start(0);
using (var tcpRf = new System.Net.Sockets.TcpClient())
{
tcpRf.Connect(System.Net.IPAddress.Loopback, rfBridge.Port);
tcpRf.GetStream().Write(rfWire, 0, rfWire.Length);
tcpRf.GetStream().WriteByte((byte)'\n');
Check(System.Threading.SpinWait.SpinUntil(
() => rfBridge.LastRfTelemetry != null && rfBridge.AcceptedMessages == 1, 4000),
"RF: telemetria dostarczona przez TCP loopback");
Check(rfBridge.PendingEvents == 0 && rfBridge.LastRfTelemetry!.Samples == 24000,
"RF: metryki nie tworzą fałszywych rekordów dekodera");
}
rfBridge.Stop();
Check(rfBridge.LastRfTelemetry == null, "RF: wyczyszczenie telemetrii po Stop");
}
return passed;
}
}
