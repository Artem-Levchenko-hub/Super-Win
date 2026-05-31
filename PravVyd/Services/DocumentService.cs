using System.IO;
using System.Text;
using PravVyd.Documents;
using PravVyd.Infrastructure;

namespace PravVyd.Services;

/// <summary>Полный конвейер: снять выделение → разобрать → записать файл → положить в буфер.
/// Единственная "толстая" операция для UI (R-01).</summary>
public sealed class DocumentService
{
    private readonly SelectionCapture _capture;
    private readonly OutputPathResolver _paths;
    private readonly ClipboardService _clipboard;
    private readonly IReadOnlyDictionary<DocFormat, IDocumentWriter> _writers;

    public DocumentService(
        SelectionCapture capture,
        OutputPathResolver paths,
        ClipboardService clipboard,
        IEnumerable<IDocumentWriter> writers)
    {
        _capture = capture;
        _paths = paths;
        _clipboard = clipboard;
        _writers = writers.ToDictionary(w => w.Format);
    }

    public async Task<DocumentResult> CreateFromSelectionAsync(DocFormat format)
    {
        var captured = await _capture.CaptureRichAsync();
        if (captured is null || (string.IsNullOrWhiteSpace(captured.Text) && string.IsNullOrWhiteSpace(captured.Html)))
            return DocumentResult.NoSelection();

        try
        {
            var writer = _writers[format];

            // .md + есть HTML в буфере → переносим оформление выделения в Markdown «как скопировал»
            if (format == DocFormat.Markdown && !string.IsNullOrWhiteSpace(captured.Html))
            {
                var markdown = HtmlToMarkdown.Convert(captured.Html);
                if (!string.IsNullOrWhiteSpace(markdown))
                {
                    var mdPath = _paths.Resolve(writer.Extension, TitleFromMarkdown(markdown));
                    File.WriteAllText(mdPath, markdown + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    _clipboard.SetFile(mdPath);
                    return DocumentResult.Ok(mdPath);
                }
            }

            var model = TextParser.Parse(captured.Text ?? string.Empty);
            var path = _paths.Resolve(writer.Extension, model.Title);
            writer.Write(model, path);
            _clipboard.SetFile(path);
            return DocumentResult.Ok(path);
        }
        catch (Exception ex)
        {
            return DocumentResult.Failure(ex.Message);
        }
    }

    private static string TitleFromMarkdown(string markdown)
    {
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim().TrimStart('#', '>', '-', '*', ' ').Trim();
            if (line.Length == 0)
                continue;

            return line.Length <= 60 ? line : line[..60].TrimEnd() + "…";
        }

        return "Документ";
    }
}
