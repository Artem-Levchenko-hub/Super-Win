using System.Windows;

namespace PravVyd.Infrastructure;

/// <summary>Снимает выделение в активном окне: шлёт Ctrl+C и ждёт смены буфера обмена.
/// CaptureAsync — обычный текст; CaptureRichAsync — текст + HTML (чтобы сохранить оформление).</summary>
public sealed class SelectionCapture
{
    public async Task<string?> CaptureAsync()
    {
        await TriggerCopyAsync();
        return ReadText();
    }

    public async Task<CapturedContent?> CaptureRichAsync()
    {
        await TriggerCopyAsync();
        return ReadRich();
    }

    private static async Task TriggerCopyAsync()
    {
        var before = NativeMethods.GetClipboardSequenceNumber();
        InputSimulator.SendCtrlC();

        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(20);
            if (NativeMethods.GetClipboardSequenceNumber() != before)
                break;
        }
    }

    private static string? ReadText()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch
            {
                Thread.Sleep(40);
            }
        }

        return null;
    }

    private static CapturedContent? ReadRich()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                var html = Clipboard.ContainsData(DataFormats.Html) ? Clipboard.GetData(DataFormats.Html) as string : null;
                return text is null && html is null ? null : new CapturedContent(text, html);
            }
            catch
            {
                Thread.Sleep(40);
            }
        }

        return null;
    }
}

/// <summary>Снятое выделение: обычный текст и (если доступно) HTML-представление из буфера.</summary>
public sealed record CapturedContent(string? Text, string? Html);
