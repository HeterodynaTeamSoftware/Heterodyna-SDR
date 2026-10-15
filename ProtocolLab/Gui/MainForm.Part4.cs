using Heterodyna.ProtocolLab.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Heterodyna.ProtocolLab.Gui;

public sealed partial class MainForm : Form
{
    private void RebuildTable()
    {
        DecodeEvent selected = table.SelectedRows.Count > 0 ? table.SelectedRows[0].Tag as DecodeEvent : null;
        table.SuspendLayout();
        try
        {
            table.Rows.Clear();
            foreach (var ev in entries.Where(PassFilter).OrderByDescending(e => e.TimestampUtc))
            {
                int i = table.Rows.Add(ev.TimestampUtc.UtcDateTime.ToString("HH:mm:ss"), ev.Protocol, ev.Status, ev.Summary);
                table.Rows[i].Tag = ev;
                table.Rows[i].Cells[2].ToolTipText = ev.Status;
                table.Rows[i].Cells[3].ToolTipText = ev.Summary;
            }
            if (table.Rows.Count > 0)
            {
                table.ClearSelection();
                int index = -1;
                if (selected is not null)
                    for (int i = 0; i < table.Rows.Count; i++)
                        if (ReferenceEquals(table.Rows[i].Tag, selected)) { index = i; break; }
                if (index < 0) index = 0;
                table.Rows[index].Selected = true;
                table.CurrentCell = table.Rows[index].Cells[0];
                table.FirstDisplayedScrollingRowIndex = index;
            }
            else details.Text = "Brak zdarzeń zgodnych z filtrem.";
            UpdateCounter();
        }
        finally { table.ResumeLayout(); }
        ShowSelection();
    }
    private void UpdateCounter()
    {
        eventCounter.Text = $"{table.Rows.Count} / {entries.Count}   ";
        emptyHint.Visible = table.Rows.Count == 0;
        if (emptyHint.Visible) emptyHint.BringToFront();
    }
    private void ShowSelection()
    {
        if (table.SelectedRows.Count == 0 || table.SelectedRows[0].Tag is not DecodeEvent ev)
        { details.Text = "Wybierz rekord z listy, aby zobaczyć szczegóły."; return; }
        var sb = new StringBuilder();
        sb.AppendLine(ev.Protocol + "  /  " + ev.Kind).AppendLine("STATUS: " + ev.Status)
            .AppendLine("ŹRÓDŁO: " + ev.Source).AppendLine("CZAS UTC: " + ev.TimestampUtc.UtcDateTime.ToString("u"))
            .AppendLine("────────────────────────────");
        foreach (var f in ev.Fields) sb.AppendLine(f.Key + ": " + f.Value);
        sb.AppendLine("────────────────────────────").AppendLine(ev.Payload);
        details.Text = sb.ToString();
    }
    private async Task ImportFileAsync()
    {
        using var dlg = new OpenFileDialog { Title = "Import danych / logów", Filter = "Dane i logi|*.txt;*.log;*.jsonl;*.json;*.hex;*.bin;*.raw|Wszystkie pliki|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string path = dlg.FileName; status.Text = "Importuję " + Path.GetFileName(path) + "…";
        try { var parsed = await Task.Run(() => InputRouter.ParseFile(path)); if (!closing && !IsDisposed) AddEvents(parsed); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Błąd importu", MessageBoxButtons.OK, MessageBoxIcon.Warning); status.Text = "Import nieudany."; }
    }
    private void ParseEditor(){try{AddEvents(InputRouter.ParseText(input.Text,"edytor"));}catch(Exception ex){MessageBox.Show(this,ex.Message,"Błąd analizy",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    private void LoadDemo()
    {
        string demo = "ACARS_HEX: " + Convert.ToHexString(TestFrames.Acars()) + "\n" +
            "EGC:1:31:01A0:11:00 SAFETYNET NAVAREA 01 WEATHER WARNING\n" +
            "ZCZC DA01 NAVTEX TEST MESSAGE NNNN\n" +
            "SATVOICE,call-demo-01,CALL_SETUP,1545.12 MHz,DEMO (no voice decoding)";
        input.Text = demo;
        try { AddEvents(InputRouter.ParseText(demo, "dane syntetyczne")); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Błąd danych testowych"); }
    }
    private void ExportJsonl()
    {
        if(entries.Count==0){MessageBox.Show(this,"Brak danych do eksportu.");return;}
        using var dlg=new SaveFileDialog{Filter="JSON Lines (*.jsonl)|*.jsonl",FileName="heterodyna-protocol-events.jsonl",DefaultExt="jsonl"};
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        try{var filtered=entries.Where(PassFilter).ToList();HistoryStore.Save(dlg.FileName,filtered);status.Text=$"Eksport JSONL: {filtered.Count} widocznych rekordów.";}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Nie udało się zapisać JSONL",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void SaveHistory()
    {
        if(entries.Count==0){MessageBox.Show(this,"Historia jest pusta.");return;}
        using var dlg=new SaveFileDialog{Title="Zapisz pełną historię",Filter="Historia JSONL (*.jsonl)|*.jsonl",DefaultExt="jsonl",FileName="heterodyna-historia.jsonl"};
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        try{HistoryStore.Save(dlg.FileName,entries);status.Text=$"Zapisano {entries.Count} zdarzeń historii: "+dlg.FileName;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){MessageBox.Show(this,ex.Message,"Błąd zapisu historii",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void OpenHistory()
    {
        using var dlg=new OpenFileDialog{Title="Otwórz historię Protocol Lab",Filter="Historia JSONL (*.jsonl)|*.jsonl|Wszystkie pliki|*.*"};
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        try{var loaded=HistoryStore.Load(dlg.FileName);AddEvents(loaded);status.Text=$"Zaimportowano {loaded.Count} zapisanych zdarzeń historii.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException){MessageBox.Show(this,ex.Message,"Błąd odczytu historii",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void ExportCsv()
    {
        if(entries.Count==0){MessageBox.Show(this,"Brak danych do eksportu.");return;}
        using var dlg=new SaveFileDialog{Filter="CSV UTF-8 (*.csv)|*.csv",FileName="heterodyna-protocol-events.csv",DefaultExt="csv"};
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        try{var filtered=entries.Where(PassFilter).ToList();HistoryStore.SaveCsv(dlg.FileName,filtered);status.Text=$"Eksport CSV: {filtered.Count} widocznych rekordów.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){MessageBox.Show(this,ex.Message,"Błąd eksportu CSV",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void StartRtlAdapter()
    {
        if(!liveReceiver.IsRunning){StartLive();if(!liveReceiver.IsRunning)return;}
        string script=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","URUCHOM_RF_ADAPTER.cmd"));
        if(!File.Exists(script)){MessageBox.Show(this,"Brak URUCHOM_RF_ADAPTER.cmd — rozpakuj pełną paczkę ZIP.","RF Adapter",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        try{Process.Start(new ProcessStartInfo{FileName=script,WorkingDirectory=Path.GetDirectoryName(script),UseShellExecute=true});status.Text="Uruchomiono adapter RTL-SDR (domyślnie 131.550 MHz). Tunera nie można równolegle używać w drugim programie.";}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException){MessageBox.Show(this,ex.Message,"Błąd uruchamiania adaptera RF",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void StartLive()
    {
        if(liveReceiver.IsRunning){status.Text="Most TCP już nasłuchuje.";return;}
        try{liveReceiver.Start();liveFailure="";status.Text="Most LOCAL TCP aktywny: 127.0.0.1:17362 • odbiór logów i telemetrii PCM (bez dekodowania RF).";}
        catch(Exception ex) when(ex is System.Net.Sockets.SocketException or InvalidOperationException or ArgumentOutOfRangeException){liveFailure=ex.Message;status.Text="BŁĄD uruchomienia LIVE TCP: "+ex.Message;}
        UpdateLiveLabels();
    }
    private void StopLive(){liveReceiver.Stop();liveFailure="";status.Text="Most LIVE TCP zatrzymany. Zachowano już odebrane rekordy.";UpdateLiveLabels();}
    private void PollLive(){if(closing||IsDisposed)return;var batch=new List<DecodeEvent>();while(batch.Count<150&&liveReceiver.TryDequeue(out var ev))batch.Add(ev);if(batch.Count>0)AddEvents(batch);UpdateLiveLabels();}
    private void UpdateLiveLabels()
    {
        if(liveMetrics is null||receiverBadge is null)return;
        var snapshot=liveReceiver.GetDiagnostics();
        if(liveStartButton is not null)liveStartButton.Enabled=!snapshot.Listening;
        if(liveStopButton is not null)liveStopButton.Enabled=snapshot.Listening;
        if(modeHint is not null){modeHint.Text=snapshot.Listening?$"LIVE TCP — {snapshot.State}  •  127.0.0.1:{snapshot.Port}  •  klienci: {snapshot.ConnectedClients}  •  pakiety OK: {snapshot.Accepted}  •  odrzucone: {snapshot.Rejected}":"TRYB OFFLINE — Import i dane testowe nie oznaczają odbioru RTL-SDR na żywo.";modeHint.ForeColor=snapshot.State=="BŁĄD"?Color.FromArgb(255,139,139):Muted;}
        string error=!string.IsNullOrWhiteSpace(liveFailure)?liveFailure:snapshot.LastError;
        string state=!string.IsNullOrWhiteSpace(liveFailure)?"BŁĄD":snapshot.State;
        var rf=liveReceiver.LastRfTelemetry;
        bool freshRf=snapshot.Listening&&rf is not null&&snapshot.SampledUtc-rf.ReceivedUtc<TimeSpan.FromSeconds(5);
        receiverBadge.Text=freshRf?(rf.Source=="demo"?"●  PCM DEMO  |  0.3.3":"●  PCM METRYKI  |  0.3.3"):"●  RF OFFLINE  |  0.3.3";
        receiverBadge.ForeColor=freshRf?(rf.Source=="demo"?Color.FromArgb(255,204,127):Color.FromArgb(125,220,174)):Muted;
        liveMetrics.ForeColor=state=="BŁĄD"?Color.FromArgb(255,139,139):(state=="ODBIÓR JSONL"?Color.FromArgb(125,220,174):Muted);
        liveMetrics.Text=freshRf?$"{rf.FrequencyHz/1000000.0:F3} MHz • {rf.RmsDbfs:F1} dBFS":$"TCP: {state} • {snapshot.ConnectedClients} klient(ów)";
        if(ShouldShowLiveStatus())status.Text=$"LIVE {state} 127.0.0.1:{snapshot.Port} | Połączenia: {snapshot.Connections}/{snapshot.Disconnections} | Linie: {snapshot.Lines} | OK: {snapshot.Accepted} | Błędne: {snapshot.Rejected} | Za długie: {snapshot.Oversized} | Kolejka: {snapshot.Pending} | Utracone: {snapshot.Dropped}"+(freshRf?$" | PCM: {rf.FrequencyHz/1000000.0:F3} MHz / {rf.RmsDbfs:F1} dBFS / {rf.Samples} próbek ({rf.Source}, deklaracja nadawcy)":"")+(string.IsNullOrEmpty(error)?"":" | "+error);
    }
}
