using System.Runtime.InteropServices;

namespace WebRebuildRecorder.App.Services;

public static class NativeInputService
{
    private const uint InputMouse = 0;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    public static DateTime LastProgrammaticMouseMoveTime { get; private set; } = DateTime.MinValue;
    public static ScreenPoint LastProgrammaticMousePosition { get; private set; }

    public static void ScrollWheel(int wheelDelta)
    {
        var input = new INPUT
        {
            type = InputMouse,
            mi = new MOUSEINPUT
            {
                mouseData = wheelDelta,
                dwFlags = MouseEventWheel
            }
        };

        var inputs = new[] { input };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void LeftClick()
    {
        var inputs = new[]
        {
            new INPUT { type = InputMouse, mi = new MOUSEINPUT { dwFlags = MouseEventLeftDown } },
            new INPUT { type = InputMouse, mi = new MOUSEINPUT { dwFlags = MouseEventLeftUp } }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public static ScreenPoint GetCursorPosition()
    {
        return GetCursorPos(out var point) ? new ScreenPoint(point.X, point.Y) : new ScreenPoint(0, 0);
    }

    public static async Task MoveMouseSmoothAsync(int targetX, int targetY, int durationMs, CancellationToken token)
    {
        var start = GetCursorPosition();
        var steps = Math.Max(10, durationMs / 16);
        for (var i = 1; i <= steps; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)steps;
            var eased = t * t * (3 - 2 * t);
            var x = (int)Math.Round(start.X + (targetX - start.X) * eased);
            var y = (int)Math.Round(start.Y + (targetY - start.Y) * eased);
            SetCursorPos(x, y);
            LastProgrammaticMouseMoveTime = DateTime.Now;
            LastProgrammaticMousePosition = new ScreenPoint(x, y);
            await Task.Delay(Math.Max(1, durationMs / steps), token);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}

public readonly record struct ScreenPoint(int X, int Y);
