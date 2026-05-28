using System.IO;
using System.Text;

namespace PravVyd.Documents;

/// <summary>Сериализует модель в нормализованный Markdown.</summary>
public sealed class MarkdownWriter : IDocumentWriter
{
    public DocFormat Format => DocFormat.Markdown;

    public string Extension => "md";

    public void Write(DocumentModel model, string path)
    {
        var sb = new StringBuilder();

        foreach (var block in model.Blocks)
        {
            switch (block.Kind)
            {
                case BlockKind.Heading:
                    sb.Append('#', block.Level).Append(' ').AppendLine(block.Text).AppendLine();
                    break;
                case BlockKind.Bullet:
                    sb.Append("- ").AppendLine(block.Text);
                    break;
                case BlockKind.Numbered:
                    sb.Append(block.Number).Append(". ").AppendLine(block.Text);
                    break;
                case BlockKind.Code:
                    sb.AppendLine("```").AppendLine(block.Text).AppendLine("```").AppendLine();
                    break;
                default:
                    sb.AppendLine(block.Text).AppendLine();
                    break;
            }
        }

        File.WriteAllText(path, sb.ToString().TrimEnd() + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
