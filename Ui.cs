using System.Drawing;
using System.Text;

namespace DXVPrivateTweek;

public sealed class WelcomeForm : Form
{
    private readonly CheckBox _restorePoint = new();
    public bool CreateRestorePoint => _restorePoint.Checked;

    public WelcomeForm()
    {
        Text = "DXV PRIVATE TWEEK";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 320);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var title = new Label { Text = "DXV PRIVATE TWEEK", Font = new Font("Segoe UI", 20, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(new Label { Text = "Безопасная настройка Windows для игр. Каждое изменение можно откатить из резервной копии.", Dock = DockStyle.Fill, AutoSize = false }, 0, 1);

        _restorePoint.Text = "Создать точку восстановления перед изменениями (рекомендуется)";
        _restorePoint.Checked = true;
        _restorePoint.Dock = DockStyle.Top;
        root.Controls.Add(_restorePoint, 0, 3);

        var continueButton = new Button { Text = "Продолжить", Dock = DockStyle.Right, Width = 140, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Выход", Dock = DockStyle.Right, Width = 100, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 0, 8, 0) };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.Add(continueButton);
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons, 0, 4);

        Controls.Add(root);
        AcceptButton = continueButton;
        CancelButton = cancelButton;
    }
}

public sealed class MainForm : Form
{
    private readonly TweakContext _context;
    private readonly RichTextBox _log = new();
    private readonly FlowLayoutPanel _tweaks = new();
    private readonly Button _apply = new();
    private readonly Button _rollback = new();
    private readonly Button _preview = new();
    private readonly Button _selectFortnite = new();
    private readonly Button _latencyProfile = new();
    private readonly List<(CheckBox CheckBox, TweakDefinition Definition)> _checks = [];

    public MainForm(TweakContext context, IEnumerable<string> startupMessages)
    {
        _context = context;
        Text = "DXV PRIVATE TWEEK";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);
        Size = new Size(1100, 720);

        _context.Logger.LineAdded += line => BeginInvoke(() => _log.AppendText(line + Environment.NewLine));
        BuildUi();
        foreach (var message in startupMessages)
            _context.Logger.Info(message);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

        var header = new Label { Text = "DXV PRIVATE TWEEK", Font = new Font("Segoe UI", 22, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        root.Controls.Add(header, 0, 0);

        var hint = new Label { Text = "Отрицательной задержки не бывает: цель — минимальный стабильный input/render latency. Выберите нужные пункты и не отключайте защиту, обновления или системные службы ради FPS.", ForeColor = Color.DarkSlateGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        root.Controls.Add(hint, 0, 1);

        _tweaks.Dock = DockStyle.Fill;
        _tweaks.AutoScroll = true;
        _tweaks.FlowDirection = FlowDirection.TopDown;
        _tweaks.WrapContents = false;
        foreach (var group in SystemTweaks.All.GroupBy(x => x.Category))
        {
            var box = new GroupBox { Text = group.Key, AutoSize = true, Width = 1020, Padding = new Padding(10), Margin = new Padding(4) };
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foreach (var definition in group)
            {
                var check = new CheckBox
                {
                    Text = $"{definition.Name}  [{definition.Risk}]\n    {definition.Description}",
                    AutoSize = true,
                    Width = 980,
                    Padding = new Padding(4),
                    Margin = new Padding(2)
                };
                check.Tag = definition;
                panel.Controls.Add(check);
                _checks.Add((check, definition));
            }
            box.Controls.Add(panel);
            _tweaks.Controls.Add(box);
        }
        root.Controls.Add(_tweaks, 0, 2);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _preview.Text = "Показать изменения";
        _apply.Text = "Применить выбранное";
        _rollback.Text = "Откатить резервные копии";
        _selectFortnite.Text = "Выбрать Fortnite .exe";
        _latencyProfile.Text = "Профиль низкой задержки";
        var report = new Button { Text = "Сохранить отчёт" };
        _preview.Click += (_, _) => PreviewSelected();
        _apply.Click += async (_, _) => await ApplySelectedAsync();
        _rollback.Click += async (_, _) => await RollbackAsync();
        _selectFortnite.Click += (_, _) => SelectFortniteExecutable();
        _latencyProfile.Click += (_, _) => SelectLatencyProfile();
        report.Click += (_, _) => SaveReport();
        buttons.Controls.AddRange([_latencyProfile, _selectFortnite, _preview, _apply, _rollback, report]);
        bottom.Controls.Add(buttons, 0, 0);

        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 9);
        bottom.Controls.Add(_log, 0, 1);
        root.Controls.Add(bottom, 0, 3);
        Controls.Add(root);
    }

    private void PreviewSelected()
    {
        var selected = _checks.Where(x => x.CheckBox.Checked).Select(x => x.Definition).ToArray();
        if (selected.Length == 0)
        {
            _context.Logger.Warning("Ничего не выбрано.");
            return;
        }
        _context.Logger.Info("Предварительный просмотр изменений:");
        foreach (var definition in selected)
            _context.Logger.Info($"- {definition.Name}: {definition.Preview}");
    }

    private void SelectLatencyProfile()
    {
        var ids = new HashSet<string>(["game-mode", "disable-capture", "high-performance", "mouse-precision", "flush-dns"]);
        foreach (var item in _checks)
            item.CheckBox.Checked = ids.Contains(item.Definition.Id);
        _context.Logger.Info("Выбран рекомендуемый профиль минимальной задержки. Проверьте изменения перед применением.");
    }

    private void SelectFortniteExecutable()
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Выберите FortniteClient-Win64-Shipping.exe",
            Filter = "Fortnite executable|FortniteClient-Win64-Shipping.exe|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _context.SelectedFortniteExecutable = dialog.FileName;
        _context.Logger.Info($"Выбран Fortnite executable: {dialog.FileName}");
        var target = _checks.FirstOrDefault(x => x.Definition.Id == "fortnite-fullscreen");
        if (target.CheckBox is not null)
            target.CheckBox.Checked = true;
    }

    private async Task ApplySelectedAsync()
    {
        var selected = _checks.Where(x => x.CheckBox.Checked).Select(x => x.Definition).ToArray();
        if (selected.Length == 0)
        {
            _context.Logger.Warning("Ничего не выбрано.");
            return;
        }

        if (MessageBox.Show($"Применить выбранные изменения ({selected.Length})? Перед этим убедитесь, что важные файлы сохранены.",
                "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            foreach (var definition in selected)
            {
                _context.Logger.Info($"Применение: {definition.Name}");
                var result = await definition.ApplyAsync(_context);
                if (result.Success) _context.Logger.Info(result.Message); else _context.Logger.Error(result.Message);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RollbackAsync()
    {
        if (MessageBox.Show("Восстановить сохранённые значения всех поддерживаемых твиков?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            foreach (var definition in SystemTweaks.All)
            {
                var result = await definition.RollbackAsync(_context);
                if (result.Success && result.Changed) _context.Logger.Info(result.Message);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveReport()
    {
        using var dialog = new System.Windows.Forms.SaveFileDialog { Filter = "Текстовый отчёт|*.txt", FileName = $"DXV-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        File.WriteAllText(dialog.FileName, _log.Text, Encoding.UTF8);
        _context.Logger.Info($"Отчёт сохранён: {dialog.FileName}");
    }

    private void SetBusy(bool busy)
    {
        _apply.Enabled = !busy;
        _rollback.Enabled = !busy;
        _preview.Enabled = !busy;
        _selectFortnite.Enabled = !busy;
        _latencyProfile.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }
}
