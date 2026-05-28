using System.Text;
using System.Text.RegularExpressions;

namespace PravVyd.Documents;

/// <summary>Превращает произвольный выделенный текст в структурированную модель ("умный Markdown").</summary>
public static partial class TextParser
{
    public static DocumentModel Parse(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<Block>();
        var paragraph = new StringBuilder();
        var code = new StringBuilder();
        var inCode = false;

        void FlushParagraph()
        {
            if (paragraph.Length == 0)
                return;

            blocks.Add(new Block(BlockKind.Paragraph, paragraph.ToString().Trim()));
            paragraph.Clear();
        }

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    blocks.Add(new Block(BlockKind.Code, code.ToString().TrimEnd('\n')));
                    code.Clear();
                    inCode = false;
                }
                else
                {
                    FlushParagraph();
                    inCode = true;
                }

                continue;
            }

            if (inCode)
            {
                code.Append(line).Append('\n');
                continue;
            }

            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            var heading = HeadingRegex().Match(trimmed);
            if (heading.Success)
            {
                FlushParagraph();
                blocks.Add(new Block(BlockKind.Heading, heading.Groups[2].Value.Trim(), Level: heading.Groups[1].Value.Length));
                continue;
            }

            var bullet = BulletRegex().Match(trimmed);
            if (bullet.Success)
            {
                FlushParagraph();
                blocks.Add(new Block(BlockKind.Bullet, bullet.Groups[1].Value.Trim()));
                continue;
            }

            var numbered = NumberedRegex().Match(trimmed);
            if (numbered.Success)
            {
                FlushParagraph();
                blocks.Add(new Block(BlockKind.Numbered, numbered.Groups[2].Value.Trim(), Number: int.Parse(numbered.Groups[1].Value)));
                continue;
            }

            if (paragraph.Length > 0)
                paragraph.Append(' ');

            paragraph.Append(trimmed);
        }

        FlushParagraph();
        if (inCode && code.Length > 0)
            blocks.Add(new Block(BlockKind.Code, code.ToString().TrimEnd('\n')));

        return new DocumentModel
        {
            Title = DeriveTitle(blocks),
            Blocks = blocks,
        };
    }

    private static string DeriveTitle(IReadOnlyList<Block> blocks)
    {
        var heading = blocks.FirstOrDefault(b => b.Kind == BlockKind.Heading);
        if (heading is not null)
            return Shorten(heading.Text);

        var paragraph = blocks.FirstOrDefault(b => b.Kind == BlockKind.Paragraph);
        if (paragraph is not null)
            return Shorten(paragraph.Text);

        return "Документ";
    }

    private static string Shorten(string text)
    {
        text = text.Trim();
        return text.Length <= 60 ? text : text[..60].TrimEnd() + "…";
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[-*•]\s+(.+)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^(\d+)[.)]\s+(.+)$")]
    private static partial Regex NumberedRegex();
}
