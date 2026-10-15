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
    private void ShowLiveDiagnostics()
    {
        var snap=liveReceiver.GetDiagnostics();
        string report=$"HETERODYNA SDR — PROTOCOL LAB 0.3.3\r\n"+$"Diagnostyka lokalnego JSONL TCP (NIE dekoder IQ/RF)\r\n"+$"Stan: {snap.State}\r\n"+$"Czas UTC: {snap.SampledUtc:O}\r\n"+$"Adres: 127.0.0.1:{snap.Port}\r\n"+$"Połączonych klientów: {snap.ConnectedClients}\r\n"+$"Połączenia / rozłączenia: {snap.Connections} / {snap.Disconnections}\r\n"+$"Linie / przyjęte / odrzucone: {snap.Lines} / {snap.Accepted} / {snap.Rejected}\r\n"+$"Przekroczony limit / utracone zdarzenia: {snap.Oversized} / {snap.Dropped}\r\n"+$"Kolejka zdarzeń: {snap.Pending}\r\n"+$"Ostatni poprawny pakiet: {snap.LastPacketUtc?.ToString("O")??"BRAK"}\r\n"+$"Ostatnie PCM: {snap.LastRfUtc?.ToString("O")??"BRAK"} ({snap.LastRfSource})\r\n"+$"Wiek ostatniej telemetrii PCM: {(snap.LastRfUtc is null?"BRAK":Math.Max(0,(snap.SampledUtc-snap.LastRfUtc.Value).TotalSeconds).ToString("F1")+" s")}\r\n"+$"Błąd gniazda: {snap.LastError}\r\n"+$"Ostatnie odrzucenie JSONL: {(string.IsNullOrWhiteSpace(snap.LastRejectedReason)?"BRAK":snap.LastRejectedReason)}\r\n"+"\r\nUWAGA: nasłuch LIVE przyjmuje zdarzenia i metryki; nie dekoduje sygnału radiowego.\r\n"+"Po zerwaniu połączenia nadawca musi ponownie połączyć się samodzielnie.\r\n";
        using var dialog=new Form{Text="Heterodyna — diagnostyka LIVE TCP",Width=770,Height=585,MinimumSize=new Size(610,440),StartPosition=FormStartPosition.CenterParent,BackColor=Base,ForeColor=TextPrimary,Font=Font};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(12)};
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        var content=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=report,Dock=DockStyle.Fill,BackColor=Surface,ForeColor=TextPrimary,Font=new Font("Consolas",10F)};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
        var close=MakeButton("Zamknij",(_,_)=>dialog.Close(),false);
        var copy=MakeButton("Kopiuj raport",(_,_)=>CopyToClipboard(report),false);
        var save=MakeButton("Zapisz JSON",(_,_)=>{using var picker=new SaveFileDialog{Filter="JSON (*.json)|*.json",DefaultExt="json",FileName="heterodyna-live-diagnostics.json"};if(picker.ShowDialog(dialog)!=DialogResult.OK)return;try{File.WriteAllText(picker.FileName,snap.ToJson(),new UTF8Encoding(false));status.Text="Zapisano diagnostykę JSON: "+picker.FileName;}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){MessageBox.Show(dialog,ex.Message,"Błąd zapisu",MessageBoxButtons.OK,MessageBoxIcon.Warning);}},true);
        buttons.Controls.Add(close);buttons.Controls.Add(copy);buttons.Controls.Add(save);layout.Controls.Add(content,0,0);layout.Controls.Add(buttons,0,1);dialog.Controls.Add(layout);dialog.ShowDialog(this);
    }
    private bool ShouldShowLiveStatus()=>liveReceiver.ReceivedLines>0||liveReceiver.ActiveClients>0||liveReceiver.IsRunning||!string.IsNullOrEmpty(liveFailure);
    private void SelectWatch()
    {
        using var dlg=new FolderBrowserDialog{Description="Folder z tekstowymi logami dekoderów"};if(dlg.ShowDialog(this)!=DialogResult.OK)return;
        StopWatcher();importedFiles.Clear();pendingFiles.Clear();
        watcher=new FileSystemWatcher(dlg.SelectedPath){Filter="*.*",NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size,IncludeSubdirectories=false,InternalBufferSize=32*1024};
        watcher.Created+=WatcherChanged;watcher.Changed+=WatcherChanged;watcher.Renamed+=WatcherRenamed;watcher.Error+=WatcherError;watcher.EnableRaisingEvents=true;
        status.Text="Obserwowany folder: "+dlg.SelectedPath+" (dopisywane pliki zostaną ponownie sprawdzone)";
    }
    private void WatcherError(object sender,ErrorEventArgs e)=>Ui(()=>status.Text="Obserwator folderu zgłosił błąd; uruchom obserwację ponownie: "+e.GetException().Message);
    private void WatcherRenamed(object sender,RenamedEventArgs e)=>QueueWatchFile(e.FullPath);
    private void WatcherChanged(object sender,FileSystemEventArgs e)=>QueueWatchFile(e.FullPath);
    private void QueueWatchFile(string path)
    {
        if(closing||!new[]{".txt",".log",".jsonl",".json",".hex"}.Contains(Path.GetExtension(path).ToLowerInvariant()))return;
        if(!pendingFiles.TryAdd(path,0))return;int generation=watcherGeneration;
        _=Task.Run(async()=>{try{for(int attempt=0;attempt<12&&!closing&&generation==watcherGeneration;attempt++){await Task.Delay(300);try{var before=new FileInfo(path);if(!before.Exists)return;long length=before.Length,ticks=before.LastWriteTimeUtc.Ticks;if(length==0||length>16*1024*1024)return;await Task.Delay(300);before.Refresh();if(!before.Exists||before.Length!=length||before.LastWriteTimeUtc.Ticks!=ticks)continue;bool previouslyLoaded=importedFiles.TryGetValue(path,out var last);if(previouslyLoaded&&last.Length==length&&last.WriteTicks==ticks)return;var decoded=InputRouter.ParseFile(path);before.Refresh();if(!before.Exists||before.Length!=length||before.LastWriteTimeUtc.Ticks!=ticks)continue;int totalEvents=decoded.Count;if(previouslyLoaded&&length>last.Length&&last.EventCount<=decoded.Count)decoded=decoded.Skip(last.EventCount).ToList();int safeCount=decoded.Any(x=>x.Status=="INVALID"||x.Status.Contains("BŁĘDNE"))?0:totalEvents;importedFiles[path]=(length,ticks,safeCount);if(generation!=watcherGeneration)return;Ui(()=>{if(generation==watcherGeneration&&decoded.Count>0)AddEvents(decoded);});return;}catch(IOException){}catch(UnauthorizedAccessException){}catch(Exception ex){Ui(()=>status.Text="Nie udało się przetworzyć "+Path.GetFileName(path)+": "+ex.Message);return;}}Ui(()=>status.Text="Plik nadal zajęty, zaimportuj go ręcznie: "+Path.GetFileName(path));}finally{pendingFiles.TryRemove(path,out _);}});
    }
    private void Ui(Action action){if(closing||!IsHandleCreated||IsDisposed)return;try{BeginInvoke(action);}catch(ObjectDisposedException){}catch(InvalidOperationException){}}
    private void StopWatcher(){watcherGeneration++;if(watcher==null)return;watcher.EnableRaisingEvents=false;watcher.Created-=WatcherChanged;watcher.Changed-=WatcherChanged;watcher.Renamed-=WatcherRenamed;watcher.Error-=WatcherError;watcher.Dispose();watcher=null;}
    private void StopWatch(){StopWatcher();status.Text="Obserwacja folderu zatrzymana.";}
    private void Clear(){if(entries.Count>0&&MessageBox.Show(this,"Wyczyścić wszystkie zdarzenia z pamięci? Zapisana historia nie zostanie usunięta.","Wyczyść rekordy",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;entries.Clear();table.Rows.Clear();details.Text="Wybierz rekord z listy, aby zobaczyć szczegóły.";UpdateCounter();status.Text="Wyczyszczono listę zdarzeń.";}
}
