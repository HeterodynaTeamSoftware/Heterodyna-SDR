using Heterodyna.ProtocolLab.Core;
using Heterodyna.ProtocolLab.Gui;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Heterodyna.ProtocolLab;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                int total = CoreSelfTests.Run();
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "Heterodyna.ProtocolLab.SelfTest.txt"),
                    $"PASS: {total} testów danych syntetycznych. Nie są to testy odbioru RF, SDR ani GUI.\n");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "Heterodyna.ProtocolLab.SelfTest.txt"), "FAIL: " + ex);
                return 1;
            }
        }
        if (args.Contains("--gui-selftest", StringComparer.OrdinalIgnoreCase))
        {
            string report = Path.Combine(AppContext.BaseDirectory, "gui-smoketest.txt");
            try
            {
                ApplicationConfiguration.Initialize();
                using var form = new MainForm(persistUi: false);
                form.Show();
                Application.DoEvents();
                bool ok = form.IsHandleCreated && form.Visible && form.Controls.OfType<TableLayoutPanel>().Any();
                var layoutIssues = form.AuditLayout().ToList();
                form.Size = new System.Drawing.Size(Math.Max(form.MinimumSize.Width, 1100), Math.Max(form.MinimumSize.Height, 750));
                Application.DoEvents();
                layoutIssues.AddRange(form.AuditLayout());
                form.Close();
                if (!ok || layoutIssues.Count != 0)
                {
                    File.WriteAllText(report, "FAIL: GUI lub układ kontrolek.\n" + string.Join("\n", layoutIssues) + "\n");
                    return 2;
                }
                File.WriteAllText(report, "PASS: Okno utworzone, zmiana rozmiaru, sześć filtrów, kontrolki w siatce i rozdzielacze OK. Bez testów RF.\n");
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(report, "FAIL: " + ex); } catch (IOException) { }
                return 3;
            }
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
