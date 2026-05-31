namespace PravVyd.Documents;

/// <summary>HTML из буфера (CF_HTML) → Markdown. Сохраняет жирный/курсив/заголовки/списки/ссылки,
/// чтобы выделенный текст «упал» в .md ровно с тем оформлением, что было скопировано.</summary>
public static class HtmlToMarkdown
{
    private static readonly ReverseMarkdown.Converter Converter = new(new ReverseMarkdown.Config
    {
        UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
    });

    public static string Convert(string cfHtml)
    {
        var fragment = ExtractFragment(cfHtml);
        return string.IsNullOrWhiteSpace(fragment) ? string.Empty : Converter.Convert(fragment).Trim();
    }

    /// <summary>CF_HTML несёт служебный заголовок и маркеры фрагмента — оставляем чистый HTML.</summary>
    private static string ExtractFragment(string cfHtml)
    {
        if (string.IsNullOrEmpty(cfHtml))
            return string.Empty;

        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        var start = cfHtml.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var end = cfHtml.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);
        if (start >= 0 && end > start)
            return cfHtml.Substring(start + startMarker.Length, end - start - startMarker.Length);

        // нет маркеров — срезаем CF_HTML-заголовок (строки "Ключ:значение" до первого тега)
        var firstTag = cfHtml.IndexOf('<');
        return firstTag >= 0 ? cfHtml[firstTag..] : cfHtml;
    }
}
