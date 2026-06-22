using System;
using System.Runtime.InteropServices;

namespace QPARKShot.Helpers;

internal static class NativeMethods
{
    // ===== Hotkeys =====
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT      = 0x0001;
    public const uint MOD_CONTROL  = 0x0002;
    public const uint MOD_SHIFT    = 0x0004;
    public const uint MOD_WIN      = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    // ===== Screen capture =====
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    public static uint VirtualKeyFromChar(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        char c = char.ToUpperInvariant(key[0]);
        if (c >= 'A' && c <= 'Z') return (uint)c;
        if (c >= '0' && c <= '9') return (uint)c;
        return 0;
    }
}
