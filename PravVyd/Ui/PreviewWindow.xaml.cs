using System.IO;
using System.Linq;
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
            if (Directory.Exists(path))
            {
                ShowFolder(path);
                return;
            }

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

    private void ShowFolder(string path)
    {
        DirectoryInfo[] dirs;
        FileInfo[] files;
        try
        {
            var info = new DirectoryInfo(path);
            dirs = info.GetDirectories();
            files = info.GetFiles();
        }
        catch (Exception ex)
        {
            ShowMessage("Нет доступа к папке: " + ex.Message);
            return;
        }

        var panel = new StackPanel { Margin = new Thickness(4) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Папок: {dirs.Length}    Файлов: {files.Length}",
            Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Margin = new Thickness(2, 2, 2, 8),
        });

        const int max = 500;
        var shown = 0;

        foreach (var d in dirs.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (shown++ >= max) break;
            panel.Children.Add(MakeRow("\U0001F4C1", d.Name, null));
        }

        foreach (var f in files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (shown++ >= max) break;
            panel.Children.Add(MakeRow("\U0001F4C4", f.Name, FormatSize(f.Length)));
        }

        if (dirs.Length + files.Length > max)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "… список обрезан для превью",
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Margin = new Thickness(2, 8, 2, 2),
            });
        }

        Host.Children.Add(new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });
    }

    private static FrameworkElement MakeRow(string glyph, string name, string? size)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new TextBlock
        {
            Text = glyph,
            FontSize = 14,
            Margin = new Thickness(2, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var label = new TextBlock
        {
            Text = name,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (size is not null)
        {
            var sizeText = new TextBlock
            {
                Text = size,
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Margin = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(sizeText, 2);
            grid.Children.Add(sizeText);
        }

        return grid;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[0]}" : $"{size:0.#} {units[unit]}";
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
