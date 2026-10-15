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
private void BuildUi()
{
var root = new TableLayoutPanel
{
Name = "RootLayout", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
BackColor = Base, Margin = Padding.Empty, Padding = Padding.Empty
};
root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 246));
root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
Controls.Add(root);
var sidebarLayout = new TableLayoutPanel
{
Name = "SidebarLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
BackColor = Sidebar, Padding = new Padding(14, 15, 14, 12), Margin = Padding.Empty
};
sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
root.Controls.Add(sidebarLayout, 0, 0);
var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
BackColor = Sidebar, Margin = Padding.Empty };
brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
brand.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
brandLabel = new Label
{
Dock = DockStyle.Fill, Text = "◉ HETERODYNA", Font = new Font("Segoe UI", 12F, FontStyle.Bold),
ForeColor = Accent, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = false
};
brand.Controls.Add(brandLabel, 0, 0);
brand.Controls.Add(new Label
{
Dock = DockStyle.Fill, Text = "SDR  /  PROTOCOL LAB", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
ForeColor = Muted, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true
}, 0, 1);
sidebarLayout.Controls.Add(brand, 0, 0);
sidebarLayout.Controls.Add(new Label
{
Dock = DockStyle.Fill, Text = "DEKODERY / FILTRY", ForeColor = Muted,
Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft
}, 0, 1);
navigationLayout = new TableLayoutPanel
{
Name = "NavigationLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
BackColor = Sidebar, Margin = Padding.Empty, Padding = Padding.Empty, AutoScroll = false
};
navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
for (int i = 0; i < 6; i++) navigationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
sidebarLayout.Controls.Add(navigationLayout, 0, 2);
int filterRow = 0;
foreach (var item in new[] { ("WSZYSTKIE", "Wszystkie zdarzenia"), ("ACARS", "ACARS"),
("ADS-C", "ADS-C / ATS"), ("SATVOICE", "SATVOICE"), ("EGC", "STD-C / EGC\nSafetyNET"),
("GMDSS", "GMDSS / DSC\nNAVTEX") })
{
string key = item.Item1;
var button = MakeButton(item.Item2, (_, _) => SetFilter(key), false);
button.Dock = DockStyle.Fill;
button.TextAlign = ContentAlignment.MiddleLeft;
button.Margin = new Padding(0, 3, 0, 3);
button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
button.AccessibleName = "Filtr " + key;
filterButtons.Add(key, button);
navigationLayout.Controls.Add(button, 0, filterRow++);
}
sidebarLayout.Controls.Add(new Label
{
Dock = DockStyle.Fill, ForeColor = Muted, Font = new Font("Segoe UI", 8.3F),
Text = "LIVE TCP + RF PCM RTL-SDR\nBez demodulacji ACARS.\nSATVOICE audio: niedostępne.",
TextAlign = ContentAlignment.BottomLeft
}, 0, 3);
UpdateFilterButtons();
var content = new TableLayoutPanel
{
Name = "ContentLayout", Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1,
BackColor = Base, Padding = new Padding(18, 12, 16, 9), Margin = Padding.Empty
};
content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
content.RowStyles.Add(new RowStyle(SizeType.Absolute, 81));
content.RowStyles.Add(new RowStyle(SizeType.Absolute, 99));
content.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
root.Controls.Add(content, 1, 0);
var heading = new TableLayoutPanel
{
Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
BackColor = Base, Margin = Padding.Empty
};
heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
heading.Controls.Add(new Label
{
Dock = DockStyle.Fill, Text = "CENTRUM DEKODERÓW", Font = new Font("Segoe UI", 18F, FontStyle.Bold),
ForeColor = Accent, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
}, 0, 0);
heading.Controls.Add(new Label
{
Dock = DockStyle.Fill, Text = "ACARS · ADS-C / ARINC-622 · SATVOICE (metadane) · STD-C / SafetyNET · GMDSS / DSC / NAVTEX",
ForeColor = Muted, Font = new Font("Segoe UI", 8.7F), TextAlign = ContentAlignment.TopLeft,
AutoEllipsis = true
}, 0, 1);
receiverBadge = new Label
{
Dock = DockStyle.Fill, Text = "●  RF OFFLINE  |  0.3.3", ForeColor = Muted,
Font = new Font("Segoe UI", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight
};
heading.Controls.Add(receiverBadge, 1, 0);
liveMetrics = new Label
{
Dock = DockStyle.Fill, ForeColor = Muted, Font = new Font("Segoe UI", 8F),
TextAlign = ContentAlignment.TopRight, Text = "TCP OFFLINE  •  127.0.0.1:17362"
};
heading.Controls.Add(liveMetrics, 1, 1);
content.Controls.Add(heading, 0, 0);
var toolbar = new TableLayoutPanel
{
Name = "ToolbarLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
BackColor = Base, Margin = Padding.Empty, Padding = Padding.Empty
};
toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
var primaryActions = MakeToolbarRow();
primaryActions.Controls.Add(MakeButton("Importuj", async (_, _) => await ImportFileAsync(), true));
primaryActions.Controls.Add(MakeButton("Analizuj", (_, _) => ParseEditor(), true));
primaryActions.Controls.Add(MakeButton("Demo", (_, _) => LoadDemo(), false));
liveStartButton = MakeButton("Start LIVE", (_, _) => StartLive(), true);
liveStopButton = MakeButton("Stop LIVE", (_, _) => StopLive(), false);
primaryActions.Controls.Add(liveStartButton);
primaryActions.Controls.Add(liveStopButton);
primaryActions.Controls.Add(MakeButton("Diag. LIVE", (_, _) => ShowLiveDiagnostics(), false));
toolsMenu = BuildToolsMenu();
toolsMenuButton = MakeButton("Więcej ▾", (_, _) => toolsMenu.Show(toolsMenuButton, new Point(0, toolsMenuButton.Height)), false);
toolsMenuButton.Name = "ToolsMenuButton";
toolsMenuButton.AccessibleName = "Więcej czynności: źródła, historia, eksport i widok";
primaryActions.Controls.Add(toolsMenuButton);
toolbar.Controls.Add(primaryActions, 0, 0);
modeHint = new Label
{
Name = "SourceStateHint", Dock = DockStyle.Fill, ForeColor = Muted, BackColor = Surface,
Font = new Font("Segoe UI", 8.8F), Padding = new Padding(12, 0, 8, 0),
TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
Text = "TRYB OFFLINE  •  Import i dane testowe nie oznaczają odbioru RTL-SDR na żywo."
};
toolbar.Controls.Add(modeHint, 0, 1);
content.Controls.Add(toolbar, 0, 1);
var queryBar = new TableLayoutPanel
{
Name = "QueryLayout", Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1,
BackColor = Base, Padding = new Padding(0, 6, 0, 6), Margin = Padding.Empty
};
queryBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
queryBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
queryBar.Controls.Add(QueryLabel("Szukaj:"), 0, 0);
searchBox.Name = "SearchBox"; searchBox.Dock = DockStyle.Fill; searchBox.Margin = new Padding(0, 2, 10, 2);
searchBox.BackColor = Raised; searchBox.ForeColor = TextPrimary; searchBox.BorderStyle = BorderStyle.FixedSingle;
searchBox.Font = new Font("Segoe UI", 10F); searchBox.PlaceholderText = "Treść, rejestracja, MMSI, źródło...";
searchBox.TextChanged += (_, _) => { queryDebounceTimer.Stop(); queryDebounceTimer.Start(); };
queryBar.Controls.Add(searchBox, 1, 0);
queryBar.Controls.Add(QueryLabel("Status:"), 2, 0);
SetupCombo(integrityBox, new[] { "Każdy", "CRC / suma OK", "Błędne", "Niezweryfikowane" });
integrityBox.SelectedIndexChanged += (_, _) => RebuildTable();
queryBar.Controls.Add(integrityBox, 3, 0);
queryBar.Controls.Add(QueryLabel("Czas:"), 4, 0);
SetupCombo(periodBox, new[] { "Cały okres", "24 godziny", "7 dni", "30 dni" });
periodBox.SelectedIndexChanged += (_, _) => RebuildTable();
queryBar.Controls.Add(periodBox, 5, 0);
var resetQuery = MakeButton("Reset filtrów", (_, _) => ResetQuery(), false);
resetQuery.Dock = DockStyle.Fill; resetQuery.Margin = new Padding(3, 2, 0, 2); resetQuery.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
queryBar.Controls.Add(resetQuery, 6, 0);
content.Controls.Add(queryBar, 0, 2);
verticalSplit = new SplitContainer
{
Name = "VerticalSplitter", Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
BackColor = Border, SplitterWidth = 6, Margin = Padding.Empty
};
content.Controls.Add(verticalSplit, 0, 3);
horizontalSplit = new SplitContainer
{
Name = "HorizontalSplitter", Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
BackColor = Border, SplitterWidth = 6
};
verticalSplit.Panel1.Controls.Add(horizontalSplit);
recordsLayout = MakePanelLayout("RecordsLayout", "ODEBRANE REKORDY");
var recordsHead = (TableLayoutPanel)recordsLayout.GetControlFromPosition(0, 0);
recordsHead.ColumnCount = 2; recordsHead.ColumnStyles.Clear();
recordsHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
recordsHead.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
eventCounter.Dock = DockStyle.Fill; eventCounter.TextAlign = ContentAlignment.MiddleRight; eventCounter.ForeColor = Accent;
eventCounter.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
recordsHead.Controls.Add(eventCounter, 1, 0);
horizontalSplit.Panel1.Controls.Add(recordsLayout);
table.Dock = DockStyle.Fill; table.BackgroundColor = Surface; table.BorderStyle = BorderStyle.None; table.GridColor = Border;
table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; table.AllowUserToAddRows = false; table.AllowUserToDeleteRows = false;
table.AllowUserToResizeRows = false; table.ReadOnly = true; table.RowHeadersVisible = false; table.MultiSelect = false;
table.SelectionMode = DataGridViewSelectionMode.FullRowSelect; table.EnableHeadersVisualStyles = false;
table.ColumnHeadersHeight = 36; table.RowTemplate.Height = 30;
table.ColumnHeadersDefaultCellStyle.BackColor = Raised; table.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
table.DefaultCellStyle.BackColor = Surface; table.DefaultCellStyle.ForeColor = TextPrimary;
table.DefaultCellStyle.SelectionBackColor = Color.FromArgb(29, 92, 122); table.DefaultCellStyle.SelectionForeColor = Color.White;
table.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 38, 55);
table.Columns.Add("time", "UTC"); table.Columns[0].FillWeight = 24;
table.Columns.Add("protocol", "Protokół"); table.Columns[1].FillWeight = 37;
table.Columns.Add("status", "Status"); table.Columns[2].FillWeight = 62;
table.Columns.Add("summary", "Opis"); table.Columns[3].FillWeight = 74;
table.SelectionChanged += (_, _) => ShowSelection();
table.CellMouseDown += (_, e) => { if (e.Button != MouseButtons.Right || e.RowIndex < 0) return; table.ClearSelection(); table.Rows[e.RowIndex].Selected = true; table.CurrentCell = table.Rows[e.RowIndex].Cells[0]; };
var recordMenu = new ContextMenuStrip { BackColor = Raised, ForeColor = TextPrimary };
recordMenu.Items.Add("Kopiuj szczegóły zdarzenia", null, (_, _) => CopySelectedEvent());
recordMenu.Items.Add("Kopiuj treść / HEX", null, (_, _) => CopySelectedPayload());
table.ContextMenuStrip = recordMenu;
table.CellFormatting += (_, e) =>
{
if (e.RowIndex < 0 || e.ColumnIndex != 2 || e.Value is not string st) return;
if (st.Contains("INVALID") || st.Contains("BŁĘDNE")) e.CellStyle.ForeColor = Color.FromArgb(255, 139, 139);
else if (st.Contains("OK")) e.CellStyle.ForeColor = Color.FromArgb(125, 220, 174);
else e.CellStyle.ForeColor = Color.FromArgb(255, 204, 127);
};
var recordsCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = Padding.Empty };
recordsCanvas.Controls.Add(table); recordsLayout.Controls.Add(recordsCanvas, 0, 1);
emptyHint.Dock = DockStyle.Fill; emptyHint.Text = "BRAK ODEBRANYCH REKORDÓW\n\nKliknij »Dane testowe« lub »Importuj«, aby rozpocząć analizę.";
emptyHint.TextAlign = ContentAlignment.MiddleCenter; emptyHint.ForeColor = Muted; emptyHint.BackColor = Surface; emptyHint.Font = new Font("Segoe UI", 10F);
recordsCanvas.Controls.Add(emptyHint); emptyHint.BringToFront();
detailsLayout = MakePanelLayout("DetailsLayout", "SZCZEGÓŁY ZDARZENIA");
ConfigureEditor(details, readOnly: true); details.Dock = DockStyle.Fill; details.Text = "Wybierz rekord z listy, aby zobaczyć szczegóły.";
detailsLayout.Controls.Add(details, 0, 1); horizontalSplit.Panel2.Controls.Add(detailsLayout);
editorLayout = MakePanelLayout("EditorLayout", "DANE WEJŚCIOWE");
editorLayout.RowCount = 3; editorLayout.RowStyles.Clear();
editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27)); editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
editorLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Wklej tekst, ACARS_HEX / BITS, JSONL, NMEA, NAVTEX lub EGC", ForeColor = Muted, Font = new Font("Segoe UI", 8.5F), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(11, 0, 2, 0), AutoEllipsis = true }, 0, 1);
ConfigureEditor(input, readOnly: false); input.Dock = DockStyle.Fill;
input.Text = "# Wklej rekord albo kliknij Dane testowe.\n# ACARS_HEX: 01... | $CDDSC,...*HH | EGC:1:31:01A0:11:00";
editorLayout.Controls.Add(input, 0, 2); verticalSplit.Panel2.Controls.Add(editorLayout);
var footerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, BackColor = Base, Margin = Padding.Empty };
footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
status.Dock = DockStyle.Fill; status.ForeColor = Muted; status.Font = new Font("Segoe UI", 8.7F);
status.Text = "GOTOWE  •  Odbiór LOCAL TCP przygotowany; RF i DSP niezaimplementowane."; status.TextAlign = ContentAlignment.MiddleLeft; status.AutoEllipsis = true;
footerLayout.Controls.Add(status, 0, 0);
footerLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "OFFLINE / IMPORT", ForeColor = Accent, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }, 1, 0);
content.Controls.Add(footerLayout, 0, 4);
Shown += (_, _) =>
{
ResetSplitters();
if (verticalSplit.Height > 385) { verticalSplit.Panel1MinSize = 175; verticalSplit.Panel2MinSize = 145; }
if (horizontalSplit.Width > 650) { horizontalSplit.Panel1MinSize = 290; horizontalSplit.Panel2MinSize = 210; }
ResetSplitters(); UpdateCounter(); if (persistUi) BeginInvoke(new Action(RestoreUiPreferences));
};
}
}
