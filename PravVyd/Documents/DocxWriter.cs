using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PravVyd.Documents;

/// <summary>Рендер в .docx через OpenXML SDK. Списки — литералами ("•", "N."), без numbering-парта: осознанный размен сложности на надёжность для v0.1.</summary>
public sealed class DocxWriter : IDocumentWriter
{
    public DocFormat Format => DocFormat.Docx;

    public string Extension => "docx";

    public void Write(DocumentModel model, string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var body = new Body();

        foreach (var block in model.Blocks)
            body.Append(ToParagraph(block));

        main.Document = new Document(body);
        main.Document.Save();
    }

    private static Paragraph ToParagraph(Block block) => block.Kind switch
    {
        BlockKind.Heading => TextParagraph(block.Text, bold: true, halfPoints: HeadingHalfPoints(block.Level), spacingBefore: 200),
        BlockKind.Bullet => TextParagraph("•  " + block.Text),
        BlockKind.Numbered => TextParagraph($"{block.Number}.  " + block.Text),
        BlockKind.Code => CodeParagraph(block.Text),
        _ => TextParagraph(block.Text),
    };

    private static int HeadingHalfPoints(int level) => level switch { 1 => 40, 2 => 32, 3 => 28, _ => 24 };

    private static Paragraph TextParagraph(string text, bool bold = false, int halfPoints = 22, int spacingBefore = 0)
    {
        var runProps = new RunProperties(new FontSize { Val = halfPoints.ToString() });
        if (bold)
            runProps.Append(new Bold());

        var run = new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var paragraph = new Paragraph(run);

        if (spacingBefore > 0)
            paragraph.ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { Before = spacingBefore.ToString() });

        return paragraph;
    }

    private static Paragraph CodeParagraph(string text)
    {
        var runProps = new RunProperties(
            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
            new FontSize { Val = "20" });

        var run = new Run(runProps);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.Append(new Break());

            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        return new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties(
                new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }),
        };
    }
}
