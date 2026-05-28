using System.IO;
using PravVyd.Settings;

namespace PravVyd.Services;

/// <summary>Решает, куда и под каким именем класть сгенерированный файл.</summary>
public sealed class OutputPathResolver
{
    private readonly AppSettings _settings;

    public OutputPathResolver(AppSettings settings) => _settings = settings;

    public string Resolve(string extension, string title)
    {
        var folder = ResolveFolder();
        Directory.CreateDirectory(folder);

        var name = $"{Sanitize(title)}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
        return Path.Combine(folder, name);
    }

    private string ResolveFolder() => _settings.Output switch
    {
        OutputLocation.Documents =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PravVyd"),
        OutputLocation.Custom when !string.IsNullOrWhiteSpace(_settings.CustomOutputFolder) =>
            _settings.CustomOutputFolder!,
        _ => Path.Combine(Path.GetTempPath(), "PravVyd"),
    };

    private static string Sanitize(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Concat(title.Select(c => invalid.Contains(c) ? '_' : c)).Trim();

        if (cleaned.Length == 0)
            return "Документ";

        return cleaned.Length <= 40 ? cleaned : cleaned[..40].TrimEnd();
    }
}
