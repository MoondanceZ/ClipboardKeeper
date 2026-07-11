using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace ClipboardKeeper;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x434B;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly WndProcDelegate _wndProc;
    private IntPtr _hwnd;
    private ushort _classAtom;
    private bool _registered;

    public GlobalHotkeyService()
    {
        _wndProc = WndProc;
    }

    public event EventHandler? Pressed;

    public bool Register(string? hotkeyText)
    {
        Unregister();

        if (!TryParse(hotkeyText, out var gesture))
        {
            return false;
        }

        EnsureMessageWindow();
        _registered = RegisterHotKey(_hwnd, HotkeyId, gesture.Modifiers, gesture.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
    }

    public static string? NormalizeDisplayText(string? hotkeyText)
    {
        return TryParse(hotkeyText, out var gesture) ? gesture.DisplayText : null;
    }

    public void Dispose()
    {
        Unregister();
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        if (_classAtom != 0)
        {
            UnregisterClass("ClipboardKeeperHotkeyWindow", GetModuleHandle(null));
            _classAtom = 0;
        }
    }

    private void EnsureMessageWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            return;
        }

        var hInstance = GetModuleHandle(null);
        var className = "ClipboardKeeperHotkeyWindow";
        var wndClass = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            Instance = hInstance,
            ClassName = className,
            WindowProc = Marshal.GetFunctionPointerForDelegate(_wndProc)
        };

        _classAtom = RegisterClassEx(ref wndClass);
        if (_classAtom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410)
            {
                throw new InvalidOperationException($"注册快捷键窗口失败: {error}");
            }
        }

        _hwnd = CreateWindowEx(
            0,
            className,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"创建快捷键窗口失败: {Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Dispatcher.UIThread.Post(() => Pressed?.Invoke(this, EventArgs.Empty));
            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static bool TryParse(string? hotkeyText, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            return false;
        }

        var modifiers = 0u;
        uint? virtualKey = null;
        var displayParts = new List<string>();
        foreach (var rawPart in hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
                AddDisplayPart(displayParts, "Ctrl");
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)
                     || part.Equals("Alc", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
                AddDisplayPart(displayParts, "Alt");
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
                AddDisplayPart(displayParts, "Shift");
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)
                     || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
                AddDisplayPart(displayParts, "Win");
            }
            else if (TryParseKey(part, out var parsedKey, out var displayKey))
            {
                virtualKey = parsedKey;
                displayParts.Add(displayKey);
            }
            else
            {
                return false;
            }
        }

        if (modifiers == 0 || virtualKey is null)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, virtualKey.Value, string.Join("+", displayParts));
        return true;
    }

    private static bool TryParseKey(string text, out uint virtualKey, out string displayKey)
    {
        virtualKey = 0;
        displayKey = string.Empty;
        if (text.Length == 1)
        {
            var ch = char.ToUpperInvariant(text[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = ch;
                displayKey = ch.ToString();
                return true;
            }
        }

        if (text.Length is >= 2 and <= 3 && text[0] is 'F' or 'f' && int.TryParse(text[1..], out var functionKey) && functionKey is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionKey - 1);
            displayKey = $"F{functionKey}";
            return true;
        }

        return false;
    }

    private static void AddDisplayPart(List<string> parts, string part)
    {
        if (!parts.Contains(part))
        {
            parts.Add(part);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr IconSmall;
    }

    private readonly record struct HotkeyGesture(uint Modifiers, uint VirtualKey, string DisplayText);
}
