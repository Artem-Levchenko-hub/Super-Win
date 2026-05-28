namespace PravVyd.Infrastructure;

/// <summary>Хоткей Ctrl+Alt+T: закрепляет активное окно поверх всех (topmost),
/// чтобы оно не пряталось за полноэкранным приложением. Повторное нажатие на том же окне — снять.</summary>
public sealed class WindowPinner : IDisposable
{
    private readonly HashSet<IntPtr> _pinned = new();
    private readonly Action<string>? _notify;

    public WindowPinner(GlobalHotkeys hotkeys, Action<string>? notify = null)
    {
        _notify = notify;
        hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_T, ToggleForeground);
    }

    private void ToggleForeground()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || IsOwnWindow(hwnd))
            return;

        if (_pinned.Remove(hwnd))
        {
            SetTopmost(hwnd, on: false);
            _notify?.Invoke("Окно откреплено");
        }
        else
        {
            _pinned.Add(hwnd);
            SetTopmost(hwnd, on: true);
            _notify?.Invoke("Окно закреплено поверх (Ctrl+Alt+T — снять)");
        }
    }

    private static void SetTopmost(IntPtr hwnd, bool on)
    {
        NativeMethods.SetWindowPos(hwnd,
            on ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    public void Dispose()
    {
        foreach (var hwnd in _pinned)
            SetTopmost(hwnd, on: false);
        _pinned.Clear();
    }
}
