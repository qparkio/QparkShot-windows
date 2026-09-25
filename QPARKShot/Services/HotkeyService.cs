using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using QPARKShot.Helpers;
using QPARKShot.Models;

namespace QPARKShot.Services;

/// <summary>
/// Global hotkey via Win32 RegisterHotKey. Listens via HwndSource on the main window.
/// </summary>
public sealed class HotkeyService
{
    public static HotkeyService Shared { get; } = new();

    private HwndSource? _source;
    private IntPtr _hwnd;
    private readonly Dictionary<int, string> _idToAction = new();
    private int _nextHotkeyId = 0xC000; // 0xC000 — start of app-private hotkey range

    private HotkeyService() { }

    public void Start(Window owner)
    {
        var helper = new WindowInteropHelper(owner);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        Sync();
        SettingsStore.Shared.SettingsChanged += OnSettingsChanged;
    }

    public void Stop()
    {
        SettingsStore.Shared.SettingsChanged -= OnSettingsChanged;
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Sync();
    public static string? Validate(HotkeyConfig value, HotkeyConfig other)
    {
        if (!value.Enabled) return null;
        if (value.Key.Length != 1 || !char.IsAsciiLetterOrDigit(value.Key[0]) || !value.Modifiers.Any(m => m is "control" or "option" or "alt" or "command" or "win")) return "settings.shortcut_invalid";
        if (other.Enabled && value.Key.Equals(other.Key, StringComparison.OrdinalIgnoreCase) && value.Modifiers.OrderBy(m => m).SequenceEqual(other.Modifiers.OrderBy(m => m))) return "settings.shortcut_duplicate";
        return null;
    }
    public void Sync()
    {
        UnregisterAll();
        if (_hwnd == IntPtr.Zero) return;

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
                case "control": case "ctrl":   mods |= NativeMethods.MOD_CONTROL; break;
                case "shift":                  mods |= NativeMethods.MOD_SHIFT; break;
                case "alt": case "option":     mods |= NativeMethods.MOD_ALT; break;
                case "meta": case "cmd": case "command": case "win": case "windows":
                    mods |= NativeMethods.MOD_WIN; break;
            }
        }
        if (mods == 0) return;

        int id = _nextHotkeyId++;
        bool ok = NativeMethods.RegisterHotKey(_hwnd, id, mods | NativeMethods.MOD_NOREPEAT, vk);
        if (ok)
        {
            _idToAction[id] = action;
            Logger.Log($"Hotkey registered: {action} → id={id}");
        }
        else
        {
            Logger.Log($"Hotkey FAILED to register: {action} (mods={mods:X} vk={vk:X})");
            WorkspaceStore.Shared.Notify(Localization.L.T("settings.shortcut_invalid") + " · " + cfg.Key);
        }
    }

    private void UnregisterAll()
    {
        foreach (var id in _idToAction.Keys)
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
        _idToAction.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_idToAction.TryGetValue(id, out var action))
            {
                handled = true;
                Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
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
                    }
                    catch (Exception ex) { Logger.LogException("Hotkey trigger", ex); }
                }));
            }
        }
        return IntPtr.Zero;
    }
}
