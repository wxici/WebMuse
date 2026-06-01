namespace WebRebuildRecorder.App.Services;

public static class ToolPathResolver
{
    public static string GetDefaultFfmpegPath() => ResolveBundledToolPath("ffmpeg.exe", "ffmpeg");

    public static string GetDefaultFfprobePath() => ResolveBundledToolPath("ffprobe.exe", "ffprobe");

    private static string ResolveBundledToolPath(string executableName, string fallback)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg", executableName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Tools", "ffmpeg", executableName)
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return fallback;
    }
}
