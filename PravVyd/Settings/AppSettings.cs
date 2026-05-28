namespace PravVyd.Settings;

public enum OutputLocation
{
    Temp,
    Documents,
    Custom,
}

/// <summary>Постоянные пользовательские настройки. Сериализуются в %AppData%\PravVyd\settings.json.</summary>
public sealed class AppSettings
{
    public double? BubbleX { get; set; }
    public double? BubbleY { get; set; }
    public bool BubbleVisible { get; set; } = true;

    public bool MarkdownEnabled { get; set; } = true;
    public bool PdfEnabled { get; set; } = true;
    public bool DocxEnabled { get; set; } = true;

    public OutputLocation Output { get; set; } = OutputLocation.Temp;
    public string? CustomOutputFolder { get; set; }

    public bool AutoStart { get; set; }
}
