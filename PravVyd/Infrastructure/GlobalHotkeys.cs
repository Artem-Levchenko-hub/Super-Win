using System.Windows.Interop;

namespace PravVyd.Infrastructure;

/// <summary>Один message-only sink на все глобальные хоткеи. Register(модификаторы, клавиша, действие).
/// Делегаты держатся в словаре — это же удерживает владельцев (pinner/ocr) от GC.</summary>
public sealed class GlobalHotkeys : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0xC000;

    public GlobalHotkeys()
    {
        var parameters = new HwndSourceParameters("PravVydHotkeySink")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE — message-only окно для приёма WM_HOTKEY
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool Register(uint modifiers, uint virtualKey, Action handler)
    {
        var id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey))
            return false;

        _handlers[id] = handler;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var handler))
        {
            handler();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _handlers.Clear();

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
