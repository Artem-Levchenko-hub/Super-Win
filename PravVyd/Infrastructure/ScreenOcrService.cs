using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows;
using PravVyd.Ui;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PravVyd.Infrastructure;

/// <summary>Хоткей Ctrl+Alt+O → выделить область экрана → OCR (Windows.Media.Ocr) → текст в буфер.
/// Решает копирование текста из видео, скриншотов, PDF-картинок.</summary>
public sealed class ScreenOcrService
{
    private readonly ClipboardService _clipboard;
    private readonly Action<string>? _notify;
    private bool _busy;

    public ScreenOcrService(GlobalHotkeys hotkeys, ClipboardService clipboard, Action<string>? notify = null)
    {
        _clipboard = clipboard;
        _notify = notify;
        hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_O, () => _ = RunAsync());
    }

    private async Task RunAsync()
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            var region = await new ScreenCaptureOverlay().SelectAsync();
            if (region is null)
                return;

            // дать оверлею исчезнуть, чтобы не попасть в снимок
            await Task.Delay(120);

            using var bitmap = Capture(region.Value);
            var text = await RecognizeAsync(bitmap);

            if (string.IsNullOrWhiteSpace(text))
            {
                _notify?.Invoke("Текст не распознан");
                return;
            }

            _clipboard.SetText(text);
            _notify?.Invoke("✓ Текст в буфере — Ctrl+V");
        }
        catch (Exception ex)
        {
            _notify?.Invoke("OCR ошибка: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private static Bitmap Capture(Int32Rect region)
    {
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.X, region.Y, 0, 0,
            new System.Drawing.Size(region.Width, region.Height), CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static async Task<string> RecognizeAsync(Bitmap bitmap)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            throw new InvalidOperationException("нет языков OCR — установите языковой пакет Windows");

        using var stream = new InMemoryRandomAccessStream();
        using (var memory = new MemoryStream())
        {
            bitmap.Save(memory, ImageFormat.Png);
            var writer = new DataWriter(stream);
            writer.WriteBytes(memory.ToArray());
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(software);

        var builder = new StringBuilder();
        foreach (var line in result.Lines)
            builder.AppendLine(line.Text);

        return builder.ToString().TrimEnd();
    }
}
