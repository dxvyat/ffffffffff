using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace DXVPrivateTweek;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            MessageBox.Show("DXV PRIVATE TWEEK работает только в Windows 10/11.", "DXV PRIVATE TWEEK",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!IsAdministrator())
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "DXVPrivateTweek.exe",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                MessageBox.Show("Для применения системных настроек нужны права администратора.", "DXV PRIVATE TWEEK",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        var logger = new AppLogger();
        var store = new SnapshotStore();
        var runner = new CommandRunner(logger);
        var context = new TweakContext(logger, store, runner);

        using var welcome = new WelcomeForm();
        if (welcome.ShowDialog() != DialogResult.OK)
            return;

        Application.Run(new MainForm(context, welcome.CreateRestorePoint));
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
