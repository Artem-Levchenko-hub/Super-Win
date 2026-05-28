namespace PravVyd.Infrastructure;

/// <summary>Ctrl+Alt+R — перекладывает выделенный текст в правильную раскладку и вставляет на место.</summary>
public sealed class LayoutFixService
{
    private readonly SelectionCapture _capture;
    private readonly ClipboardService _clipboard;
    private readonly Action<string>? _notify;
    private bool _busy;

    public LayoutFixService(GlobalHotkeys hotkeys, SelectionCapture capture, ClipboardService clipboard, Action<string>? notify = null)
    {
        _capture = capture;
        _clipboard = clipboard;
        _notify = notify;
        hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_R, () => _ = FixAsync());
    }

    private async Task FixAsync()
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            var text = await _capture.CaptureAsync();
            if (string.IsNullOrEmpty(text))
            {
                _notify?.Invoke("Выделите текст");
                return;
            }

            var converted = LayoutConverter.Convert(text);
            if (converted == text)
                return;

            _clipboard.SetText(converted);
            await Task.Delay(40);
            InputSimulator.SendCtrlV();
        }
        catch (Exception ex)
        {
            _notify?.Invoke("Раскладка: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }
}
