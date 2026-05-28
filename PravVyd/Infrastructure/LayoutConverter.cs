using System.Text;

namespace PravVyd.Infrastructure;

/// <summary>Перекладка текста по позиции клавиш QWERTY↔ЙЦУКЕН (исправление неправильной раскладки).</summary>
public static class LayoutConverter
{
    private const string En = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?~";
    private const string Ru = "йцукенгшщзхъфывапролджэячсмитьбю.ёЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,Ё";

    private static readonly Dictionary<char, char> EnToRu = Build(En, Ru);
    private static readonly Dictionary<char, char> RuToEn = Build(Ru, En);

    /// <summary>Конвертит текст в другую раскладку. Направление по содержимому: есть кириллица → RU→EN, иначе EN→RU.</summary>
    public static string Convert(string text)
    {
        var hasCyrillic = text.Any(c => c is (>= 'А' and <= 'я') or 'ё' or 'Ё');
        var map = hasCyrillic ? RuToEn : EnToRu;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
            builder.Append(map.TryGetValue(c, out var mapped) ? mapped : c);

        return builder.ToString();
    }

    private static Dictionary<char, char> Build(string from, string to)
    {
        var map = new Dictionary<char, char>(from.Length);
        for (var i = 0; i < from.Length && i < to.Length; i++)
            map[from[i]] = to[i];

        return map;
    }
}
