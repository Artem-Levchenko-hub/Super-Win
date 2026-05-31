using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using PravVyd.Infrastructure;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

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
        ".txt", ".log", ".json", ".xml", ".csv", ".ini", ".yml", ".yaml",
        ".cs", ".js", ".ts", ".css", ".py", ".c", ".cpp", ".h", ".java",
        ".go", ".rs", ".sql", ".sh", ".bat", ".ps1", ".config",
    };

    // html/htm/svg рендерим как страницу через WebView2, а не как исходник
    private static readonly string[] WebExtensions = { ".html", ".htm", ".svg" };

    private bool _closing;
    private WebView2? _web;

    public PreviewWindow(string path)
    {
        InitializeComponent();
        TitleText.Text = Path.GetFileName(path);
        Loaded += async (_, _) =>
        {
            AnchorGrowOriginToCursor();
            PlayEnterAnimation();
            await LoadAsync(path);
        };
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

            if (Array.IndexOf(WebExtensions, ext) >= 0)
                await ShowWebAsync(path);
            else if (Array.IndexOf(ImageExtensions, ext) >= 0)
                await ShowImageAsync(path);
            else if (ext == ".pdf")
                await ShowPdfAsync(path);
            else if (ext == ".md")
                await ShowMarkdownAsync(path);
            else if (ext == ".docx")
                await ShowDocxAsync(path);
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

    private async Task ShowImageAsync(string path)
    {
        // декод в фоне + уменьшение больших фото прямо при декодировании — в разы быстрее, не блокирует анимацию
        var bitmap = await Task.Run(() =>
        {
            var decodeWidth = 0;
            try
            {
                using var probe = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (decoder.Frames[0].PixelWidth > 1600)
                    decodeWidth = 1600;
            }
            catch
            {
                // заголовок не прочитали — декодируем как есть
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0)
                bmp.DecodePixelWidth = decodeWidth;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        });

        Host.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform });
    }

    private async Task ShowWebAsync(string path)
    {
        var core = await CreateWebAsync(transparent: false);
        // file:// → локальные ресурсы (css/картинки рядом) подхватываются; svg браузер рисует сам
        core?.Navigate(new Uri(path).AbsoluteUri);
    }

    /// <summary>Готовый HTML (из .md/.docx) на прозрачном фоне — текст ложится прямо на матовое стекло.</summary>
    private async Task ShowRenderedHtmlAsync(string bodyHtml, string baseHref)
    {
        var core = await CreateWebAsync(transparent: true);
        core?.NavigateToString(WrapHtml(bodyHtml, baseHref));
    }

    private async Task<Microsoft.Web.WebView2.Core.CoreWebView2?> CreateWebAsync(bool transparent)
    {
        _web = new WebView2();
        if (transparent)
            _web.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        Host.Children.Add(_web);

        await _web.EnsureCoreWebView2Async();
        if (_closing || _web.CoreWebView2 is null)
            return null;

        _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        return _web.CoreWebView2;
    }

    private static string WrapHtml(string bodyHtml, string baseHref)
    {
        var baseTag = string.IsNullOrEmpty(baseHref) ? string.Empty : $"<base href=\"{baseHref}\">";
        return $"<!doctype html><html><head><meta charset=\"utf-8\">{baseTag}<style>{DocCss}</style></head><body>{bodyHtml}</body></html>";
    }

    private const string DocCss = """
        :root{color-scheme:dark}
        html,body{margin:0}
        body{background:rgba(16,16,22,.72);color:#ECECF1;
             font-family:'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif;
             font-size:15px;line-height:1.65;padding:10px 16px 22px;-webkit-font-smoothing:antialiased;word-wrap:break-word}
        h1,h2,h3,h4,h5,h6{color:#fff;font-weight:600;line-height:1.25;margin:1.05em 0 .5em}
        h1{font-size:1.9em}h2{font-size:1.55em}h3{font-size:1.3em}h4{font-size:1.12em}
        p{margin:.55em 0}
        a{color:#7DB6FF;text-decoration:none}
        code{font-family:'Cascadia Code',Consolas,monospace;background:#ffffff14;padding:.12em .35em;border-radius:5px;font-size:.9em}
        pre{background:#00000045;padding:12px 14px;border-radius:10px;overflow:auto}
        pre code{background:transparent;padding:0}
        blockquote{margin:.6em 0;padding:.15em 0 .15em 14px;border-left:3px solid #ffffff33;color:#C9C9D6}
        img{max-width:100%;height:auto;border-radius:8px}
        table{border-collapse:collapse;margin:.6em 0;font-size:.96em}
        th,td{border:1px solid #ffffff24;padding:6px 10px;text-align:left}
        th{background:#ffffff10}
        hr{border:none;border-top:1px solid #ffffff1f;margin:1.2em 0}
        ul,ol{padding-left:1.4em;margin:.5em 0}
        li{margin:.2em 0}
        ::selection{background:#5A8DEE55}
        ::-webkit-scrollbar{width:10px;height:10px}
        ::-webkit-scrollbar-thumb{background:#ffffff2a;border-radius:8px}
        ::-webkit-scrollbar-track{background:transparent}
        """;

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

    private async Task ShowMarkdownAsync(string path)
    {
        var html = await Task.Run(() =>
        {
            var md = ReadTextCapped(path, 512 * 1024);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            return Markdown.ToHtml(md, pipeline);
        });

        // base href = папка файла → относительные картинки/ссылки в .md подхватываются
        var dir = Path.GetDirectoryName(path);
        var baseHref = string.IsNullOrEmpty(dir) ? string.Empty : new Uri(dir + Path.DirectorySeparatorChar).AbsoluteUri;
        await ShowRenderedHtmlAsync(html, baseHref);
    }

    private async Task ShowDocxAsync(string path)
    {
        var html = await Task.Run(() => DocxToHtml(path));
        if (string.IsNullOrWhiteSpace(html))
        {
            ShowMessage("Пустой документ или текст не найден");
            return;
        }

        await ShowRenderedHtmlAsync(html, baseHref: string.Empty);
    }

    private static string ReadTextCapped(string path, int maxChars)
    {
        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[maxChars];
        var read = reader.Read(buffer, 0, maxChars);
        return new string(buffer, 0, read);
    }

    // --- docx → HTML: заголовки, жирный/курсив/подчёркивание, размер, шрифт, цвет, таблицы ---

    private static string DocxToHtml(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var el in body.ChildElements)
        {
            if (sb.Length > 400_000)
            {
                sb.Append("<p><i>… документ обрезан для превью</i></p>");
                break;
            }

            switch (el)
            {
                case Paragraph p: AppendParagraph(sb, p); break;
                case Table t: AppendTable(sb, t); break;
            }
        }

        return sb.ToString();
    }

    private static void AppendParagraph(StringBuilder sb, Paragraph p)
    {
        var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        var level = HeadingLevel(styleId);
        var tag = level > 0 ? "h" + level : "p";

        var inner = new StringBuilder();
        foreach (var run in p.Descendants<Run>())
            AppendRun(inner, run);

        var bullet = level == 0 && p.ParagraphProperties?.NumberingProperties is not null ? "•&nbsp;&nbsp;" : string.Empty;

        sb.Append('<').Append(tag).Append('>');
        sb.Append(bullet);
        sb.Append(inner.Length > 0 ? inner.ToString() : "&nbsp;");
        sb.Append("</").Append(tag).Append('>');
    }

    private static void AppendRun(StringBuilder sb, Run run)
    {
        var rp = run.RunProperties;
        var styles = new List<string>();
        if (rp is not null)
        {
            if (IsOn(rp.Bold)) styles.Add("font-weight:600");
            if (IsOn(rp.Italic)) styles.Add("font-style:italic");

            var deco = string.Empty;
            if (rp.Underline is not null) deco += "underline ";
            if (IsOn(rp.Strike)) deco += "line-through";
            if (deco.Trim().Length > 0) styles.Add("text-decoration:" + deco.Trim());

            if (rp.FontSize?.Val?.Value is { } fs && double.TryParse(fs, out var halfPt))
                styles.Add("font-size:" + (halfPt / 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "pt");

            var font = rp.RunFonts?.Ascii?.Value;
            if (!string.IsNullOrEmpty(font))
                styles.Add($"font-family:'{font}','Segoe UI',sans-serif");

            var color = rp.Color?.Val?.Value;
            if (!string.IsNullOrEmpty(color) && color != "auto" && IsHexColor(color))
                styles.Add("color:#" + color);
        }

        var attr = styles.Count > 0 ? $" style=\"{string.Join(';', styles)}\"" : string.Empty;
        sb.Append("<span").Append(attr).Append('>');
        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case Text t: sb.Append(Escape(t.Text)); break;
                case Break: sb.Append("<br>"); break;
                case TabChar: sb.Append("&emsp;"); break;
            }
        }

        sb.Append("</span>");
    }

    private static void AppendTable(StringBuilder sb, Table table)
    {
        sb.Append("<table>");
        foreach (var row in table.Elements<TableRow>())
        {
            sb.Append("<tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                sb.Append("<td>");
                foreach (var p in cell.Elements<Paragraph>())
                    AppendParagraph(sb, p);
                sb.Append("</td>");
            }

            sb.Append("</tr>");
        }

        sb.Append("</table>");
    }

    private static int HeadingLevel(string styleId)
    {
        if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string(styleId.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) && n is >= 1 and <= 6 ? n : 2;
        }

        return styleId.Equals("Title", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static bool IsOn(OnOffType? toggle) => toggle is not null && (toggle.Val is null || toggle.Val.Value);

    private static bool IsHexColor(string s) =>
        s.Length == 6 && s.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private async Task ShowPdfAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var pdf = await PdfDocument.LoadFromFileAsync(file);

        // подложку добавляем сразу, страницы дорисовываем по мере рендера — первая видна почти мгновенно
        var panel = new StackPanel();
        Host.Children.Add(new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        var pageCount = Math.Min(pdf.PageCount, 50u);
        var options = new PdfPageRenderOptions { DestinationWidth = 1000 };

        for (uint i = 0; i < pageCount; i++)
        {
            if (_closing)
                break; // окно уже закрывают — не тратим время на остальные страницы

            using var page = pdf.GetPage(i);
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream, options);

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // матовое стекло (acrylic) + скруглённые углы. На ОС без поддержки вызовы тихо игнорируются.
        var hwnd = new WindowInteropHelper(this).Handle;
        int on = 1, round = NativeMethods.DWMWCP_ROUND, acrylic = NativeMethods.DWMSBT_TRANSIENTWINDOW;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref acrylic, sizeof(int));
    }

    // --- Анимация «вырастания» из ярлыка: scale + opacity от точки, где стоит курсор ---

    private void AnchorGrowOriginToCursor()
    {
        if (!NativeMethods.GetCursorPos(out var p))
            return;

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        // курсор — в физических пикселях; переводим в DIP, как Left/Top/ActualWidth
        var cursor = source.CompositionTarget.TransformFromDevice.Transform(new Point(p.x, p.y));
        var originX = Math.Clamp((cursor.X - Left) / ActualWidth, 0, 1);
        var originY = Math.Clamp((cursor.Y - Top) / ActualHeight, 0, 1);
        Root.RenderTransformOrigin = new Point(originX, originY);
    }

    private void PlayEnterAnimation()
    {
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
        var grow = new DoubleAnimation(0.5, 1.0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease };
        var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));

        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        Root.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closing)
        {
            base.OnClosing(e);
            return;
        }

        // схлопывание обратно к ярлыку, затем реальное закрытие (быстрее входа — R: exit-faster-than-enter)
        e.Cancel = true;
        _closing = true;

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var shrink = new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        var fade = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => Close();

        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        Root.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _web?.Dispose(); // освобождаем процесс движка Edge
        _web = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape || e.Key == Key.Space)
            Close();
    }
}
