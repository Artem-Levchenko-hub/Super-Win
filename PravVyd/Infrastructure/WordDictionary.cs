using System.IO;
using System.Reflection;

namespace PravVyd.Infrastructure;

/// <summary>Частотные словари EN/RU (вшиты как embedded-ресурсы) + список IT-терминов.
/// Отвечает на вопрос «это реальное слово языка?» — основа авто-смены раскладки в обе стороны.</summary>
public sealed class WordDictionary
{
    private readonly HashSet<string> _en;
    private readonly HashSet<string> _ru;

    // латиница без гласных, но валидные термины — НЕ конвертим EN→RU (иначе sql→ыйд).
    private static readonly HashSet<string> ItTerms = new(StringComparer.Ordinal)
    {
        "sql", "html", "css", "php", "xml", "json", "http", "https", "www", "ftp", "ssh",
        "sdk", "api", "npm", "git", "jvm", "jdk", "csv", "pdf", "png", "jpg", "jpeg", "gif",
        "svg", "yml", "yaml", "js", "ts", "tsx", "jsx", "py", "rs", "go", "cpp", "cs",
        "db", "sqlite", "mysql", "psql", "nginx", "dns", "tcp", "udp", "ip", "ssl", "tls",
        "jwt", "orm", "mvc", "crud", "regex", "cli", "gui", "ui", "ux", "ci", "cd", "vm",
        "vpn", "url", "uri", "uuid", "ascii", "utf", "ram", "cpu", "gpu", "ssd", "hdd",
        "linux", "unix", "bash", "zsh", "vim", "grep", "curl", "wget", "docker", "k8s",
        "redis", "mongodb", "postgres", "kafka", "github", "gitlab", "devops", "frontend",
        "backend", "kwargs", "args", "stdin", "stdout", "stderr", "env", "tmp", "src", "dist",
    };

    public WordDictionary()
    {
        _en = Load("PravVyd.Resources.en_words.txt", russian: false);
        _ru = Load("PravVyd.Resources.ru_words.txt", russian: true);
    }

    public bool IsEnglish(string word)
    {
        var w = Norm(word);
        return _en.Contains(w) || ItTerms.Contains(w);
    }

    public bool IsRussian(string word) => _ru.Contains(NormRu(word));

    private static string Norm(string w) => w.Trim().ToLowerInvariant();

    // ё→е: списки/ввод могут различаться написанием — приводим к одному виду.
    private static string NormRu(string w) => Norm(w).Replace('ё', 'е');

    private static HashSet<string> Load(string resource, bool russian)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        if (stream is null)
            return set;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var w = russian ? NormRu(line) : Norm(line);
            if (w.Length > 0)
                set.Add(w);
        }

        return set;
    }
}
