using System.Globalization;

namespace WebRebuildRecorder.App.Services;

public static class TimeUtil
{
    public static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    public static string FormatTimeCompact(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}-{time.Minutes:00}-{time.Seconds:00}";
    }

    public static string FormatSeconds(double seconds)
    {
        return seconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
