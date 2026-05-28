namespace PravVyd.Documents;

public enum BlockKind
{
    Heading,
    Paragraph,
    Bullet,
    Numbered,
    Code,
}

/// <summary>Один структурный блок. Level — для Heading (1..6), Number — для Numbered.</summary>
public sealed record Block(BlockKind Kind, string Text, int Level = 0, int Number = 0);

/// <summary>Промежуточное представление. Разбираем текст один раз, рендерим в MD/PDF/DOCX (DRY).</summary>
public sealed class DocumentModel
{
    public required string Title { get; init; }
    public required IReadOnlyList<Block> Blocks { get; init; }
}
