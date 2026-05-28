using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using PravVyd.Ui;

namespace PravVyd.Infrastructure;

/// <summary>Пробел на выбранном файле в Проводнике → мгновенное превью (как Quick Look).
/// Глотает пробел ТОЛЬКО когда впереди Проводник с выбранным файлом — иначе пропускает (не ломает набор текста).</summary>
public sealed class QuickLookService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly Dispatcher _dispatcher;
    private IntPtr _hook;
    private PreviewWindow? _preview;

    public QuickLookService()
    {
        _dispatcher = Application.Current.Dispatcher;
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0
            && wParam.ToInt32() == NativeMethods.WM_KEYDOWN
            && Marshal.ReadInt32(lParam) == NativeMethods.VK_SPACE)
        {
            try
            {
                if (TryHandleSpace())
                    return new IntPtr(1); // проглотить пробел
            }
            catch
            {
                // никогда не ломаем пробел глобально из-за ошибки превью
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool TryHandleSpace()
    {
        if (_preview is { IsVisible: true })
        {
            var open = _preview;
            _preview = null;
            _dispatcher.BeginInvoke(open.Close);
            return true;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        if (!IsExplorer(foreground))
            return false;

        var path = ExplorerSelection.GetSelectedFile(foreground);
        if (path is null)
            return false;

        _dispatcher.BeginInvoke(() => OpenPreview(path));
        return true;
    }

    private void OpenPreview(string path)
    {
        _preview = new PreviewWindow(path);
        _preview.Closed += (_, _) => _preview = null;
        _preview.Show();
        _preview.Activate();
    }

    private static bool IsExplorer(IntPtr hwnd)
    {
        var className = new StringBuilder(64);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);
        var name = className.ToString();
        return name is "CabinetWClass" or "ExploreWClass";
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
