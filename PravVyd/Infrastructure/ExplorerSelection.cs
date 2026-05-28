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
                return System.IO.File.Exists(path) ? path : null;
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
}
