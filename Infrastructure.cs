using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DXVPrivateTweek;

public sealed class AppLogger
{
    private readonly object _gate = new();
    private readonly string _logPath;

    public event Action<string>? LineAdded;

    public AppLogger()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DXVPrivateTweek", "Logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {level}: {message}";
        lock (_gate)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
        }
        LineAdded?.Invoke(line);
    }

    public string LogPath => _logPath;
}

public sealed class SnapshotStore
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DXVPrivateTweek", "Backups");

    public SnapshotStore() => Directory.CreateDirectory(_directory);

    public void Save<T>(string tweakId, T payload)
    {
        var document = new BackupDocument { TweakId = tweakId, Payload = payload };
        var path = Path.Combine(_directory, $"{tweakId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonDefaults.Options), Encoding.UTF8);
    }

    public bool TryLoad<T>(string tweakId, out T? payload)
    {
        var path = Path.Combine(_directory, $"{tweakId}.json");
        if (!File.Exists(path))
        {
            payload = default;
            return false;
        }

        try
        {
            var document = JsonSerializer.Deserialize<BackupDocument>(File.ReadAllText(path), JsonDefaults.Options);
            if (document?.Payload is null)
            {
                payload = default;
                return false;
            }

            var json = JsonSerializer.Serialize(document.Payload, JsonDefaults.Options);
            payload = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
            return payload is not null;
        }
        catch
        {
            payload = default;
            return false;
        }
    }

    public string DirectoryPath => _directory;
}

public sealed record CommandResult(bool Success, int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandRunner
{
    private readonly AppLogger _logger;

    public CommandRunner(AppLogger logger) => _logger = logger;

    public async Task<CommandResult> RunAsync(string fileName, string arguments)
    {
        _logger.Info($"> {fileName} {arguments}");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The process may have exited between the timeout and Kill call.
                }

                var timedOutOutput = await stdoutTask;
                var timedOutError = await stderrTask;
                _logger.Warning("Команда остановлена после таймаута 30 секунд.");
                return new CommandResult(false, -2, timedOutOutput, "Таймаут выполнения команды. " + timedOutError);
            }
            var output = await stdoutTask;
            var error = await stderrTask;
            var success = process.ExitCode == 0;
            if (!success)
                _logger.Warning($"Команда завершилась с кодом {process.ExitCode}: {error.Trim()}");
            return new CommandResult(success, process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            _logger.Error($"Не удалось запустить команду: {ex.Message}");
            return new CommandResult(false, -1, "", ex.Message);
        }
    }
}

public sealed class TweakContext
{
    public TweakContext(AppLogger logger, SnapshotStore snapshots, CommandRunner commands)
    {
        Logger = logger;
        Snapshots = snapshots;
        Commands = commands;
    }

    public AppLogger Logger { get; }
    public SnapshotStore Snapshots { get; }
    public CommandRunner Commands { get; }
    public string? SelectedFortniteExecutable { get; set; }
    public string FortniteConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");

    public async Task<TweakResult> CreateRestorePointAsync()
    {
        var escaped = "DXV PRIVATE TWEEK - перед оптимизацией".Replace("'", "''");
        var script = $"$ErrorActionPreference='Stop'; Checkpoint-Computer -Description '{escaped}' -RestorePointType 'MODIFY_SETTINGS'";
        var result = await Commands.RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"");
        if (result.Success)
            return new TweakResult(true, "Точка восстановления создана.", true);

        return new TweakResult(false,
            "Не удалось создать точку восстановления. Возможно, защита системы отключена; резервные копии приложения всё равно будут создаваться.");
    }

    public static RegistryValueSnapshot CaptureRegistryValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        if (key is null || !key.GetValueNames().Any(name => string.Equals(name, valueName, StringComparison.OrdinalIgnoreCase)))
            return new RegistryValueSnapshot { KeyPath = keyPath, ValueName = valueName, Exists = false };

        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var kind = key.GetValueKind(valueName);
        var data = kind switch
        {
            RegistryValueKind.DWord => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(value)
        };
        return new RegistryValueSnapshot { KeyPath = keyPath, ValueName = valueName, Exists = true, Kind = kind.ToString(), Data = data };
    }

    public static void RestoreRegistryValue(RegistryValueSnapshot snapshot)
    {
        if (!snapshot.Exists)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(snapshot.KeyPath, writable: true);
            existing?.DeleteValue(snapshot.ValueName, throwOnMissingValue: false);
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(snapshot.KeyPath, writable: true);
        if (key is null)
            return;

        var kind = Enum.TryParse<RegistryValueKind>(snapshot.Kind, out var parsed) ? parsed : RegistryValueKind.String;
        object value = kind switch
        {
            RegistryValueKind.DWord => int.TryParse(snapshot.Data, out var dword) ? dword : 0,
            RegistryValueKind.QWord => long.TryParse(snapshot.Data, out var qword) ? qword : 0L,
            _ => snapshot.Data ?? ""
        };
        key.SetValue(snapshot.ValueName, value, kind);
    }
}
