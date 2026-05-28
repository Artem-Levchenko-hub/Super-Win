using System.Windows;

namespace PravVyd.Infrastructure;

/// <summary>Снимает выделенный в активном окне текст: шлёт Ctrl+C и ждёт смены буфера обмена.</summary>
public sealed class SelectionCapture
{
    public async Task<string?> CaptureAsync()
    {
        var before = NativeMethods.GetClipboardSequenceNumber();
        InputSimulator.SendCtrlC();

        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(20);
            if (NativeMethods.GetClipboardSequenceNumber() != before)
                break;
        }

        return ReadText();
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
}
