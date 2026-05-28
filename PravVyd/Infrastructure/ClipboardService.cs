using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;

namespace PravVyd.Infrastructure;

/// <summary>Кладёт готовый файл в буфер как CF_HDROP — тогда Ctrl+V в любом приложении вставит сам файл.</summary>
public sealed class ClipboardService
{
    public void SetFile(string path)
    {
        var files = new StringCollection { path };

        // буфер может быть временно занят другим процессом — повторяем
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetFileDropList(files);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(60);
            }
        }
    }

    /// <summary>Кладёт распознанный текст (OCR) в буфер как обычный текст.</summary>
    public void SetText(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(60);
            }
        }
    }
}
