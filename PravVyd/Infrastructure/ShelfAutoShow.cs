using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using PravVyd.Ui;

namespace PravVyd.Infrastructure;

/// <summary>Полка спрятана за краем экрана и плавно выезжает, когда пользователь начинает тащить файл:
/// зажал ЛКМ над Проводником/рабочим столом и повёл мышь. Отпустил — уезжает, если на полке пусто.
/// Глобальный low-level mouse hook видит ввод во всех окнах, в т.ч. во время drag.</summary>
public sealed class ShelfAutoShow : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private readonly Dispatcher _dispatcher;
    private readonly ShelfWindow _shelf;
    private IntPtr _hook;
    private bool _armed; // ЛКМ зажата над файловым окном — возможно начало drag
    private bool _shown; // полка уже выехала в текущем drag
    private bool _enabled = true;

    public ShelfAutoShow(ShelfWindow shelf)
    {
        _shelf = shelf;
        _dispatcher = Application.Current.Dispatcher;
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
                _dispatcher.BeginInvoke(_shelf.SlideOut); // выключили — спрятать
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _enabled)
        {
            try
            {
                switch (wParam.ToInt32())
                {
                    case NativeMethods.WM_LBUTTONDOWN:
                        OnDown(lParam);
                        break;
                    case NativeMethods.WM_MOUSEMOVE:
                        OnMove();
                        break;
                    case NativeMethods.WM_LBUTTONUP:
                        OnUp();
                        break;
                }
            }
            catch
            {
                // хук мыши — глобальный, никогда не падаем из колбэка
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void OnDown(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
        _armed = IsFileSource(data.pt);
        _shown = false;
    }

    private void OnMove()
    {
        if (_armed && !_shown)
        {
            _shown = true;
            _dispatcher.BeginInvoke(_shelf.SlideIn);
        }
    }

    private void OnUp()
    {
        _armed = false;
        if (!_shown)
            return;

        _shown = false;
        _dispatcher.BeginInvoke(_shelf.RequestRetract); // уедет через момент, если ничего не бросили
    }

    /// <summary>Под курсором — окно Проводника или рабочий стол (откуда тащат файлы)?</summary>
    private static bool IsFileSource(NativeMethods.POINT pt)
    {
        var hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero)
            return false;

        var sb = new StringBuilder(64);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString() is
            "CabinetWClass" or "ExploreWClass" or "DirectUIHWND"
            or "SysListView32" or "SHELLDLL_DefView" or "Progman" or "WorkerW";
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
