using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class FfprobeService
{
    private readonly AppLogger _logger;

    public FfprobeService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<RecordingInfo> ProbeAsync(string ffprobePath, string videoPath, DateTime startTime, DateTime endTime, RecordingArea area, string browser, bool isAutoObserve, bool hadManualTakeover)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("视频文件不存在。", videoPath);
        }

        EnsureExecutableLooksValid(ffprobePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(videoPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("FFprobe 启动失败。");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFprobe 读取失败：{stderr}");
        }

        var info = ParseProbeJson(stdout);
        info.VideoPath = videoPath;
        info.FileSizeBytes = new FileInfo(videoPath).Length;
        info.StartTime = startTime;
        info.EndTime = endTime;
        if (info.DurationSeconds <= 0 && startTime != DateTime.MinValue && endTime > startTime)
        {
            info.DurationSeconds = Math.Max(0, (endTime - startTime).TotalSeconds);
        }
        info.RecordingArea = area;
        info.Browser = browser;
        info.IsAutoObserve = isAutoObserve;
        info.HadManualTakeover = hadManualTakeover;

        _logger.Info($"FFprobe OK: duration={info.DurationSeconds:0.###}s size={info.Width}x{info.Height}");
        return info;
    }

    public async Task<double> GetDurationSecondsAsync(string ffprobePath, string videoPath)
    {
        var info = await ProbeAsync(ffprobePath, videoPath, DateTime.MinValue, DateTime.MinValue, RecordingArea.FullScreen(), string.Empty, false, false);
        return info.DurationSeconds;
    }

    private static RecordingInfo ParseProbeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var info = new RecordingInfo();

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (!stream.TryGetProperty("codec_type", out var codecType)
                    || !string.Equals(codecType.GetString(), "video", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (stream.TryGetProperty("width", out var width))
                {
                    info.Width = width.GetInt32();
                }

                if (stream.TryGetProperty("height", out var height))
                {
                    info.Height = height.GetInt32();
                }

                if (stream.TryGetProperty("avg_frame_rate", out var frameRate))
                {
                    info.FrameRate = ParseRational(frameRate.GetString());
                }

                if (stream.TryGetProperty("duration", out var streamDuration)
                    && double.TryParse(streamDuration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
                {
                    info.DurationSeconds = duration;
                }

                break;
            }
        }

        if (root.TryGetProperty("format", out var format))
        {
            if (info.DurationSeconds <= 0
                && format.TryGetProperty("duration", out var durationElement)
                && double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
            {
                info.DurationSeconds = duration;
            }
        }

        return info;
    }

    private static double ParseRational(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var parts = value.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static void EnsureExecutableLooksValid(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("FFprobe 路径不能为空。");
        }

        var containsPath = executablePath.Contains(Path.DirectorySeparatorChar)
            || executablePath.Contains(Path.AltDirectorySeparatorChar)
            || Path.IsPathRooted(executablePath);

        if (containsPath && !File.Exists(executablePath))
        {
            throw new FileNotFoundException("FFprobe 路径不存在。", executablePath);
        }
    }
}
