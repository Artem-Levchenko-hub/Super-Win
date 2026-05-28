using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace PravVyd.Infrastructure;

/// <summary>Ctrl+Shift+T — открыть заново последнюю закрытую папку Проводника.
/// Опрашиваем открытые окна Проводника; исчезнувшие пути складываем в историю.</summary>
public sealed class RecentlyClosedService : IDisposable
{
    private const int MaxHistory = 25;

    private readonly DispatcherTimer _timer;
    private readonly List<string> _closed = new(); // последний закрытый — в конце
    private readonly Action<string>? _notify;
    private HashSet<string> _previous;

    public RecentlyClosedService(GlobalHotkeys hotkeys, Action<string>? notify = null)
    {
        _notify = notify;
        _previous = new HashSet<string>(ExplorerSelection.GetOpenFolders(), StringComparer.OrdinalIgnoreCase);

        hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, NativeMethods.VK_T, Reopen);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _timer.Tick += Poll;
        _timer.Start();
    }

    private void Poll(object? sender, EventArgs e)
    {
        var current = new HashSet<string>(ExplorerSelection.GetOpenFolders(), StringComparer.OrdinalIgnoreCase);

        foreach (var path in _previous)
        {
            if (current.Contains(path))
                continue;

            _closed.Remove(path); // не плодим дубли
            _closed.Add(path);
            if (_closed.Count > MaxHistory)
                _closed.RemoveAt(0);
        }

        _previous = current;
    }

    private void Reopen()
    {
        for (var i = _closed.Count - 1; i >= 0; i--)
        {
            var path = _closed[i];
            _closed.RemoveAt(i);

            if (!Directory.Exists(path))
                continue;

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                _notify?.Invoke("Открыто заново: " + Path.GetFileName(path));
            }
            catch
            {
                _notify?.Invoke("Не удалось открыть папку");
            }

            return;
        }

        _notify?.Invoke("Нет недавно закрытых папок");
    }

    public void Dispose() => _timer.Stop();
}
