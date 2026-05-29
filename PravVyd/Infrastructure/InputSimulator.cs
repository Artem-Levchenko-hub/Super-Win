using System.Runtime.InteropServices;

namespace PravVyd.Infrastructure;

/// <summary>Синтетический ввод: Ctrl+C (снять выделение) и Ctrl+V (вставить).</summary>
public static class InputSimulator
{
    public static void SendCtrlC() => SendCtrlCombo(NativeMethods.VK_C);

    public static void SendCtrlV() => SendCtrlCombo(NativeMethods.VK_V);

    /// <summary>N нажатий Backspace (удалить набранное слово перед заменой).</summary>
    public static void SendBackspaces(int count)
    {
        if (count <= 0)
            return;

        var inputs = new NativeMethods.INPUT[count * 2];
        for (var i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyDown(NativeMethods.VK_BACK);
            inputs[i * 2 + 1] = KeyUp(NativeMethods.VK_BACK);
        }

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>Одно нажатие виртуальной клавиши (напр. пробел).</summary>
    public static void SendKey(ushort vk)
    {
        NativeMethods.INPUT[] inputs = { KeyDown(vk), KeyUp(vk) };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>Печатает текст как Unicode — не зависит от активной раскладки (KEYEVENTF_UNICODE).</summary>
    public static void SendUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var inputs = new NativeMethods.INPUT[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeKey(text[i], keyUp: false);
            inputs[i * 2 + 1] = UnicodeKey(text[i], keyUp: true);
        }

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT UnicodeKey(char ch, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = NativeMethods.KEYEVENTF_UNICODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0),
            },
        },
    };

    private static void SendCtrlCombo(ushort key)
    {
        NativeMethods.INPUT[] inputs =
        {
            KeyDown(NativeMethods.VK_CONTROL),
            KeyDown(key),
            KeyUp(key),
            KeyUp(NativeMethods.VK_CONTROL),
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT KeyDown(ushort vk) => Key(vk, keyUp: false);

    private static NativeMethods.INPUT KeyUp(ushort vk) => Key(vk, keyUp: true);

    private static NativeMethods.INPUT Key(ushort vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
            },
        },
    };
}
