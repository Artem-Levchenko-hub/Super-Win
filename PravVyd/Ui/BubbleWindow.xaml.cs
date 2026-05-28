using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PravVyd.Documents;
using PravVyd.Infrastructure;
using PravVyd.Services;
using PravVyd.Settings;

namespace PravVyd.Ui;

public partial class BubbleWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly DocumentService _service;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _autoHide;

    private bool _dragging;
    private Point _dragOrigin;
    private double _startLeft;
    private double _startTop;

    public BubbleWindow(AppSettings settings, SettingsStore store, DocumentService service)
    {
        InitializeComponent();

        _settings = settings;
        _store = store;
        _service = service;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast.Visibility = Visibility.Collapsed;
        };

        _autoHide = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _autoHide.Tick += (_, _) => HideBubble();

        ApplyFormatVisibility();
    }

    /// <summary>Прячет кнопки форматов, выключенных в настройках.</summary>
    public void ApplyFormatVisibility()
    {
        MdButton.Visibility = _settings.MarkdownEnabled ? Visibility.Visible : Visibility.Collapsed;
        PdfButton.Visibility = _settings.PdfEnabled ? Visibility.Visible : Visibility.Collapsed;
        DocxButton.Visibility = _settings.DocxEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Показать панель над курсором. Координаты — физические пиксели от mouse-hook.
    /// Позиционируем через SetWindowPos в физ. пикселях: верно для нескольких мониторов и per-monitor DPI.</summary>
    public void ShowNear(int physicalX, int physicalY)
    {
        if (!IsVisible)
            Show();

        UpdateLayout();

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.GetWindowRect(hwnd, out var rect);
        var width = rect.right - rect.left;
        var height = rect.bottom - rect.top;
        if (width <= 0) width = 220;
        if (height <= 0) height = 60;

        // рабочая область монитора под курсором (физ. пиксели)
        var monitor = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT { x = physicalX, y = physicalY }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitor, ref info);
        var work = info.rcWork;

        var x = physicalX + 8;
        var y = physicalY - height - 12; // над текстом

        if (y < work.top)
            y = physicalY + 20; // сверху нет места — показываем снизу
        if (x + width > work.right)
            x = work.right - width - 6;
        if (x < work.left)
            x = work.left + 6;
        if (y + height > work.bottom)
            y = work.bottom - height - 6;

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        _autoHide.Stop();
        _autoHide.Start();
    }

    public void HideBubble()
    {
        _autoHide.Stop();
        _toastTimer.Stop();
        Toast.Visibility = Visibility.Collapsed;
        Hide();
    }

    /// <summary>Попадает ли экранная точка (физ. пиксели) в панель — чтобы клик по кнопкам не считался "кликом мимо".</summary>
    public bool HitTestPhysical(int physicalX, int physicalY)
    {
        if (!IsVisible)
            return false;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        NativeMethods.GetWindowRect(hwnd, out var rect);
        return physicalX >= rect.left && physicalX <= rect.right
            && physicalY >= rect.top && physicalY <= rect.bottom;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // окно не должно перехватывать фокус — иначе Ctrl+C уйдёт нам, а не приложению с выделением
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private async void Md_Click(object sender, RoutedEventArgs e) => await GenerateAsync(DocFormat.Markdown);

    private async void Pdf_Click(object sender, RoutedEventArgs e) => await GenerateAsync(DocFormat.Pdf);

    private async void Docx_Click(object sender, RoutedEventArgs e) => await GenerateAsync(DocFormat.Docx);

    private async Task GenerateAsync(DocFormat format)
    {
        _autoHide.Stop();

        var result = await _service.CreateFromSelectionAsync(format);

        ShowToast(result.Outcome switch
        {
            DocumentOutcome.Success => "✓ Файл готов — нажмите Ctrl+V",
            DocumentOutcome.NoSelection => "Сначала выделите текст",
            _ => "Ошибка: " + result.Error,
        });

        // формат выбран — панель прячется, показав короткий статус
        await Task.Delay(1200);
        HideBubble();
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings, _store, ApplyFormatVisibility);
        window.Show();
        window.Activate();
    }

    private void Grip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragOrigin = PointToScreen(e.GetPosition(this));
        _startLeft = Left;
        _startTop = Top;
        Grip.CaptureMouse();
        _autoHide.Stop();
    }

    private void Grip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        var current = PointToScreen(e.GetPosition(this));
        Left = _startLeft + (current.X - _dragOrigin.X);
        Top = _startTop + (current.Y - _dragOrigin.Y);
    }

    private void Grip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        Grip.ReleaseMouseCapture();
        _autoHide.Start();
    }
}
