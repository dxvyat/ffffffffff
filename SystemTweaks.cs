using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace DXVPrivateTweek;

public static class SystemTweaks
{
    public static IReadOnlyList<TweakDefinition> All { get; } = Build();

    private static List<TweakDefinition> Build() =>
    [
        new()
        {
            Id = "game-mode",
            Category = "Windows и игра",
            Name = "Включить игровой режим Windows",
            Description = "Разрешает Windows отдавать игре приоритет над фоновыми задачами.",
            Risk = "Безопасно",
            Preview = "HKCU\\Software\\Microsoft\\GameBar: AllowAutoGameMode=1, AutoGameModeEnabled=1",
            ApplyAsync = ctx => SetRegistryValuesAsync(ctx, "game-mode", [
                ("Software\\Microsoft\\GameBar", "AllowAutoGameMode", 1),
                ("Software\\Microsoft\\GameBar", "AutoGameModeEnabled", 1)]),
            RollbackAsync = ctx => RestoreRegistryValuesAsync(ctx, "game-mode")
        },
        new()
        {
            Id = "disable-capture",
            Category = "Windows и игра",
            Name = "Отключить фоновую запись Game Bar",
            Description = "Отключает запись клипов в фоне, чтобы она не забирала CPU/GPU и диск. Xbox Game Bar останется доступным.",
            Risk = "Внимание",
            Preview = "HKCU\\System\\GameConfigStore: GameDVR_Enabled=0; HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR: AppCaptureEnabled=0",
            ApplyAsync = ctx => SetRegistryValuesAsync(ctx, "disable-capture", [
                ("System\\GameConfigStore", "GameDVR_Enabled", 0),
                ("Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR", "AppCaptureEnabled", 0)]),
            RollbackAsync = ctx => RestoreRegistryValuesAsync(ctx, "disable-capture")
        },
        new()
        {
            Id = "mouse-precision",
            Category = "Мышь и клавиатура",
            Name = "Отключить ускорение мыши",
            Description = "Отключает Enhance Pointer Precision. Чувствительность мыши не меняется.",
            Risk = "Внимание",
            Preview = "HKCU\\Control Panel\\Mouse: MouseSpeed=0, MouseThreshold1=0, MouseThreshold2=0",
            ApplyAsync = ctx => SetRegistryValuesAsync(ctx, "mouse-precision", [
                ("Control Panel\\Mouse", "MouseSpeed", "0"),
                ("Control Panel\\Mouse", "MouseThreshold1", "0"),
                ("Control Panel\\Mouse", "MouseThreshold2", "0")]),
            RollbackAsync = ctx => RestoreRegistryValuesAsync(ctx, "mouse-precision")
        },
        new()
        {
            Id = "keyboard-response",
            Category = "Мышь и клавиатура",
            Name = "Настроить быстрый отклик клавиатуры",
            Description = "Ставит максимальную скорость повтора и минимальную задержку повтора.",
            Risk = "Внимание",
            Preview = "HKCU\\Control Panel\\Keyboard: KeyboardSpeed=31, KeyboardDelay=0",
            ApplyAsync = ctx => SetRegistryValuesAsync(ctx, "keyboard-response", [
                ("Control Panel\\Keyboard", "KeyboardSpeed", "31"),
                ("Control Panel\\Keyboard", "KeyboardDelay", "0")]),
            RollbackAsync = ctx => RestoreRegistryValuesAsync(ctx, "keyboard-response")
        },
        new()
        {
            Id = "high-performance",
            Category = "Питание",
            Name = "Включить план высокой производительности",
            Description = "Переключает активный план питания на встроенный High performance. Исходный GUID сохраняется для отката.",
            Risk = "Внимание",
            Preview = "powercfg /setactive SCHEME_MIN",
            ApplyAsync = ApplyHighPerformanceAsync,
            RollbackAsync = RollbackPowerPlanAsync
        },
        new()
        {
            Id = "flush-dns",
            Category = "Сеть",
            Name = "Очистить DNS-кэш",
            Description = "Очищает только локальный кэш DNS. Постоянные сетевые параметры не меняются.",
            Risk = "Безопасно",
            Preview = "ipconfig /flushdns",
            ApplyAsync = FlushDnsAsync,
            RollbackAsync = _ => Task.FromResult(new TweakResult(true, "Для очистки DNS-кэша откат не требуется."))
        },
        new()
        {
            Id = "fortnite-fullscreen",
            Category = "Fortnite",
            Name = "Настроить выбранный Fortnite .exe для fullscreen",
            Description = "Отключает оптимизацию полноэкранного режима только для выбранного FortniteClient-Win64-Shipping.exe. Это не гарантирует улучшения на каждом ПК.",
            Risk = "Внимание",
            Preview = "HKCU\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers: выбранный Fortnite .exe = ~ DISABLEDXMAXIMIZEDWINDOWEDMODE",
            ApplyAsync = ApplyFortniteFullscreenAsync,
            RollbackAsync = ctx => RestoreRegistryValuesAsync(ctx, "fortnite-fullscreen")
        }
    ];

    private static Task<TweakResult> SetRegistryValuesAsync(
        TweakContext ctx, string tweakId, IReadOnlyList<(string KeyPath, string Name, object Value)> values)
    {
        RegistryValueSnapshot[]? snapshots = null;
        try
        {
            snapshots = values.Select(v => TweakContext.CaptureRegistryValue(v.KeyPath, v.Name)).ToArray();
            ctx.Snapshots.Save(tweakId, snapshots);
            foreach (var value in values)
            {
                using var key = Registry.CurrentUser.CreateSubKey(value.KeyPath, writable: true)
                    ?? throw new InvalidOperationException($"Не удалось открыть HKCU\\{value.KeyPath}");
                key.SetValue(value.Name, value.Value, value.Value is int ? RegistryValueKind.DWord : RegistryValueKind.String);
            }
            return Task.FromResult(new TweakResult(true, $"Применено: {tweakId}.", true));
        }
        catch (Exception ex)
        {
            if (snapshots is not null)
            {
                try
                {
                    foreach (var snapshot in snapshots)
                        TweakContext.RestoreRegistryValue(snapshot);
                }
                catch (Exception rollbackError)
                {
                    ctx.Logger.Error($"Не удалось полностью откатить частичное изменение {tweakId}: {rollbackError.Message}");
                }
            }
            ctx.Logger.Error($"Ошибка реестра ({tweakId}): {ex.Message}");
            return Task.FromResult(new TweakResult(false, $"Не удалось применить {tweakId}: {ex.Message}"));
        }
    }

    private static Task<TweakResult> RestoreRegistryValuesAsync(TweakContext ctx, string tweakId)
    {
        if (!ctx.Snapshots.TryLoad<RegistryValueSnapshot[]>(tweakId, out var snapshots) || snapshots is null)
            return Task.FromResult(new TweakResult(false, $"Резервная копия для {tweakId} не найдена."));

        try
        {
            foreach (var snapshot in snapshots)
                TweakContext.RestoreRegistryValue(snapshot);
            return Task.FromResult(new TweakResult(true, $"Откат выполнен: {tweakId}.", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TweakResult(false, $"Не удалось откатить {tweakId}: {ex.Message}"));
        }
    }

    private static async Task<TweakResult> ApplyHighPerformanceAsync(TweakContext ctx)
    {
        var current = await ctx.Commands.RunAsync("powercfg.exe", "/getactivescheme");
        if (!current.Success)
            return new TweakResult(false, "Не удалось определить текущий план питания.");

        var match = Regex.Match(current.StandardOutput + current.StandardError,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        if (!match.Success)
            return new TweakResult(false, "Не удалось сохранить текущий план питания для отката.");

        ctx.Snapshots.Save("high-performance", new PowerPlanSnapshot { ActiveGuid = match.Value });
        var applied = await ctx.Commands.RunAsync("powercfg.exe", "/setactive SCHEME_MIN");
        return applied.Success
            ? new TweakResult(true, "План высокой производительности включён.", true)
            : new TweakResult(false, "Не удалось включить план высокой производительности.");
    }

    private static async Task<TweakResult> RollbackPowerPlanAsync(TweakContext ctx)
    {
        if (!ctx.Snapshots.TryLoad<PowerPlanSnapshot>("high-performance", out var snapshot) || string.IsNullOrWhiteSpace(snapshot?.ActiveGuid))
            return new TweakResult(false, "Резервная копия плана питания не найдена.");

        var result = await ctx.Commands.RunAsync("powercfg.exe", $"/setactive {snapshot.ActiveGuid}");
        return result.Success
            ? new TweakResult(true, "Исходный план питания восстановлен.", true)
            : new TweakResult(false, "Не удалось восстановить исходный план питания.");
    }

    private static async Task<TweakResult> FlushDnsAsync(TweakContext ctx)
    {
        var result = await ctx.Commands.RunAsync("ipconfig.exe", "/flushdns");
        return result.Success
            ? new TweakResult(true, "DNS-кэш очищен.", true)
            : new TweakResult(false, "Не удалось очистить DNS-кэш.");
    }

    private static Task<TweakResult> ApplyFortniteFullscreenAsync(TweakContext ctx)
    {
        const string keyPath = "Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers";
        var executable = ctx.SelectedFortniteExecutable;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return Task.FromResult(new TweakResult(false, "Сначала нажмите «Выбрать Fortnite .exe» и укажите FortniteClient-Win64-Shipping.exe."));

        try
        {
            var snapshot = TweakContext.CaptureRegistryValue(keyPath, executable);
            ctx.Snapshots.Save("fortnite-fullscreen", snapshot);
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
                ?? throw new InvalidOperationException("Не удалось открыть настройки совместимости Windows.");
            key.SetValue(executable, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", RegistryValueKind.String);
            return Task.FromResult(new TweakResult(true, $"Fullscreen-настройка применена только к {Path.GetFileName(executable)}.", true));
        }
        catch (Exception ex)
        {
            ctx.Logger.Error($"Ошибка настройки Fortnite fullscreen: {ex.Message}");
            return Task.FromResult(new TweakResult(false, $"Не удалось настроить Fortnite fullscreen: {ex.Message}"));
        }
    }
}
