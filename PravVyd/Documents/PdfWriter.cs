using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PravVyd.Documents;

/// <summary>Рендер в PDF через QuestPDF. Прячет fluent-API библиотеки за IDocumentWriter (R-02).</summary>
public sealed class PdfWriter : IDocumentWriter
{
    static PdfWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocFormat Format => DocFormat.Pdf;

    public string Extension => "pdf";

    public void Write(DocumentModel model, string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Segoe UI"));

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    foreach (var block in model.Blocks)
                        Render(column, block);
                });

                page.Footer().AlignCenter().Text(x => x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium));
            });
        }).GeneratePdf(path);
    }

    private static void Render(ColumnDescriptor column, Block block)
    {
        switch (block.Kind)
        {
            case BlockKind.Heading:
                var size = block.Level switch { 1 => 20f, 2 => 16f, 3 => 14f, _ => 12f };
                column.Item().PaddingTop(6).Text(block.Text).FontSize(size).SemiBold();
                break;
            case BlockKind.Bullet:
                column.Item().Row(row =>
                {
                    row.ConstantItem(16).Text("•");
                    row.RelativeItem().Text(block.Text);
                });
                break;
            case BlockKind.Numbered:
                column.Item().Row(row =>
                {
                    row.ConstantItem(22).Text($"{block.Number}.");
                    row.RelativeItem().Text(block.Text);
                });
                break;
            case BlockKind.Code:
                column.Item().Background(Colors.Grey.Lighten3).Padding(8)
                    .Text(block.Text).FontFamily("Consolas").FontSize(10);
                break;
            default:
                column.Item().Text(block.Text);
                break;
        }
    }
}
