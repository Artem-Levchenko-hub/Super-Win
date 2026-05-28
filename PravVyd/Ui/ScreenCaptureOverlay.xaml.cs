using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PravVyd.Ui;

/// <summary>Полноэкранный оверлей: пользователь выделяет прямоугольник мышью.
/// SelectAsync возвращает область в физических пикселях или null при отмене (Esc / слишком маленькое).</summary>
public partial class ScreenCaptureOverlay : Window
{
    private readonly TaskCompletionSource<Int32Rect?> _result = new();
    private Point _start;
    private bool _dragging;

    public ScreenCaptureOverlay()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public Task<Int32Rect?> SelectAsync()
    {
        Show();
        Activate();
        Focus();
        return _result.Task;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _start = e.GetPosition(Root);
        _dragging = true;

        Canvas.SetLeft(Selection, _start.X);
        Canvas.SetTop(Selection, _start.Y);
        Selection.Width = 0;
        Selection.Height = 0;
        Selection.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
            return;

        var p = e.GetPosition(Root);
        var x = Math.Min(p.X, _start.X);
        var y = Math.Min(p.Y, _start.Y);
        var w = Math.Abs(p.X - _start.X);
        var h = Math.Abs(p.Y - _start.Y);

        Canvas.SetLeft(Selection, x);
        Canvas.SetTop(Selection, y);
        Selection.Width = w;
        Selection.Height = h;

        SizeText.Text = $"{(int)w} × {(int)h}";
        Canvas.SetLeft(SizeBadge, x);
        Canvas.SetTop(SizeBadge, Math.Max(0, y - 26));
        SizeBadge.Visibility = Visibility.Visible;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging)
            return;

        _dragging = false;
        ReleaseMouseCapture();
        Complete(_start, e.GetPosition(Root));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Finish(null);
    }

    private void Complete(Point a, Point b)
    {
        // в физические пиксели экрана (корректно для текущего монитора)
        var p1 = PointToScreen(a);
        var p2 = PointToScreen(b);

        var x = (int)Math.Round(Math.Min(p1.X, p2.X));
        var y = (int)Math.Round(Math.Min(p1.Y, p2.Y));
        var w = (int)Math.Round(Math.Abs(p1.X - p2.X));
        var h = (int)Math.Round(Math.Abs(p1.Y - p2.Y));

        Finish(w < 2 || h < 2 ? null : new Int32Rect(x, y, w, h));
    }

    private void Finish(Int32Rect? region)
    {
        if (!_result.Task.IsCompleted)
            _result.SetResult(region);

        Close();
    }
}
