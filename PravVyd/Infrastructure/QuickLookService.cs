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
    private bool _spaceHeld;

    public QuickLookService()
    {
        _dispatcher = Application.Current.Dispatcher;
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Marshal.ReadInt32(lParam) == NativeMethods.VK_SPACE)
        {
            var message = wParam.ToInt32();
            try
            {
                if (message == NativeMethods.WM_KEYDOWN && OnSpaceDown())
                    return new IntPtr(1); // проглотить пробел
                if (message == NativeMethods.WM_KEYUP && OnSpaceUp())
                    return new IntPtr(1);
            }
            catch
            {
                // никогда не ломаем пробел глобально из-за ошибки превью
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool OnSpaceDown()
    {
        if (_spaceHeld)
            return true; // автоповтор удержания — глотаем, превью уже открыто

        // в самом хуке — только быстрые проверки. COM/Shell выносим в Dispatcher,
        // иначе медленный колбэк превысит LowLevelHooksTimeout и Windows снимет хук
        if (!IsExplorer(NativeMethods.GetForegroundWindow()))
            return false;

        _spaceHeld = true;
        _dispatcher.BeginInvoke(OpenFromSelection);
        return true;
    }

    private bool OnSpaceUp()
    {
        if (!_spaceHeld)
            return false;

        _spaceHeld = false;
        _dispatcher.BeginInvoke(ClosePreview);
        return true;
    }

    private void OpenFromSelection()
    {
        var path = ExplorerSelection.GetSelectedFile(NativeMethods.GetForegroundWindow());
        if (path is null)
            return;

        ClosePreview();
        _preview = new PreviewWindow(path);
        _preview.Closed += (_, _) => _preview = null;
        _preview.Show();
        _preview.Activate();
    }

    private void ClosePreview()
    {
        _preview?.Close();
        _preview = null;
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
