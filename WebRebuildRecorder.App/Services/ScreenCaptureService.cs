using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class ScreenCaptureService
{
    private readonly AppLogger _logger;

    public ScreenCaptureService(AppLogger logger)
    {
        _logger = logger;
    }

    public void Capture(RecordingArea area, string outputPath, string format, int quality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var bounds = area.IsRegion
            ? new NativeRect(area.X, area.Y, area.Width, area.Height)
            : GetVirtualScreenBounds();

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法获取屏幕 DC。");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var oldObject = IntPtr.Zero;

        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            bitmap = CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);
            oldObject = SelectObject(memoryDc, bitmap);

            if (!BitBlt(memoryDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.X, bounds.Y, CopyPixelOperation.SourceCopy))
            {
                throw new InvalidOperationException("屏幕截图失败。");
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            SaveBitmapSource(source, outputPath, format, quality);
            _logger.Info($"Screenshot saved: {outputPath}");
        }
        finally
        {
            if (oldObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                SelectObject(memoryDc, oldObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public static NativeRect GetVirtualScreenBounds()
    {
        return new NativeRect(
            GetSystemMetrics(76),
            GetSystemMetrics(77),
            GetSystemMetrics(78),
            GetSystemMetrics(79));
    }

    private static void SaveBitmapSource(BitmapSource source, string outputPath, string format, int quality)
    {
        BitmapEncoder encoder;
        if (string.Equals(format, "PNG", StringComparison.OrdinalIgnoreCase))
        {
            encoder = new PngBitmapEncoder();
        }
        else
        {
            encoder = new JpegBitmapEncoder
            {
                QualityLevel = Math.Clamp(quality, 1, 100)
            };
        }

        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, CopyPixelOperation rop);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}

public readonly record struct NativeRect(int X, int Y, int Width, int Height);

public enum CopyPixelOperation : int
{
    SourceCopy = 0x00CC0020
}
