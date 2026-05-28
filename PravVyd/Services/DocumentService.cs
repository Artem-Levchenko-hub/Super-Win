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
        var text = await _capture.CaptureAsync();
        if (string.IsNullOrWhiteSpace(text))
            return DocumentResult.NoSelection();

        try
        {
            var model = TextParser.Parse(text);
            var writer = _writers[format];
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
}
