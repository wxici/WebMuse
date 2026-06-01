using System.Diagnostics;
using System.Globalization;
using System.Text;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class FfmpegFrameExtractor : IFrameExtractor
{
    private readonly AppLogger _logger;

    public FfmpegFrameExtractor(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<FrameExtractResult> ExtractAsync(FrameExtractOptions options, CancellationToken token = default)
    {
        if (!File.Exists(options.VideoPath))
        {
            throw new FileNotFoundException("抽帧来源视频不存在。", options.VideoPath);
        }

        FfmpegScreenRecorder.EnsureExecutableLooksValid(options.FfmpegPath);
        Directory.CreateDirectory(options.OutputDirectory);

        var extension = IsPng(options.Format) ? "png" : "jpg";
        var requestedFps = 1000d / options.IntervalMs;
        var sourceFps = options.SourceFrameRate > 0 ? options.SourceFrameRate : 30d;
        var fps = options.AllowDuplicateFramesAboveSourceFps ? requestedFps : Math.Min(requestedFps, sourceFps);
        var outputPattern = Path.Combine(options.OutputDirectory, $"{options.Prefix}_%06d.{extension}");

        var startInfo = new ProcessStartInfo
        {
            FileName = options.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(options.VideoPath);
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add(BuildVideoFilter(fps, options));

        if (options.MaxFrames is > 0)
        {
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add(options.MaxFrames.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (IsPng(options.Format))
        {
            startInfo.ArgumentList.Add("-compression_level");
            startInfo.ArgumentList.Add("3");
        }
        else
        {
            startInfo.ArgumentList.Add("-q:v");
            startInfo.ArgumentList.Add(MapJpegQuality(options.JpegQuality).ToString(CultureInfo.InvariantCulture));
        }

        startInfo.ArgumentList.Add(outputPattern);

        _logger.Info($"开始抽帧：间隔={options.IntervalMs}ms 输出目录={options.OutputDirectory}");
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("FFmpeg 抽帧进程启动失败。");
        var stderrTask = process.StandardError.ReadToEndAsync(token);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        var stderr = await stderrTask;
        _ = await stdoutTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg 抽帧失败：{stderr}");
        }

        var result = await BuildIndexesAsync(options, extension, fps);
        _logger.Info($"抽帧完成：{result.TotalFrames} 张");
        return result;
    }

    private static string BuildVideoFilter(double fps, FrameExtractOptions options)
    {
        var fpsText = fps.ToString("0.###", CultureInfo.InvariantCulture);
        var filters = new List<string> { $"fps={fpsText}" };

        if (!options.UseOriginalSize && options.CustomWidth > 0 && options.CustomHeight > 0)
        {
            var scale = options.KeepAspectRatio
                ? $"scale={options.CustomWidth}:{options.CustomHeight}:force_original_aspect_ratio=decrease"
                : $"scale={options.CustomWidth}:{options.CustomHeight}";
            filters.Add(scale);
        }

        return string.Join(",", filters);
    }

    private static async Task<FrameExtractResult> BuildIndexesAsync(FrameExtractOptions options, string extension, double effectiveFps)
    {
        var files = Directory.EnumerateFiles(options.OutputDirectory, $"{options.Prefix}_*.{extension}")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var frames = new List<FrameIndexItem>();
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            frames.Add(new FrameIndexItem
            {
                Index = index + 1,
                FileName = Path.GetFileName(file),
                Time = effectiveFps > 0 ? TimeSpan.FromSeconds(index / effectiveFps) : TimeSpan.FromMilliseconds(index * options.IntervalMs),
                RelativePath = ToRelativePath(options.ProjectDirectory, file)
            });
        }

        var csvPath = Path.Combine(options.OutputDirectory, "frames-index.csv");
        var mdPath = Path.Combine(options.OutputDirectory, "frames-index.md");

        await WriteCsvAsync(csvPath, frames);
        await WriteMarkdownAsync(mdPath, options, frames);

        return new FrameExtractResult
        {
            OutputDirectory = options.OutputDirectory,
            FrameIndexCsvPath = csvPath,
            FrameIndexMarkdownPath = mdPath,
            TotalFrames = frames.Count,
            IntervalMs = options.IntervalMs,
            EffectiveFps = effectiveFps,
            Format = options.Format,
            Frames = frames
        };
    }

    private static async Task WriteCsvAsync(string path, IReadOnlyList<FrameIndexItem> frames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("index,fileName,time,relativePath");
        foreach (var frame in frames)
        {
            builder.AppendLine($"{frame.Index},{Csv(frame.FileName)},{Csv(TimeUtil.FormatTime(frame.Time))},{Csv(frame.RelativePath)}");
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8);
    }

    private static async Task WriteMarkdownAsync(string path, FrameExtractOptions options, IReadOnlyList<FrameIndexItem> frames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Frames Index");
        builder.AppendLine();
        builder.AppendLine("## 基本信息");
        builder.AppendLine();
        builder.AppendLine($"- 来源视频：{options.VideoPath}");
        builder.AppendLine($"- 抽帧间隔：{options.IntervalMs} ms");
        builder.AppendLine($"- 总截图数：{frames.Count}");
        builder.AppendLine($"- 输出目录：{options.OutputDirectory}");
        builder.AppendLine();
        builder.AppendLine("## 截图列表");
        builder.AppendLine();
        builder.AppendLine("| 序号 | 文件名 | 对应时间 | 相对路径 |");
        builder.AppendLine("|---|---|---|---|");

        foreach (var frame in frames.Take(500))
        {
            builder.AppendLine($"| {frame.Index} | {frame.FileName} | {TimeUtil.FormatTime(frame.Time)} | {frame.RelativePath} |");
        }

        if (frames.Count > 500)
        {
            builder.AppendLine();
            builder.AppendLine($"> 截图数量超过 500，Markdown 只列出前 500 张，完整列表见 frames-index.csv。");
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8);
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string ToRelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static bool IsPng(string format)
    {
        return string.Equals(format, "PNG", StringComparison.OrdinalIgnoreCase);
    }

    private static int MapJpegQuality(int qualityPercent)
    {
        return qualityPercent switch
        {
            >= 90 => 2,
            >= 80 => 3,
            >= 70 => 5,
            >= 60 => 7,
            _ => 10
        };
    }
}
