using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using QPARKShot.Helpers;
using QPARKShot.Models;

namespace QPARKShot.Services;

/// <summary>
/// Win32 global hotkey registration. Mirror of macOS Carbon-based
/// <c>AppDelegate.syncHotkeySettings</c>. Hosts a hidden message-only window
/// to receive WM_HOTKEY.
/// </summary>
public sealed class HotkeyService
{
    public static HotkeyService Shared { get; } = new();

    private MessageWindow? _msgWindow;
    private readonly Dictionary<int, string> _idToAction = new();
    private int _nextHotkeyId = 1;

    private HotkeyService() { }

    public void Start()
    {
        _msgWindow ??= new MessageWindow(OnHotkey);
        Sync();
        SettingsStore.Shared.SettingsChanged += (_, _) => Sync();
    }

    public void Stop()
    {
        UnregisterAll();
        _msgWindow?.Dispose();
        _msgWindow = null;
    }

    public void Sync()
    {
        UnregisterAll();
        if (_msgWindow == null) return;

        var s = SettingsStore.Shared.Settings;
        Register(s.Hotkey, "captureSelection");
        Register(s.FullScreenHotkey, "captureFullScreen");
    }

    private void Register(HotkeyConfig cfg, string action)
    {
        if (!cfg.Enabled || string.IsNullOrEmpty(cfg.Key)) return;
        uint vk = NativeMethods.VirtualKeyFromChar(cfg.Key);
        if (vk == 0) return;

        uint mods = 0;
        foreach (var m in cfg.Modifiers)
        {
            switch (m.ToLowerInvariant())
            {
                case "control": case "ctrl":    mods |= NativeMethods.MOD_CONTROL; break;
                case "shift":                   mods |= NativeMethods.MOD_SHIFT; break;
                case "alt": case "option":      mods |= NativeMethods.MOD_ALT; break;
                case "meta": case "cmd": case "command": case "win": case "windows":
                    mods |= NativeMethods.MOD_WIN; break;
            }
        }
        if (mods == 0) return;

        int id = _nextHotkeyId++;
        bool ok = NativeMethods.RegisterHotKey(_msgWindow!.Handle, id, mods | NativeMethods.MOD_NOREPEAT, vk);
        if (ok)
        {
            _idToAction[id] = action;
        }
    }

    private void UnregisterAll()
    {
        if (_msgWindow == null) return;
        foreach (var id in _idToAction.Keys)
        {
            NativeMethods.UnregisterHotKey(_msgWindow.Handle, id);
        }
        _idToAction.Clear();
    }

    private void OnHotkey(int id)
    {
        if (!_idToAction.TryGetValue(id, out var action)) return;
        var dq = DispatcherQueue.GetForCurrentThread() ?? App.MainDispatcherQueue;
        dq?.TryEnqueue(async () =>
        {
            switch (action)
            {
                case "captureFullScreen":
                    await CaptureService.Shared.TriggerCapture(modeOverride: "fullScreen");
                    break;
                default:
                    await CaptureService.Shared.TriggerCapture(modeOverride: "selection");
                    break;
            }
        });
    }
}

/// <summary>Hidden message-only Win32 window receiving WM_HOTKEY.</summary>
internal sealed class MessageWindow : IDisposable
{
    private readonly Action<int> _onHotkey;
    private IntPtr _hwnd;
    private readonly WndProcDelegate _wndProc;
    private GCHandle _gcHandle;
    private const int WS_OVERLAPPED = 0;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    public IntPtr Handle => _hwnd;

    public MessageWindow(Action<int> onHotkey)
    {
        _onHotkey = onHotkey;
        _wndProc = WndProc;
        _gcHandle = GCHandle.Alloc(_wndProc);

        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "QPARKShot.HotkeyMessageWindow",
        };
        RegisterClass(ref wc);
        _hwnd = CreateWindowEx(0, wc.lpszClassName, "", WS_OVERLAPPED, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            _onHotkey?.Invoke(id);
            return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        if (_gcHandle.IsAllocated) _gcHandle.Free();
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int width, int height,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
