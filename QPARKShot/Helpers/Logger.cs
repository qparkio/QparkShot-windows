using System;
using System.IO;
using System.Runtime.InteropServices;

namespace QPARKShot.Helpers;

/// <summary>
/// Dirt-cheap file logger so we have *something* to read when the app
/// silently exits on a user machine without a debugger attached.
/// Path: %TEMP%\qparkshot-debug.log (rotated on each launch).
/// </summary>
public static class Logger
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "qparkshot-debug.log");

    private static readonly object Sync = new();
    private static bool _initialized;

    public static string Path => LogPath;

    public static void Init()
    {
        try
        {
            lock (Sync)
            {
                if (_initialized) return;
                _initialized = true;
                File.WriteAllText(LogPath, $"=== QPARK Shot launch {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n");
            }
        }
        catch { }
    }

    public static void Log(string msg)
    {
        try
        {
            Init();
            lock (Sync)
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\r\n");
            }
        }
        catch { }
    }

    public static void LogException(string where, Exception ex)
    {
        Log($"EXCEPTION at {where}: {ex.GetType().Name}: {ex.Message}");
        Log($"  StackTrace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            LogException(where + " (inner)", ex.InnerException);
        }
    }

    /// <summary>Fallback message box via Win32 — works even if XAML isn't initialized yet.</summary>
    public static void ShowFatal(string title, string message)
    {
        Log($"FATAL: {title} — {message}");
        try
        {
            MessageBoxW(IntPtr.Zero, message + "\n\nFull log: " + LogPath, title, 0x00000010); // MB_ICONERROR
        }
        catch { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
