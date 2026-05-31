using System.Runtime.InteropServices;

namespace PravVyd.Infrastructure;

/// <summary>Достаёт путь выбранного файла из активного окна Проводника через Shell COM (late-bound dynamic).</summary>
public static class ExplorerSelection
{
    public static string? GetSelectedFile(IntPtr foreground)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
            return null;

        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            dynamic windows = ((dynamic)shell!).Windows();
            int count = windows.Count;

            for (var i = 0; i < count; i++)
            {
                dynamic? window = windows.Item(i);
                if (window is null)
                    continue;

                if ((IntPtr)(long)window.HWND != foreground)
                    continue;

                dynamic items = window.Document.SelectedItems();
                if (items.Count < 1)
                    return null;

                string path = items.Item(0).Path;
                return System.IO.File.Exists(path) || System.IO.Directory.Exists(path) ? path : null;
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shell is not null)
                Marshal.FinalReleaseComObject(shell);
        }

        return null;
    }

    // FindWindowSW: SWC_DESKTOP — найти shell-вид рабочего стола; SWFO_NEEDDISPATCH — вернуть IDispatch вместо HWND
    private const int SWC_DESKTOP = 0x08;
    private const int SWFO_NEEDDISPATCH = 0x01;

    /// <summary>Путь выбранного значка на Рабочем столе. Стол отсутствует в Shell.Windows(),
    /// поэтому достаём его shell-вид напрямую через IShellWindows.FindWindowSW(SWC_DESKTOP).</summary>
    public static string? GetDesktopSelectedFile()
    {
        var shellWindowsType = Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"));
        if (shellWindowsType is null)
            return null;

        object? shellWindows = null;
        object? desktop = null;
        try
        {
            shellWindows = Activator.CreateInstance(shellWindowsType);
            object loc = null!, locRoot = null!;
            ((IShellWindows)shellWindows!).FindWindowSW(
                ref loc, ref locRoot, SWC_DESKTOP, out _, SWFO_NEEDDISPATCH, out desktop);
            if (desktop is null)
                return null;

            dynamic items = ((dynamic)desktop).Document.SelectedItems();
            if (items.Count < 1)
                return null;

            string path = items.Item(0).Path;
            return System.IO.File.Exists(path) || System.IO.Directory.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (desktop is not null)
                Marshal.FinalReleaseComObject(desktop);
            if (shellWindows is not null)
                Marshal.FinalReleaseComObject(shellWindows);
        }
    }

    /// <summary>Все пути открытых сейчас окон Проводника.</summary>
    public static List<string> GetOpenFolders()
    {
        var result = new List<string>();
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
            return result;

        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            dynamic windows = ((dynamic)shell!).Windows();
            int count = windows.Count;

            for (var i = 0; i < count; i++)
            {
                dynamic? window = windows.Item(i);
                if (window is null)
                    continue;

                try
                {
                    string path = window.Document.Folder.Self.Path;
                    if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                        result.Add(path);
                }
                catch
                {
                    // окно без файловой папки (Панель управления и т.п.) — пропускаем
                }
            }
        }
        catch
        {
            // Shell недоступен
        }
        finally
        {
            if (shell is not null)
                Marshal.FinalReleaseComObject(shell);
        }

        return result;
    }
}

/// <summary>Минимальный IShellWindows — нужен только FindWindowSW. Методы до него — заглушки,
/// держат порядок vtable (IDispatch + собственные методы интерфейса), их не вызываем.</summary>
[ComImport]
[Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellWindows
{
    // IDispatch (4 слота)
    void GetTypeInfoCount();
    void GetTypeInfo();
    void GetIDsOfNames();
    void Invoke();
    // IShellWindows до FindWindowSW
    void GetCount();
    void Item();
    void NewEnum();
    void Register();
    void RegisterPending();
    void Revoke();
    void OnNavigate();
    void OnActivated();

    [PreserveSig]
    int FindWindowSW(
        ref object pvarLoc,
        ref object pvarLocRoot,
        int swClass,
        out int pHWND,
        int swfwOptions,
        [MarshalAs(UnmanagedType.IDispatch)] out object ppDispOut);
}
