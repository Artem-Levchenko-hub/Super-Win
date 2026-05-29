using System.Drawing;
using System.Windows.Forms;
using PravVyd.Infrastructure;
using PravVyd.Settings;
using WpfApp = System.Windows.Application;

namespace PravVyd.Ui;

/// <summary>Иконка в трее: вкл/выкл всплывания, настройки, выход. Прячет WinForms NotifyIcon (R-02).</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly BubbleWindow _bubble;
    private readonly SelectionWatcher _watcher;
    private readonly ShelfAutoShow _shelfAuto;
    private readonly AutoLayoutService _autoLayout;

    public TrayIcon(AppSettings settings, SettingsStore store, BubbleWindow bubble, SelectionWatcher watcher, ShelfAutoShow shelfAuto, AutoLayoutService autoLayout)
    {
        _settings = settings;
        _store = store;
        _bubble = bubble;
        _watcher = watcher;
        _shelfAuto = shelfAuto;
        _autoLayout = autoLayout;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Включить / выключить всплывание", null, (_, _) => ToggleFeature());

        var shelfItem = new ToolStripMenuItem("Полка: выезжает при перетаскивании") { CheckOnClick = true, Checked = _shelfAuto.Enabled };
        shelfItem.CheckedChanged += (_, _) => _shelfAuto.Enabled = shelfItem.Checked;
        menu.Items.Add(shelfItem);

        menu.Items.Add("Настройки…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripMenuItem("Закреп поверх окон: Ctrl+Alt+T") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem("OCR области: Ctrl+Alt+O") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem("Превью файла: Пробел (в Проводнике)") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem("Вернуть закрытую папку: Ctrl+Shift+T") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem("Исправить раскладку: Ctrl+Alt+R") { Enabled = false });

        var autoLayoutItem = new ToolStripMenuItem("Авто-раскладка по пробелу (ghbdtn→привет)")
        {
            CheckOnClick = true,
            Checked = _autoLayout.Enabled,
        };
        autoLayoutItem.CheckedChanged += (_, _) =>
        {
            _autoLayout.Enabled = autoLayoutItem.Checked;
            _settings.AutoLayoutFix = autoLayoutItem.Checked;
            _store.Save(_settings);
        };
        menu.Items.Add(autoLayoutItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => WpfApp.Current.Shutdown());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Правильный выделяльщик",
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => ToggleFeature();
    }

    private void ToggleFeature()
    {
        _watcher.Enabled = !_watcher.Enabled;
        if (!_watcher.Enabled)
            _bubble.HideBubble();

        _settings.BubbleVisible = _watcher.Enabled;
        _store.Save(_settings);
    }

    private void OpenSettings()
    {
        var existing = WpfApp.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        var window = new SettingsWindow(_settings, _store, _bubble.ApplyFormatVisibility);
        window.Show();
        window.Activate();
    }

    public void Notify(string message)
    {
        _icon.BalloonTipTitle = "Правильный выделяльщик";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(2000);
    }

    /// <summary>Балун «есть обновление»; клик по нему открывает страницу релиза в браузере.</summary>
    public void ShowUpdate(string version, string url)
    {
        _icon.BalloonTipTitle = "Доступно обновление";
        _icon.BalloonTipText = $"Версия {version} — нажмите, чтобы открыть страницу загрузки";

        void OnClick(object? sender, EventArgs e)
        {
            _icon.BalloonTipClicked -= OnClick;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // браузер не открылся — не критично
            }
        }

        _icon.BalloonTipClicked += OnClick;
        _icon.ShowBalloonTip(8000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
