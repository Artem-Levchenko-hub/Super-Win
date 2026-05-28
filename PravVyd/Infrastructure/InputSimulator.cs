using System.Runtime.InteropServices;

namespace PravVyd.Infrastructure;

/// <summary>Синтетический ввод: Ctrl+C (снять выделение) и Ctrl+V (вставить).</summary>
public static class InputSimulator
{
    public static void SendCtrlC() => SendCtrlCombo(NativeMethods.VK_C);

    public static void SendCtrlV() => SendCtrlCombo(NativeMethods.VK_V);

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
