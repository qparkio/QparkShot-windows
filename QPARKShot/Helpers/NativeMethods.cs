using System;
using System.Runtime.InteropServices;

namespace QPARKShot.Helpers;

internal static class NativeMethods
{
    // user32.dll - hotkeys
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;
    public const uint MOD_WIN     = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    // gdi32.dll - screen capture
    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                     IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    public const uint SRCCOPY = 0x00CC0020;
    public const uint CAPTUREBLT = 0x40000000;

    // Virtual-key codes (subset that we care about for hotkey letters/digits)
    public static uint VirtualKeyFromChar(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        char c = char.ToUpperInvariant(key[0]);
        if (c >= 'A' && c <= 'Z') return (uint)c;
        if (c >= '0' && c <= '9') return (uint)c;
        return 0;
    }
}
