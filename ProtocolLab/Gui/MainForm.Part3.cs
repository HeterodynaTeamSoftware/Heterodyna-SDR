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
    private void HandleShortcuts(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.F) { searchBox.Focus(); searchBox.SelectAll(); }
        else if (e.Control && e.KeyCode == Keys.O) _ = ImportFileAsync();
        else if (e.Control && e.KeyCode == Keys.S) SaveHistory();
        else if (e.Control && e.KeyCode == Keys.E) ExportCsv();
        else if (e.Control && e.KeyCode == Keys.J) ExportJsonl();
        else if (e.Control && e.KeyCode == Keys.D) ShowLiveDiagnostics();
        else if (e.Control && e.KeyCode == Keys.R) ResetQuery();
        else if (e.Control && e.KeyCode == Keys.C && table.ContainsFocus) CopySelectedEvent();
        else if (e.KeyCode == Keys.Escape && searchBox.Focused) searchBox.Clear();
        else return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }
    private DecodeEvent SelectedEvent() => table.SelectedRows.Count > 0 ? table.SelectedRows[0].Tag as DecodeEvent : null;
    private void CopySelectedEvent(){ if (SelectedEvent() is null) return; CopyToClipboard(details.Text); }
    private void CopySelectedPayload(){ if (SelectedEvent() is not DecodeEvent ev) return; CopyToClipboard(ev.Payload ?? string.Empty); }
    private void CopyToClipboard(string value)
    {
        if (string.IsNullOrEmpty(value)) { status.Text = "Wybrany rekord nie ma treści do skopiowania."; return; }
        try { Clipboard.SetText(value); status.Text = "Skopiowano do schowka."; }
        catch (ExternalException) { status.Text = "Schowek zajęty przez inny program; spróbuj ponownie."; }
    }
    private void SaveUiPreferences()
    {
        if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return;
        var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var prefs = new UiPreferences {
            X=b.X,Y=b.Y,Width=b.Width,Height=b.Height,Maximized=WindowState==FormWindowState.Maximized,
            UpperRatio=verticalSplit.Height>0?(double)verticalSplit.SplitterDistance/verticalSplit.Height:0.64,
            LeftRatio=horizontalSplit.Width>0?(double)horizontalSplit.SplitterDistance/horizontalSplit.Width:0.63,
            Filter=activeFilter,IntegrityIndex=integrityBox.SelectedIndex,PeriodIndex=periodBox.SelectedIndex };
        UiPreferencesStore.Save(prefs);
    }
    private void RestoreUiPreferences()
    {
        var prefs=UiPreferencesStore.Load(); if(prefs is null)return;
        var wanted=new Rectangle(prefs.X,prefs.Y,prefs.Width,prefs.Height);
        if(UiPreferencesStore.IsVisibleOnScreen(wanted)){StartPosition=FormStartPosition.Manual;Bounds=wanted;}
        if(double.IsFinite(prefs.UpperRatio)&&prefs.UpperRatio is >=0.20 and <=0.80) SetSplitterSafely(verticalSplit,(int)(verticalSplit.Height*prefs.UpperRatio));
        if(double.IsFinite(prefs.LeftRatio)&&prefs.LeftRatio is >=0.25 and <=0.78) SetSplitterSafely(horizontalSplit,(int)(horizontalSplit.Width*prefs.LeftRatio));
        if(!string.IsNullOrWhiteSpace(prefs.Filter)&&filterButtons.ContainsKey(prefs.Filter))SetFilter(prefs.Filter);
        if(prefs.IntegrityIndex>=0&&prefs.IntegrityIndex<integrityBox.Items.Count)integrityBox.SelectedIndex=prefs.IntegrityIndex;
        if(prefs.PeriodIndex>=0&&prefs.PeriodIndex<periodBox.Items.Count)periodBox.SelectedIndex=prefs.PeriodIndex;
        if(prefs.Maximized)WindowState=FormWindowState.Maximized;
    }
    private ContextMenuStrip BuildToolsMenu()
    {
        var menu=new ContextMenuStrip{BackColor=Raised,ForeColor=TextPrimary};
        var sources=new ToolStripMenuItem("Źródła i odbiór");
        sources.DropDownItems.Add("Obserwuj folder",null,(_,_)=>SelectWatch());
        sources.DropDownItems.Add("Zatrzymaj obserwację",null,(_,_)=>StopWatch());
        sources.DropDownItems.Add("Adapter RTL-SDR (PCM)",null,(_,_)=>StartRtlAdapter()); menu.Items.Add(sources);
        var history=new ToolStripMenuItem("Historia"); history.DropDownItems.Add("Otwórz historię...",null,(_,_)=>OpenHistory()); history.DropDownItems.Add("Zapisz historię...",null,(_,_)=>SaveHistory()); menu.Items.Add(history);
        var export=new ToolStripMenuItem("Eksport"); export.DropDownItems.Add("Eksport JSONL...",null,(_,_)=>ExportJsonl()); export.DropDownItems.Add("Eksport CSV...",null,(_,_)=>ExportCsv()); menu.Items.Add(export);
        var view=new ToolStripMenuItem("Widok i filtry"); view.DropDownItems.Add("Resetuj filtry",null,(_,_)=>ResetQuery()); view.DropDownItems.Add("Resetuj układ paneli",null,(_,_)=>ResetSplitters()); menu.Items.Add(view);
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Wyczyść rekordy...",null,(_,_)=>Clear()); return menu;
    }
    private static FlowLayoutPanel MakeToolbarRow()=>new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,AutoScroll=true,BackColor=Base,Margin=Padding.Empty,Padding=Padding.Empty};
    private static TableLayoutPanel MakePanelLayout(string name,string title)
    {
        var frame=new TableLayoutPanel{Name=name,Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Surface,Margin=Padding.Empty,Padding=Padding.Empty};
        frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); frame.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var heading=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=1,ColumnCount=1,BackColor=Raised,Margin=Padding.Empty};
        heading.RowStyles.Add(new RowStyle(SizeType.Percent,100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        heading.Controls.Add(new Label{Dock=DockStyle.Fill,Text="   "+title,BackColor=Raised,ForeColor=TextPrimary,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9F,FontStyle.Bold)},0,0);
        frame.Controls.Add(heading,0,0); return frame;
    }
    private void ResetSplitters(){if(verticalSplit is null||horizontalSplit is null)return;if(verticalSplit.Height>0)SetSplitterSafely(verticalSplit,(int)(verticalSplit.Height*0.64));if(horizontalSplit.Width>0)SetSplitterSafely(horizontalSplit,(int)(horizontalSplit.Width*0.63));}
    public IReadOnlyList<string> AuditLayout()
    {
        var failures=new List<string>();
        if(navigationLayout is null||recordsLayout is null||detailsLayout is null||editorLayout is null){failures.Add("Nie utworzono wszystkich sekcji układu.");return failures;}
        if(filterButtons.Count!=6||navigationLayout.Controls.Count!=6)failures.Add("Niewłaściwa liczba filtrów bocznych.");
        if(!KeyPreview||table.ContextMenuStrip?.Items.Count!=2)failures.Add("Brak skrótów klawiszowych lub menu kopiowania rekordów.");
        if(liveMetrics is null||liveMetrics.Height<12||modeHint is null||modeHint.Height<15||liveStartButton is null||liveStopButton is null||toolsMenuButton is null||toolsMenu is null||toolsMenu.Items.Count<5||toolsMenu.Items.OfType<ToolStripMenuItem>().Count()<5)failures.Add("Brak panelu stanu lub przycisków i menu LIVE TCP.");
        foreach(var filter in filterButtons){var b=filter.Value;if(b.Width<60||b.Height<28||b.Height>55||b.Right>navigationLayout.ClientSize.Width+1||b.Bottom>navigationLayout.ClientSize.Height+1)failures.Add("Nieprawidłowy rozmiar filtru: "+filter.Key+$" ({b.Width} x {b.Height})");}
        if(toolsMenu is not null){var groups=toolsMenu.Items.OfType<ToolStripMenuItem>().ToList();if(groups.Count<5||!groups.Any(g=>g.Text=="Źródła i odbiór"&&g.DropDownItems.Count>=3)||!groups.Any(g=>g.Text=="Eksport"&&g.DropDownItems.Count>=2))failures.Add("Menu dodatkowych czynności jest niekompletne.");}
        if(navigationLayout.RowCount!=7||navigationLayout.RowStyles.Count!=7||navigationLayout.RowStyles[6].SizeType!=SizeType.Percent)failures.Add("Brak elastycznego wiersza po przyciskach bocznych.");
        if(brandLabel is null||TextRenderer.MeasureText(brandLabel.Text,brandLabel.Font).Width>brandLabel.Width+4)failures.Add("Ucięta nazwa HETERODYNA w panelu bocznym.");
        if(recordsLayout.GetControlFromPosition(0,1)is null||detailsLayout.GetControlFromPosition(0,1)!=details||editorLayout.GetControlFromPosition(0,2)!=input)failures.Add("Edytor lub szczegóły nie są w osobnym wierszu siatki.");
        if(input.Height<=15||details.Height<=15||table.Height<=15)failures.Add("Zbyt mała wysokość panelu edytora, detali lub listy.");
        if(searchBox.Width<=30||integrityBox.Width<=30||periodBox.Width<=30)failures.Add("Pole wyszukiwania lub filtry są ucięte.");
        if(verticalSplit.Panel1.Height<=0||verticalSplit.Panel2.Height<=0||horizontalSplit.Panel1.Width<=0||horizontalSplit.Panel2.Width<=0)failures.Add("Znikający panel SplitContainer."); return failures;
    }
    private static void SetSplitterSafely(SplitContainer split,int desired){int available=(split.Orientation==Orientation.Horizontal?split.Height:split.Width)-split.SplitterWidth;int max=available-split.Panel2MinSize;if(max>=split.Panel1MinSize)split.SplitterDistance=Math.Clamp(desired,split.Panel1MinSize,max);}
    private static void ConfigureEditor(RichTextBox editor,bool readOnly){editor.ReadOnly=readOnly;editor.WordWrap=true;editor.BackColor=Surface;editor.ForeColor=TextPrimary;editor.BorderStyle=BorderStyle.None;editor.Font=new Font("Consolas",10.5F);editor.DetectUrls=false;}
    private static Button MakeButton(string title,EventHandler click,bool primary){var button=new Button{Text=title,FlatStyle=FlatStyle.Flat,AutoSize=false,Height=38,Width=Math.Max(84,28+TextRenderer.MeasureText(title,new Font("Segoe UI",9F)).Width),BackColor=primary?Color.FromArgb(12,94,141):Raised,ForeColor=TextPrimary,Font=new Font("Segoe UI",9F,FontStyle.Bold),Margin=new Padding(0,2,9,3),Cursor=Cursors.Hand};button.FlatAppearance.BorderColor=primary?Accent:Border;button.Click+=click;return button;}
    private static Label QueryLabel(string text)=>new(){Dock=DockStyle.Fill,Text=text,ForeColor=Muted,Font=new Font("Segoe UI",9F,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft};
    private static void SetupCombo(ComboBox combo,string[] items){combo.Dock=DockStyle.Fill;combo.DropDownStyle=ComboBoxStyle.DropDownList;combo.FlatStyle=FlatStyle.Flat;combo.BackColor=Raised;combo.ForeColor=TextPrimary;combo.Font=new Font("Segoe UI",9F);combo.Margin=new Padding(0,2,9,2);combo.Items.AddRange(items);combo.SelectedIndex=0;}
    private void ResetQuery(){searchBox.Clear();integrityBox.SelectedIndex=0;periodBox.SelectedIndex=0;SetFilter("WSZYSTKIE");}
    private void SetFilter(string filter){activeFilter=filter;UpdateFilterButtons();RebuildTable();}
    private void UpdateFilterButtons(){foreach(var pair in filterButtons){bool on=pair.Key==activeFilter;pair.Value.BackColor=on?Color.FromArgb(16,81,115):Raised;pair.Value.ForeColor=on?Accent:TextPrimary;}}
    private bool PassFilter(DecodeEvent ev)=>EventQuery.Matches(ev,activeFilter,searchBox.Text,integrityBox.SelectedIndex>=0?integrityBox.SelectedIndex:0,periodBox.SelectedIndex>=0?periodBox.SelectedIndex:0,DateTimeOffset.UtcNow);
    private void AddEvents(IEnumerable<DecodeEvent> newEvents){var list=newEvents?.ToList()??new List<DecodeEvent>();if(list.Count==0){status.Text="Brak rozpoznanych rekordów — sprawdź format wejścia.";return;}entries.AddRange(list);if(entries.Count>EventLimit)entries.RemoveRange(0,entries.Count-EventLimit);RebuildTable();status.Text=$"Wczytano {list.Count} zdarzeń • historia maks. {EventLimit:N0} • brak dekodowania RF";}
}
