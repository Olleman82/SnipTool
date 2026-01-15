using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using SnipTool.Models;

namespace SnipTool.Services;

public enum HotkeyAction
{
    Rectangle,
    Window,
    Fullscreen,
    CopyLast
}

public sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr _handle;
    private readonly Dictionary<int, HotkeyAction> _actions = new();
    private int _nextId = 1;

    public event Action<HotkeyAction>? HotkeyPressed;

    public HotkeyManager(HwndSource source)
    {
        _handle = source.Handle;
        source.AddHook(WndProc);
    }

    public void RegisterFromSettings(AppSettings settings)
    {
        UnregisterAll();
        Register(settings.Hotkeys.Rectangle, HotkeyAction.Rectangle);
        Register(settings.Hotkeys.Window, HotkeyAction.Window);
        Register(settings.Hotkeys.Fullscreen, HotkeyAction.Fullscreen);
        Register(settings.Hotkeys.CopyLast, HotkeyAction.CopyLast);
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_handle, id);
        }
        _actions.Clear();
        _nextId = 1;
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    private void Register(string hotkey, HotkeyAction action)
    {
        if (!TryParseHotkey(hotkey, out var modifiers, out var vk))
        {
            return;
        }

        var id = _nextId++;
        if (RegisterHotKey(_handle, id, modifiers, vk))
        {
            _actions[id] = action;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var action))
            {
                HotkeyPressed?.Invoke(action);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private static bool TryParseHotkey(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    if (part.Length == 1 && char.IsDigit(part[0]))
                    {
                        var digit = part[0] - '0';
                        var key = (Key)((int)Key.D0 + digit);
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    }
                    else if (Enum.TryParse(part, true, out Key parsed))
                    {
                        vk = (uint)KeyInterop.VirtualKeyFromKey(parsed);
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }
        }

        return vk != 0;
    }

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
