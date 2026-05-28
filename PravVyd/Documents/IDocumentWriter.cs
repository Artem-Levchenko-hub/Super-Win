namespace PravVyd.Documents;

/// <summary>Рендер модели в конкретный формат файла. Каждая реализация прячет свою библиотеку (R-02).</summary>
public interface IDocumentWriter
{
    DocFormat Format { get; }
    string Extension { get; }
    void Write(DocumentModel model, string path);
}
