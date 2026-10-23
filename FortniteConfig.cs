using System.Text;

namespace DXVPrivateTweek;

public static class FortniteConfigService
{
    private const string BackupName = "Fortnite-GameUserSettings.ini.backup";

    public static string BackupPath(SnapshotStore store) => Path.Combine(store.DirectoryPath, BackupName);

    public static TweakResult ApplyVsyncOff(TweakContext context)
    {
        var path = context.FortniteConfigPath;
        if (!File.Exists(path))
            return new TweakResult(false, "GameUserSettings.ini не найден. Запусти Fortnite один раз и повтори.");

        try
        {
            File.Copy(path, BackupPath(context.Snapshots), overwrite: true);
            var lines = File.ReadAllLines(path).ToList();
            var inSettings = false;
            var changed = false;
            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                    inSettings = trimmed.Equals("[GameUserSettings]", StringComparison.OrdinalIgnoreCase);
                if (inSettings && trimmed.StartsWith("bUseVSync=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = "bUseVSync=False";
                    changed = true;
                }
            }
            if (!changed)
            {
                var index = lines.FindIndex(x => x.Trim().Equals("[GameUserSettings]", StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    return new TweakResult(false, "В конфиге нет секции [GameUserSettings]. Резервная копия сохранена, файл не изменён.");
                lines.Insert(index + 1, "bUseVSync=False");
            }
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            return new TweakResult(true, "В Fortnite config выставлено bUseVSync=False. Резервная копия сохранена.", true);
        }
        catch (Exception ex)
        {
            return new TweakResult(false, $"Не удалось изменить Fortnite config: {ex.Message}");
        }
    }

    public static TweakResult Restore(TweakContext context)
    {
        var backup = BackupPath(context.Snapshots);
        if (!File.Exists(backup))
            return new TweakResult(false, "Резервная копия Fortnite config не найдена.");
        try
        {
            File.Copy(backup, context.FortniteConfigPath, overwrite: true);
            return new TweakResult(true, "Fortnite config восстановлен.", true);
        }
        catch (Exception ex)
        {
            return new TweakResult(false, $"Не удалось восстановить Fortnite config: {ex.Message}");
        }
    }
}
