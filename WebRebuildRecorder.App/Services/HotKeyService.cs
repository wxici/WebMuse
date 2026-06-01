using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public enum RecorderHotKey
{
    PauseAutomation,
    ResumeAutomation,
    Marker,
    Screenshot,
    StopRecording
}

public sealed class HotKeyRegistrationFailure
{
    public RecorderHotKey Action { get; init; }
    public string Hotkey { get; init; } = string.Empty;
    public int Id { get; init; }
    public int ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"快捷键注册失败：{Hotkey}，动作={Action}，id={Id}，错误码={ErrorCode}，系统消息={Message}";
    }
}

public sealed class HotKeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly Dictionary<int, RecorderHotKey> _hotKeys = new();
    private HwndSource? _source;
    private IntPtr _handle;

    public event EventHandler<RecorderHotKey>? HotKeyPressed;
    public event EventHandler<HotKeyRegistrationFailure>? RegisterFailed;

    public IReadOnlyCollection<RecorderHotKey> RegisteredActions => _hotKeys.Values.ToArray();

    public void Register(Window window, HotkeySettings settings)
    {
        UnregisterAll();

        if (!settings.Enabled)
        {
            return;
        }

        _handle = new WindowInteropHelper(window).Handle;
        if (_handle == IntPtr.Zero)
        {
            RaiseFailure(RecorderHotKey.PauseAutomation, settings.PauseAuto, 1001, 0, "窗口句柄尚未创建。");
            return;
        }

        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);

        RegisterOne(1001, RecorderHotKey.PauseAutomation, settings.PauseAuto);
        RegisterOne(1002, RecorderHotKey.ResumeAutomation, settings.ResumeAuto);
        RegisterOne(1003, RecorderHotKey.Marker, settings.Marker);
        RegisterOne(1004, RecorderHotKey.Screenshot, settings.Screenshot);
        RegisterOne(1005, RecorderHotKey.StopRecording, settings.StopRecording);
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotKeys.Keys.ToArray())
        {
            UnregisterHotKey(_handle, id);
        }

        _hotKeys.Clear();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    private void RegisterOne(int id, RecorderHotKey action, string hotkeyText)
    {
        if (!TryParseHotkey(hotkeyText, out var modifiers, out var key, out var parseError))
        {
            RaiseFailure(action, hotkeyText, id, 0, parseError);
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (RegisterHotKey(_handle, id, modifiers, (uint)virtualKey))
        {
            _hotKeys[id] = action;
            return;
        }

        var errorCode = Marshal.GetLastWin32Error();
        var message = new Win32Exception(errorCode).Message;
        RaiseFailure(action, hotkeyText, id, errorCode, message);
    }

    private void RaiseFailure(RecorderHotKey action, string hotkeyText, int id, int errorCode, string message)
    {
        RegisterFailed?.Invoke(this, new HotKeyRegistrationFailure
        {
            Action = action,
            Hotkey = hotkeyText,
            Id = id,
            ErrorCode = errorCode,
            Message = message
        });
    }

    private static bool TryParseHotkey(string text, out uint modifiers, out Key key, out string error)
    {
        modifiers = 0;
        key = Key.None;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "热键为空。";
            return false;
        }

        foreach (var rawPart in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
                continue;
            }

            if (!Enum.TryParse(part, ignoreCase: true, out key) || key == Key.None)
            {
                error = $"无法识别热键按键：{part}";
                return false;
            }
        }

        if (key == Key.None)
        {
            error = "热键缺少按键。";
            return false;
        }

        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _hotKeys.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            HotKeyPressed?.Invoke(this, action);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
