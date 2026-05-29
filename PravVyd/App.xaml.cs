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
    private RecentlyClosedService _recentClosed = null!;
    private ShelfWindow _shelf = null!;
    private ShelfAutoShow _shelfAuto = null!;
    private AutoLayoutService _autoLayout = null!;

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

        // полка прячется за краем экрана и плавно выезжает при перетаскивании файла
        _shelf = new ShelfWindow();
        _shelf.Show(); // создаём HWND за правым краем (скрыто) — чтобы стать drop-целью, когда выедет
        _shelfAuto = new ShelfAutoShow(_shelf);

        // авто-раскладка по пробелу (Punto-style, на словарях): ghbdtn↔привет, руддщ↔hello + смена раскладки
        _autoLayout = new AutoLayoutService(_settings, new WordDictionary());

        _tray = new TrayIcon(_settings, _store, _bubble, _watcher, _shelfAuto, _autoLayout);

        // глобальные хоткеи: Ctrl+Alt+T — закреп окна поверх; Ctrl+Alt+O — OCR области экрана
        _hotkeys = new GlobalHotkeys();
        _pinner = new WindowPinner(_hotkeys, _tray.Notify);
        _ = new ScreenOcrService(_hotkeys, new ClipboardService(), _tray.Notify);

        // Ctrl+Alt+R — исправить раскладку выделенного текста
        _ = new LayoutFixService(_hotkeys, new SelectionCapture(), new ClipboardService(), _tray.Notify);

        // пробел в Проводнике на выбранном файле → мгновенное превью (Quick Look)
        _quicklook = new QuickLookService();

        // Ctrl+Shift+T — открыть заново последнюю закрытую папку Проводника
        _recentClosed = new RecentlyClosedService(_hotkeys, _tray.Notify);

        // проверка обновлений: спросить последний GitHub Release, новее текущей → балун в трее
        new UpdateChecker((version, url) => _tray.ShowUpdate(version, url)).CheckInBackground();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _recentClosed?.Dispose();
        _shelfAuto?.Dispose();
        _autoLayout?.Dispose();
        _quicklook?.Dispose();
        _pinner?.Dispose();
        _hotkeys?.Dispose();
        _watcher?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
