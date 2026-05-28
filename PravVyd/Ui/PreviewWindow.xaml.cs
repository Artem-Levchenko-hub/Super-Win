using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PravVyd.Ui;

/// <summary>Мгновенное превью файла: текст, картинка, PDF (рендер WinRT), видео (MediaElement).</summary>
public partial class PreviewWindow : Window
{
    private static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".ico" };

    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv" };

    private static readonly string[] TextExtensions =
    {
        ".txt", ".md", ".log", ".json", ".xml", ".csv", ".ini", ".yml", ".yaml",
        ".cs", ".js", ".ts", ".html", ".css", ".py", ".c", ".cpp", ".h", ".java",
        ".go", ".rs", ".sql", ".sh", ".bat", ".ps1", ".config",
    };

    public PreviewWindow(string path)
    {
        InitializeComponent();
        TitleText.Text = Path.GetFileName(path);
        Loaded += async (_, _) => await LoadAsync(path);
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (Array.IndexOf(ImageExtensions, ext) >= 0)
                ShowImage(path);
            else if (ext == ".pdf")
                await ShowPdfAsync(path);
            else if (Array.IndexOf(VideoExtensions, ext) >= 0)
                ShowVideo(path);
            else if (Array.IndexOf(TextExtensions, ext) >= 0)
                ShowText(path);
            else
                ShowMessage("Нет превью для этого типа файла");
        }
        catch (Exception ex)
        {
            ShowMessage("Ошибка превью: " + ex.Message);
        }
    }

    private void ShowImage(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        Host.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform });
    }

    private void ShowVideo(string path)
    {
        var media = new MediaElement
        {
            Source = new Uri(path),
            LoadedBehavior = MediaState.Play,
            Stretch = Stretch.Uniform,
        };
        media.MediaEnded += (_, _) =>
        {
            media.Position = TimeSpan.Zero;
            media.Play();
        };

        Host.Children.Add(media);
    }

    private void ShowText(string path)
    {
        const int max = 1024 * 1024;
        string content;
        using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
        {
            var buffer = new char[max];
            var read = reader.Read(buffer, 0, max);
            content = new string(buffer, 0, read);
            if (!reader.EndOfStream)
                content += "\n\n… (файл обрезан для превью)";
        }

        Host.Children.Add(new TextBox
        {
            Text = content,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
    }

    private async Task ShowPdfAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var pdf = await PdfDocument.LoadFromFileAsync(file);

        var panel = new StackPanel();
        var pageCount = Math.Min(pdf.PageCount, 50u);

        for (uint i = 0; i < pageCount; i++)
        {
            using var page = pdf.GetPage(i);
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream);

            stream.Seek(0);
            var bytes = new byte[(int)stream.Size];
            var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            panel.Children.Add(new Image
            {
                Source = bitmap,
                Margin = new Thickness(0, 0, 0, 10),
                Stretch = Stretch.Uniform,
            });
        }

        Host.Children.Add(new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
    }

    private void ShowMessage(string message)
    {
        Host.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontSize = 15,
            FontFamily = new FontFamily("Segoe UI"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape || e.Key == Key.Space)
            Close();
    }
}
