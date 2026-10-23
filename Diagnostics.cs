using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows.Forms;

namespace DXVPrivateTweek;

public static class DiagnosticsService
{
    private static readonly string[] OverlayProcesses =
    [
        "GameBar", "GameBarFTServer", "Discord", "NVIDIA Share", "nvcontainer",
        "RadeonSoftware", "AMDRSServ", "obs64", "Overwolf", "RTSS", "Steam"
    ];

    public static async Task<string> RunAsync(TweakContext context)
    {
        var report = new StringBuilder();
        report.AppendLine("DXV PRIVATE TWEEK — диагностика ПК");
        report.AppendLine($"Время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"ОС: {Environment.OSVersion.VersionString}");
        report.AppendLine($"64-bit ОС: {Environment.Is64BitOperatingSystem}; процесс: {Environment.Is64BitProcess}");
        report.AppendLine($"CPU-потоки: {Environment.ProcessorCount}");
        report.AppendLine($"ОЗУ: {GetMemoryGb():0.0} GB");
        report.AppendLine($"Питание: {GetPowerState()}");
        report.AppendLine($"Конфиг Fortnite: {context.FortniteConfigPath} ({(File.Exists(context.FortniteConfigPath) ? "найден" : "не найден — запусти Fortnite один раз")})");

        foreach (var drive in DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == DriveType.Fixed))
            report.AppendLine($"Диск {drive.Name}: свободно {drive.AvailableFreeSpace / 1024d / 1024 / 1024:0.0} GB из {drive.TotalSize / 1024d / 1024 / 1024:0.0} GB");

        var hardware = await context.Commands.RunAsync("powershell.exe",
            "-NoProfile -NonInteractive -Command \"Get-CimInstance Win32_Processor,Win32_VideoController | Select-Object Name,DriverVersion | ConvertTo-Json -Compress\"");
        report.AppendLine("CPU/GPU:");
        report.AppendLine(string.IsNullOrWhiteSpace(hardware.StandardOutput) ? "  не удалось получить через WMI" : "  " + hardware.StandardOutput.Trim());

        var power = await context.Commands.RunAsync("powercfg.exe", "/getactivescheme");
        report.AppendLine("План питания: " + (power.StandardOutput + power.StandardError).Trim());

        foreach (var host in new[] { "epicgames.com", "1.1.1.1" })
            report.AppendLine(await PingSummaryAsync(host));

        report.AppendLine("Оверлеи и фоновые процессы (только обнаружение):");
        report.AppendLine(GetOverlayReport());
        report.AppendLine("Рекомендация: сравни этот отчёт с отчётом после изменений и проверь frametime в Fortnite.");
        return report.ToString();
    }

    public static string GetOverlayReport()
    {
        var running = Process.GetProcesses()
            .Select(p =>
            {
                try { return p.ProcessName; } catch { return ""; }
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = OverlayProcesses.Where(name => running.Contains(name)).ToArray();
        return found.Length == 0 ? "  не обнаружены" : "  " + string.Join(", ", found);
    }

    private static async Task<string> PingSummaryAsync(string host)
    {
        using var ping = new Ping();
        var times = new List<long>();
        var lost = 0;
        for (var i = 0; i < 5; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 1500);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime); else lost++;
            }
            catch { lost++; }
        }

        if (times.Count == 0)
            return $"Ping {host}: нет ответа (потери {lost}/5)";
        var average = times.Average();
        var jitter = times.Count > 1 ? times.Zip(times.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
        return $"Ping {host}: avg {average:0} ms, min {times.Min()} ms, max {times.Max()} ms, jitter {jitter:0} ms, потери {lost}/5";
    }

    private static double GetMemoryGb()
    {
        try { return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024d / 1024 / 1024; }
        catch { return 0; }
    }

    private static string GetPowerState()
    {
        try
        {
            var status = SystemInformation.PowerStatus;
            return status.PowerLineStatus == PowerLineStatus.Online ? "от сети" : "от батареи";
        }
        catch { return "неизвестно"; }
    }
}
