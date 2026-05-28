using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace PravVyd.Infrastructure;

/// <summary>Глобальный mouse-hook. Drag-выделение (зажал → провёл → отпустил) → показываем панель у курсора;
/// клик мимо панели → прячем. Не читает само выделение — лишь распознаёт жест.</summary>
public sealed class SelectionWatcher : IDisposable
{
    private const int DragThresholdSquaredPx = 36; // (6px)^2 — мельче считаем обычным кликом, не выделением

    private readonly Func<int, int, bool> _insidePill;
    private readonly Action<int, int> _onSelection;
    private readonly Action _onDismiss;
    private readonly Dispatcher _dispatcher;
    private readonly NativeMethods.LowLevelMouseProc _proc; // держим ссылку — иначе GC соберёт делегат

    private IntPtr _hook;
    private int _downX;
    private int _downY;
    private bool _downInside;
    private bool _hasDown;

    public bool Enabled { get; set; } = true;

    public SelectionWatcher(Func<int, int, bool> insidePill, Action<int, int> onSelection, Action onDismiss)
    {
        _insidePill = insidePill;
        _onSelection = onSelection;
        _onDismiss = onDismiss;
        _dispatcher = Application.Current.Dispatcher;
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Enabled)
        {
            var message = (int)wParam;

            if (message == NativeMethods.WM_LBUTTONDOWN)
            {
                var pt = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam).pt;
                _downX = pt.x;
                _downY = pt.y;
                _hasDown = true;
                _downInside = _insidePill(pt.x, pt.y);

                if (!_downInside)
                    _dispatcher.BeginInvoke(_onDismiss);
            }
            else if (message == NativeMethods.WM_LBUTTONUP && _hasDown)
            {
                _hasDown = false;
                var pt = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam).pt;

                if (!_downInside)
                {
                    var dx = pt.x - _downX;
                    var dy = pt.y - _downY;
                    if (dx * dx + dy * dy >= DragThresholdSquaredPx)
                    {
                        int x = pt.x;
                        int y = pt.y;
                        _dispatcher.BeginInvoke(() => _onSelection(x, y));
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
