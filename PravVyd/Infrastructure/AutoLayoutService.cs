using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using PravVyd.Settings;

namespace PravVyd.Infrastructure;

/// <summary>Полная авто-раскладка (Punto-style, на словарях). По пробелу проверяет по словарям EN/RU,
/// в какой раскладке набранное слово реально существует, и при ошибке заменяет текст + переключает язык.
/// Работает в ОБЕ стороны: ghbdtn→привет (EN→RU) и руддщ→hello (RU→EN). IT-термины (sql, html) не трогает.
/// Если слова нет ни в одном словаре — НЕ трогаем (имена, переменные, пароли остаются как есть).</summary>
public sealed class AutoLayoutService : IDisposable
{
    private const int LangEnglish = 0x09;
    private const int LangRussian = 0x19;
    private const int MaxWord = 40;

    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly Dispatcher _dispatcher;
    private readonly WordDictionary _dict;
    private readonly StringBuilder _keys = new(); // латинское представление физических клавиш (QWERTY)
    private IntPtr _hook;

    public bool Enabled { get; set; }

    public AutoLayoutService(AppSettings settings, WordDictionary dict)
    {
        Enabled = settings.AutoLayoutFix;
        _dict = dict;
        _dispatcher = Application.Current.Dispatcher;
        _proc = HookCallback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Enabled && wParam.ToInt32() == NativeMethods.WM_KEYDOWN)
        {
            try
            {
                // свой синтетический ввод (Backspace/Unicode/пробел) помечен INJECTED — не буферим и не глотаем
                var flags = Marshal.ReadInt32(lParam, 8);
                if ((flags & NativeMethods.LLKHF_INJECTED) == 0 && OnKeyDown(Marshal.ReadInt32(lParam)))
                    return new IntPtr(1); // проглотить пробел — заменим слово сами
            }
            catch
            {
                // никогда не ломаем ввод глобально из-за ошибки замены
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <returns>true → проглотить клавишу (только пробел, запускающий замену).</returns>
    private bool OnKeyDown(int vk)
    {
        // физ. клавиша-буква (A–Z + OEM-клавиши с рус. буквами э х ъ ж б ю ё): копим латинское представление
        if (TryLetterChar(vk, out var ch))
        {
            if (CurrentLang() is LangEnglish or LangRussian && _keys.Length < MaxWord)
                _keys.Append(ch);
            else
                _keys.Clear();
            return false;
        }

        // Backspace — правим буфер, слово не сбрасываем
        if (vk == NativeMethods.VK_BACK)
        {
            if (_keys.Length > 0)
                _keys.Remove(_keys.Length - 1, 1);
            return false;
        }

        // Пробел — граница слова: решаем, менять ли раскладку
        if (vk == NativeMethods.VK_SPACE)
        {
            var keys = _keys.ToString();
            _keys.Clear();
            return TryFixOnSpace(keys);
        }

        // любая другая клавиша (цифра, пунктуация, навигация) — конец слова
        _keys.Clear();
        return false;
    }

    /// <returns>true → проглотить пробел (замена запущена, пробел отправим сами после неё).</returns>
    private bool TryFixOnSpace(string keys)
    {
        if (keys.Length < 2)
            return false;

        var lang = CurrentLang();
        var latin = keys;                          // англ. чтение (как на QWERTY)
        var cyr = LayoutConverter.Convert(keys);   // рус. чтение (ЙЦУКЕН)

        if (lang == LangEnglish)
        {
            // на экране latin. Реальное англ. слово / IT-термин — не трогаем
            if (_dict.IsEnglish(latin))
                return false;
            // не английское, но рус. чтение — реальное слово → набрано не в той раскладке
            if (_dict.IsRussian(cyr) && TryGetHkl(LangRussian, out var ru))
            {
                _dispatcher.BeginInvoke(() => Replace(onScreen: latin, replacement: cyr, ru));
                return true;
            }

            return false;
        }

        if (lang == LangRussian)
        {
            // на экране cyr. Реальное рус. слово — не трогаем
            if (_dict.IsRussian(cyr))
                return false;
            // не русское, но англ. чтение — реальное слово / IT-термин → набрано не в той раскладке
            if (_dict.IsEnglish(latin) && TryGetHkl(LangEnglish, out var en))
            {
                _dispatcher.BeginInvoke(() => Replace(onScreen: cyr, replacement: latin, en));
                return true;
            }

            return false;
        }

        return false;
    }

    private void Replace(string onScreen, string replacement, IntPtr targetHkl)
    {
        InputSimulator.SendBackspaces(onScreen.Length); // стереть набранное
        InputSimulator.SendUnicodeText(replacement);     // напечатать в правильной раскладке

        // переключить раскладку активного окна на нужный язык
        var fg = NativeMethods.GetForegroundWindow();
        NativeMethods.PostMessage(fg, NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetHkl);

        InputSimulator.SendKey((ushort)NativeMethods.VK_SPACE); // вернуть проглоченный пробел
    }

    private static char ToChar(int vk)
    {
        var shift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
        var caps = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0;
        var upper = shift ^ caps;
        return (char)((upper ? 'A' : 'a') + (vk - 0x41));
    }

    /// <summary>Латинский символ физ. клавиши: A–Z + OEM-клавиши, дающие рус. буквы (х ъ ж э б ю ё).
    /// LayoutConverter перекладывает их в кириллицу. false → клавиша не буква (граница слова).</summary>
    private static bool TryLetterChar(int vk, out char ch)
    {
        if (vk is >= 0x41 and <= 0x5A)
        {
            ch = ToChar(vk);
            return true;
        }

        ch = vk switch
        {
            0xDB => '[',  // х
            0xDD => ']',  // ъ
            0xBA => ';',  // ж
            0xDE => '\'', // э
            0xBC => ',',  // б
            0xBE => '.',  // ю
            0xC0 => '`',  // ё
            _ => '\0',
        };
        return ch != '\0';
    }

    private static int CurrentLang()
    {
        var fg = NativeMethods.GetForegroundWindow();
        var tid = NativeMethods.GetWindowThreadProcessId(fg, out _);
        var lang = NativeMethods.GetKeyboardLayout(tid).ToInt32() & 0xFFFF;
        return lang & 0x3FF;
    }

    private static bool TryGetHkl(int lang, out IntPtr hkl)
    {
        hkl = IntPtr.Zero;
        var count = NativeMethods.GetKeyboardLayoutList(0, Array.Empty<IntPtr>());
        if (count == 0)
            return false;

        var list = new IntPtr[count];
        NativeMethods.GetKeyboardLayoutList((int)count, list);
        foreach (var h in list)
        {
            if (((h.ToInt32() & 0xFFFF) & 0x3FF) == lang)
            {
                hkl = h;
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
