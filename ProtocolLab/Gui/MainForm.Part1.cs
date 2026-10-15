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
    private static readonly Color Base = Color.FromArgb(11, 17, 27);
    private static readonly Color Sidebar = Color.FromArgb(16, 26, 39);
    private static readonly Color Surface = Color.FromArgb(20, 33, 49);
    private static readonly Color Raised = Color.FromArgb(28, 44, 63);
    private static readonly Color Accent = Color.FromArgb(56, 190, 232);
    private static readonly Color TextPrimary = Color.FromArgb(232, 240, 249);
    private static readonly Color Muted = Color.FromArgb(157, 177, 196);
    private static readonly Color Border = Color.FromArgb(48, 67, 88);
    private const int EventLimit = 10000;
    private readonly bool persistUi;
    private readonly System.Windows.Forms.Timer queryDebounceTimer = new() { Interval = 260 };
    private readonly List<DecodeEvent> entries = new();
    private readonly LiveReceiver liveReceiver = new();
    private readonly System.Windows.Forms.Timer livePollTimer = new() { Interval = 180 };
    private Label liveMetrics;
    private Label modeHint;
    private Button liveStartButton;
    private Button liveStopButton;
    private Button toolsMenuButton;
    private ContextMenuStrip toolsMenu;
    private string liveFailure = "";
    private readonly DataGridView table = new();
    private readonly RichTextBox input = new();
    private readonly RichTextBox details = new();
    private readonly Label status = new();
    private readonly Label eventCounter = new();
    private readonly Label emptyHint = new();
    private readonly TextBox searchBox = new();
    private readonly ComboBox integrityBox = new();
    private readonly ComboBox periodBox = new();
    private Label brandLabel;
    private TableLayoutPanel navigationLayout;
    private TableLayoutPanel recordsLayout;
    private TableLayoutPanel detailsLayout;
    private TableLayoutPanel editorLayout;
    private SplitContainer verticalSplit;
    private SplitContainer horizontalSplit;
    private Label receiverBadge;
    private readonly Dictionary<string, Button> filterButtons = new();
    private readonly ConcurrentDictionary<string, byte> pendingFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (long Length, long WriteTicks, int EventCount)> importedFiles = new(StringComparer.OrdinalIgnoreCase);
    private string activeFilter = "WSZYSTKIE";
    private FileSystemWatcher watcher;
    private int watcherGeneration;
    private bool closing;

    public MainForm(bool persistUi = true)
    {
        this.persistUi = persistUi;
        Text = "Heterodyna SDR — Protocol Lab 0.3.3 [eksperymentalny]";
        Width = 1440;
        Height = 900;
        MinimumSize = new Size(1050, 710);
        DoubleBuffered = true;
        KeyPreview = true;
        KeyDown += HandleShortcuts;
        queryDebounceTimer.Tick += (_, _) => { queryDebounceTimer.Stop(); if (!closing) RebuildTable(); };
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Base;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildUi();
        livePollTimer.Tick += (_, _) => PollLive();
        livePollTimer.Start();
        FormClosing += (_, _) => { if (persistUi) SaveUiPreferences(); closing = true; queryDebounceTimer.Stop(); livePollTimer.Stop(); StopWatcher(); liveReceiver.Dispose(); };
    }
}
