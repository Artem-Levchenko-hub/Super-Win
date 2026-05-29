using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace PravVyd.Infrastructure;

/// <summary>На запуске спрашивает у GitHub последний Release. Если его версия новее текущей —
/// зовёт onUpdate(версия, url-страницы). Ничего не качает и не заменяет — только уведомляет.
/// Все сетевые ошибки глотает: проверка обновлений — не критичный путь.</summary>
public sealed class UpdateChecker
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/Artem-Levchenko-hub/Super-Win/releases/latest";

    private readonly Action<string, string> _onUpdate;
    private readonly Dispatcher _dispatcher;

    public UpdateChecker(Action<string, string> onUpdate)
    {
        _onUpdate = onUpdate;
        _dispatcher = Application.Current.Dispatcher;
    }

    public void CheckInBackground() => _ = CheckAsync();

    private async Task CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PravVyd-UpdateChecker");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(LatestReleaseApi);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString();
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            if (tag is null || url is null || !TryParseVersion(tag, out var latest))
                return;

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            if (latest <= Normalize(current))
                return;

            _dispatcher.BeginInvoke(() => _onUpdate(tag, url));
        }
        catch
        {
            // нет сети / лимит API / нет релизов — молча выходим
        }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static bool TryParseVersion(string tag, out Version version)
    {
        var s = tag.TrimStart('v', 'V').Trim();
        if (Version.TryParse(s, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }

        version = new Version(0, 0);
        return false;
    }
}
