using System.Windows;
using PravVyd.Documents;
using PravVyd.Infrastructure;
using PravVyd.Services;
using PravVyd.Settings;
using PravVyd.Ui;

namespace PravVyd;

public partial class App : Application
{
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private BubbleWindow _bubble = null!;
    private SelectionWatcher _watcher = null!;
    private TrayIcon _tray = null!;
    private GlobalHotkeys _hotkeys = null!;
    private WindowPinner _pinner = null!;
    private QuickLookService _quicklook = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _store = new SettingsStore();
        _settings = _store.Load();

        var service = new DocumentService(
            new SelectionCapture(),
            new OutputPathResolver(_settings),
            new ClipboardService(),
            new IDocumentWriter[] { new MarkdownWriter(), new PdfWriter(), new DocxWriter() });

        _bubble = new BubbleWindow(_settings, _store, service);

        // панель появляется по выделению, а не висит постоянно
        _watcher = new SelectionWatcher(_bubble.HitTestPhysical, _bubble.ShowNear, _bubble.HideBubble)
        {
            Enabled = _settings.BubbleVisible,
        };

        _tray = new TrayIcon(_settings, _store, _bubble, _watcher);

        // глобальные хоткеи: Ctrl+Alt+T — закреп окна поверх; Ctrl+Alt+O — OCR области экрана
        _hotkeys = new GlobalHotkeys();
        _pinner = new WindowPinner(_hotkeys, _tray.Notify);
        _ = new ScreenOcrService(_hotkeys, new ClipboardService(), _tray.Notify);

        // пробел в Проводнике на выбранном файле → мгновенное превью (Quick Look)
        _quicklook = new QuickLookService();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _quicklook?.Dispose();
        _pinner?.Dispose();
        _hotkeys?.Dispose();
        _watcher?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
