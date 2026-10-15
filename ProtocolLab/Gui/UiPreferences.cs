using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace Heterodyna.ProtocolLab.Gui;

public sealed class UiPreferences
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
    public double UpperRatio { get; set; } = 0.64;
    public double LeftRatio { get; set; } = 0.63;
    public string Filter { get; set; } = "WSZYSTKIE";
    public int IntegrityIndex { get; set; }
    public int PeriodIndex { get; set; }
}

public static class UiPreferencesStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HeterodynaTeamSoftware", "ProtocolLab", "ui-preferences.json");

    public static UiPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var fi = new FileInfo(FilePath);
            if (fi.Length > 16 * 1024) return null;
            return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(FilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return null; }
    }

    public static void Save(UiPreferences prefs)
    {
        string temp = null;
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(dir);
            temp = Path.Combine(dir, ".ui-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, overwrite: true);
            temp = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { }
        finally
        {
            if (temp is not null)
                try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static bool IsVisibleOnScreen(Rectangle bounds)
    {
        if (bounds.Width < 1050 || bounds.Height < 710 || bounds.Width > 10000 || bounds.Height > 10000) return false;
        foreach (var screen in Screen.AllScreens)
        {
            var visible = Rectangle.Intersect(bounds, screen.WorkingArea);
            if (visible.Width >= 200 && visible.Height >= 150) return true;
        }
        return false;
    }
}
