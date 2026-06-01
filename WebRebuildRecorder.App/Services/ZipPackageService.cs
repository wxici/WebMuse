using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class ZipPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;

    private readonly AppLogger _logger;

    public ZipPackageService(AppLogger logger)
    {
        _logger = logger;
    }

    public Task<string> CreateFramesZipAsync(RebuildProject project, FrameExtractResult result)
    {
        var zipPath = Path.Combine(project.PackageDirectory, $"{project.Slug}_{project.RecordingId}_{result.IntervalMs}ms_frames.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8);
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDirectory(archive, result.OutputDirectory, project.ProjectDirectory, added);
        AddDirectory(archive, Path.Combine(project.FramesDirectory, "manual"), project.ProjectDirectory, added);

        _logger.Info($"截图压缩包已生成：{zipPath}");
        return Task.FromResult(zipPath);
    }

    public Task<string> CreateObservationPackageAsync(RebuildProject project, FrameExtractResult? frameResult, string? observationMarkdownPath)
    {
        var interval = frameResult?.IntervalMs ?? 0;
        var zipPath = Path.Combine(project.PackageDirectory, $"{project.Slug}_{project.RecordingId}_{interval}ms_observation-package.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8);
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFileIfExists(archive, Path.Combine(project.ProjectDirectory, "project.json"), project.ProjectDirectory, added);
        AddDirectory(archive, project.InputDirectory, project.ProjectDirectory, added);
        AddDirectory(archive, project.ActionsDirectory, project.ProjectDirectory, added);
        AddDirectory(archive, project.MarkersDirectory, project.ProjectDirectory, added);
        AddDirectory(archive, project.ConfigDirectory, project.ProjectDirectory, added);
        AddDirectory(archive, project.MarkdownDirectory, project.ProjectDirectory, added);

        AddFileIfExists(archive, Path.Combine(project.RecordingDirectory, $"recording-info_{project.RecordingId}.json"), project.ProjectDirectory, added);
        AddFileIfExists(archive, Path.Combine(project.RecordingDirectory, "recording-info.json"), project.ProjectDirectory, added);
        if (!string.IsNullOrWhiteSpace(observationMarkdownPath))
        {
            AddFileIfExists(archive, observationMarkdownPath, project.ProjectDirectory, added);
        }

        if (frameResult is not null)
        {
            AddFileIfExists(archive, frameResult.FrameIndexCsvPath, project.ProjectDirectory, added);
            AddFileIfExists(archive, frameResult.FrameIndexMarkdownPath, project.ProjectDirectory, added);
            AddFileIfExists(archive, Path.Combine(project.PackageDirectory, $"{project.Slug}_{project.RecordingId}_{frameResult.IntervalMs}ms_frames.zip"), project.ProjectDirectory, added);
        }

        _logger.Info($"观察资料包已生成：{zipPath}");
        return Task.FromResult(zipPath);
    }

    public async Task<ChatGptPackageResult> CreateChatGptPackageAsync(
        RebuildProject project,
        FrameExtractResult frameResult,
        string observationMarkdownPath)
    {
        var deliveryProfile = await LoadDeliveryProfileAsync(project);
        var zipPath = Path.Combine(project.PackageDirectory, $"{project.Slug}_{project.RecordingId}_{frameResult.IntervalMs}ms_chatgpt-package.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        var selectedFrames = SelectFramesForChatGpt(project, frameResult, deliveryProfile);
        var indexMarkdown = BuildSelectedFramesIndexMarkdown(project, frameResult, selectedFrames, deliveryProfile);
        var indexCsv = BuildSelectedFramesIndexCsv(selectedFrames);
        var selectedIndexMarkdownPath = Path.Combine(project.PackageDirectory, "selected-frames-index.md");
        var selectedIndexCsvPath = Path.Combine(project.PackageDirectory, "selected-frames-index.csv");
        await File.WriteAllTextAsync(selectedIndexMarkdownPath, indexMarkdown, Encoding.UTF8);
        await File.WriteAllTextAsync(selectedIndexCsvPath, indexCsv, Encoding.UTF8);

        var promptText = BuildChatGptPrompt();

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8))
        {
            AddStringEntry(archive, "README_FOR_CHATGPT.md", BuildReadmeForChatGpt());
            AddStringEntry(archive, "chatgpt-prompt.md", promptText);
            AddStringEntry(archive, "selected-frames-index.md", indexMarkdown);
            AddStringEntry(archive, "selected-frames-index.csv", indexCsv);
            AddFileWithEntryName(archive, observationMarkdownPath, "observation.md");
            AddFileWithEntryName(archive, frameResult.FrameIndexMarkdownPath, "frames-index.md");
            AddFileWithEntryName(archive, frameResult.FrameIndexCsvPath, "frames-index.csv");
            AddFileWithEntryName(archive, Path.Combine(project.ActionsDirectory, "action-log.md"), "action-log.md");
            AddFileWithEntryName(archive, Path.Combine(project.ActionsDirectory, "action-log.jsonl"), "action-log.jsonl");
            AddFileWithEntryName(archive, Path.Combine(project.ActionsDirectory, "dom-summary.json"), "dom-summary.json");
            AddFileWithEntryName(archive, Path.Combine(project.ActionsDirectory, "interactive-targets.md"), "interactive-targets.md");
            AddFileWithEntryName(archive, Path.Combine(project.ActionsDirectory, "interactive-targets.json"), "interactive-targets.json");
            AddFileWithEntryName(archive, Path.Combine(project.MarkersDirectory, "markers.md"), "markers.md");
            AddFileWithEntryName(archive, project.UserIntentAssetManifestMarkdownPath, "user-intent-assets.md");
            AddFileWithEntryName(archive, project.UserIntentAssetManifestJsonPath, "user-intent-assets.json");
            AddFileWithEntryName(archive, Path.Combine(project.ProjectDirectory, "project.json"), "project.json");
            AddUserIntentImages(archive, project);

            foreach (var selected in selectedFrames)
            {
                var source = Path.Combine(project.ProjectDirectory, selected.Frame.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                AddCompressedSelectedFrame(archive, source, selected.PackagePath, deliveryProfile);
            }
        }

        var info = new FileInfo(zipPath);
        var result = new ChatGptPackageResult
        {
            ZipPath = zipPath,
            ZipSizeBytes = info.Exists ? info.Length : 0,
            SelectedFrameCount = selectedFrames.Count,
            PromptText = promptText,
            SelectedFramesIndexMarkdownPath = selectedIndexMarkdownPath,
            SelectedFramesIndexCsvPath = selectedIndexCsvPath
        };

        _logger.Info($"ChatGPT 上传包已生成：{zipPath} 精选帧={result.SelectedFrameCount} 大小={TimeUtil.FormatFileSize(result.ZipSizeBytes)}");
        return result;
    }

    private static async Task<DeliveryProfile> LoadDeliveryProfileAsync(RebuildProject project)
    {
        var path = Path.Combine(project.ConfigDirectory, "delivery-profile.json");
        if (!File.Exists(path))
        {
            var fallback = new DeliveryProfile();
            await WriteJsonAsync(path, fallback);
            return fallback;
        }

        await using var stream = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<DeliveryProfile>(stream, JsonOptions) ?? new DeliveryProfile();
        await WriteJsonAsync(path, profile);
        return profile;
    }

    private static void AddDirectory(ZipArchive archive, string directory, string root, HashSet<string> added)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            AddFileIfExists(archive, file, root, added);
        }
    }

    private static void AddFileIfExists(ZipArchive archive, string? path, string root, HashSet<string> added)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var entryName = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (!added.Add(entryName))
        {
            return;
        }

        archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
    }

    private static void AddFileWithEntryName(ZipArchive archive, string? path, string entryName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        archive.CreateEntryFromFile(path, entryName.Replace('\\', '/'), CompressionLevel.Optimal);
    }

    private static void AddUserIntentImages(ZipArchive archive, RebuildProject project)
    {
        if (!Directory.Exists(project.UserIntentDirectory))
        {
            return;
        }

        foreach (var fieldDirectory in Directory.EnumerateDirectories(project.UserIntentDirectory))
        {
            var field = Path.GetFileName(fieldDirectory);
            foreach (var file in Directory.EnumerateFiles(fieldDirectory))
            {
                var entryName = $"user-intent-images/{field}/{Path.GetFileName(file)}";
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            }
        }
    }

    private static void AddStringEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void AddCompressedSelectedFrame(ZipArchive archive, string source, string entryName, DeliveryProfile profile)
    {
        if (!File.Exists(source))
        {
            return;
        }

        try
        {
            var bitmap = LoadBitmap(source);
            BitmapSource output = bitmap;
            if (profile.UploadImageMaxWidth > 0 && bitmap.PixelWidth > profile.UploadImageMaxWidth)
            {
                var scale = profile.UploadImageMaxWidth / (double)bitmap.PixelWidth;
                output = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
            }

            var encoder = new JpegBitmapEncoder
            {
                QualityLevel = Math.Clamp(profile.UploadImageJpegQuality, 1, 100)
            };
            encoder.Frames.Add(BitmapFrame.Create(output));

            var entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            encoder.Save(entryStream);
        }
        catch
        {
            archive.CreateEntryFromFile(source, entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        }
    }

    private static BitmapSource LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static List<SelectedFrameItem> SelectFramesForChatGpt(RebuildProject project, FrameExtractResult result, DeliveryProfile profile)
    {
        var maxSelected = Math.Max(1, profile.MaxSelectedFrames);
        var selected = new Dictionary<int, string>();
        AddSelectedFrame(result, selected, 1, "first-screen");

        foreach (var marker in ReadMarkerEvents(project))
        {
            AddFrameWindow(result, marker.Time, selected, Math.Max(0, profile.MarkerContextFrameCount), $"marker: {marker.Type}");
        }

        if (profile.IncludeActionContextFrames)
        {
            foreach (var action in ReadActionEvents(project))
            {
                AddFrameWindow(result, action.Time, selected, Math.Max(0, profile.ActionContextFrameCount), $"action: {action.Type}");
            }
        }

        if (selected.Count > maxSelected)
        {
            selected = TakeEvenly(selected, maxSelected);
        }

        var remaining = maxSelected - selected.Count;
        if (remaining > 0)
        {
            foreach (var index in UniformFrameIndexes(result.TotalFrames, remaining))
            {
                if (!selected.ContainsKey(index))
                {
                    selected[index] = "uniform-sample";
                }
            }
        }

        while (selected.Count > maxSelected)
        {
            selected.Remove(selected.Keys.Max());
        }

        return result.Frames
            .Where(frame => selected.ContainsKey(frame.Index))
            .OrderBy(frame => frame.Time)
            .Select(frame =>
            {
                var packageFileName = Path.GetFileNameWithoutExtension(frame.FileName) + ".jpg";
                return new SelectedFrameItem(frame, $"selected-frames/{packageFileName}", selected[frame.Index]);
            })
            .ToList();
    }

    private static IEnumerable<TimedEvent> ReadMarkerEvents(RebuildProject project)
    {
        var path = Path.Combine(project.MarkersDirectory, "markers.md");
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (!line.StartsWith('|') || line.Contains("---", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length > 2 && TryParseTime(parts[1], out var time))
            {
                yield return new TimedEvent(parts[2], time);
            }
        }
    }

    private static IEnumerable<TimedEvent> ReadActionEvents(RebuildProject project)
    {
        var path = Path.Combine(project.ActionsDirectory, "action-log.jsonl");
        if (!File.Exists(path))
        {
            yield break;
        }

        var keyTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hover",
            "scroll",
            "hotspot-click",
            "manual-control-start",
            "manual-control-end",
            "manual-marker",
            "manual-screenshot",
            "recording-start",
            "recording-stop",
            "recording-start-before-open-url",
            "mouse-manual-detected"
        };

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : string.Empty;
                if (string.IsNullOrWhiteSpace(type) || !keyTypes.Contains(type))
                {
                    continue;
                }

                var timeText = root.TryGetProperty("videoTime", out var videoTime)
                    ? videoTime.GetString()
                    : null;

                if (TryParseTime(timeText, out var time))
                {
                    yield return new TimedEvent(type, time);
                }
            }
        }
    }

    private static void AddFrameWindow(FrameExtractResult result, TimeSpan time, Dictionary<int, string> selected, int radius, string reason)
    {
        var nearest = result.Frames
            .OrderBy(frame => Math.Abs((frame.Time - time).TotalMilliseconds))
            .FirstOrDefault();

        if (nearest is null)
        {
            return;
        }

        for (var index = Math.Max(1, nearest.Index - radius); index <= Math.Min(result.TotalFrames, nearest.Index + radius); index++)
        {
            AddSelectedFrame(result, selected, index, reason);
        }
    }

    private static void AddSelectedFrame(FrameExtractResult result, Dictionary<int, string> selected, int index, string reason)
    {
        if (index < 1 || index > result.TotalFrames)
        {
            return;
        }

        if (selected.TryGetValue(index, out var existing))
        {
            if (!existing.Contains(reason, StringComparison.OrdinalIgnoreCase))
            {
                selected[index] = existing + "; " + reason;
            }
            return;
        }

        selected[index] = reason;
    }

    private static Dictionary<int, string> TakeEvenly(Dictionary<int, string> selected, int count)
    {
        var keys = selected.Keys.OrderBy(index => index).ToList();
        var kept = new Dictionary<int, string>();
        if (count <= 0 || keys.Count == 0)
        {
            return kept;
        }

        if (keys.Count <= count)
        {
            return selected;
        }

        for (var i = 0; i < count; i++)
        {
            var position = (int)Math.Round(i * (keys.Count - 1) / (double)(count - 1));
            var key = keys[position];
            kept[key] = selected[key];
        }

        return kept;
    }

    private static IEnumerable<int> UniformFrameIndexes(int totalFrames, int count)
    {
        if (totalFrames <= 0 || count <= 0)
        {
            yield break;
        }

        if (count == 1)
        {
            yield return 1;
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            yield return Math.Clamp((int)Math.Round(1 + i * (totalFrames - 1) / (double)(count - 1)), 1, totalFrames);
        }
    }

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value) || value == "--")
        {
            return false;
        }

        return TimeSpan.TryParse(value, out time);
    }

    private static string BuildSelectedFramesIndexMarkdown(
        RebuildProject project,
        FrameExtractResult frameResult,
        IReadOnlyList<SelectedFrameItem> selectedFrames,
        DeliveryProfile profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Selected Frames Index / 精选截图索引");
        builder.AppendLine();
        builder.AppendLine("## 基本信息");
        builder.AppendLine();
        builder.AppendLine($"- 来源视频：{project.RecordingFilePath}");
        builder.AppendLine($"- 原始抽帧目录：{frameResult.OutputDirectory}");
        builder.AppendLine($"- 全量截图数：{frameResult.TotalFrames}");
        builder.AppendLine($"- 精选截图数：{selectedFrames.Count}");
        builder.AppendLine("- 精选规则：首屏、marker 附近、关键 action 附近、均匀采样");
        builder.AppendLine($"- 精选截图上限：{profile.MaxSelectedFrames}");
        builder.AppendLine("- selected-frames 目录：selected-frames/");
        builder.AppendLine();
        builder.AppendLine("## 精选截图列表");
        builder.AppendLine();
        builder.AppendLine("| 序号 | 原始帧 | 对应时间 | 包内路径 | 选择原因 |");
        builder.AppendLine("|---|---|---|---|---|");

        for (var i = 0; i < selectedFrames.Count; i++)
        {
            var selected = selectedFrames[i];
            builder.AppendLine($"| {i + 1} | {selected.Frame.FileName} | {TimeUtil.FormatTime(selected.Frame.Time)} | {selected.PackagePath} | {EscapeMarkdown(selected.Reason)} |");
        }

        return builder.ToString();
    }

    private static string BuildSelectedFramesIndexCsv(IReadOnlyList<SelectedFrameItem> selectedFrames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("index,originalFrame,time,packagePath,reason");
        for (var i = 0; i < selectedFrames.Count; i++)
        {
            var selected = selectedFrames[i];
            builder.AppendLine($"{i + 1},{Csv(selected.Frame.FileName)},{Csv(TimeUtil.FormatTime(selected.Frame.Time))},{Csv(selected.PackagePath)},{Csv(selected.Reason)}");
        }

        return builder.ToString();
    }

    private static string BuildReadmeForChatGpt()
    {
        return """
# README_FOR_CHATGPT

请先阅读本文件，然后阅读 observation.md 和 selected-frames-index.md。

本压缩包用于分析参考网站的网页结构、首屏构图、滚动节奏、hover / 点击 / 菜单 / 弹层 / 转场动效，以及可借鉴的设计语言。

## 文件说明

- observation.md：网页观察主文档，包含录屏信息、抽帧信息、动作日志摘要、重点片段和分析任务。
- selected-frames-index.md：本 zip 内实际包含的精选截图索引，分析截图时请优先参考它。
- selected-frames/：本次上传给 ChatGPT 的精选截图。
- frames-index.md：本地完整抽帧索引，不代表 zip 内全部图片都存在。
- action-log.md：录屏过程中的动作日志。
- markers.md：人工标记的重点片段。
- user-intent.md：用户主观感受与目标动效；如果用户未填写，会包含默认说明。若本 zip 内不存在该文件，请参考 observation.md 和动作日志继续分析。
- asset-manifest.md / asset-manifest.json：用户素材清单；如果本项目导入了素材，请结合该文件理解用户素材储备。若本 zip 内不存在该文件，说明本包未携带素材清单。
- interactive-targets.md：网页交互目标清单，包括链接、按钮、菜单、CTA、弹层入口等。
- user-intent-assets.md：用户粘贴或上传的重点参考图片清单。
- user-intent-images/：用户手动添加的重点图片。

## 建议阅读顺序

1. observation.md
2. selected-frames-index.md
3. user-intent.md
4. markers.md
5. action-log.md
6. interactive-targets.md
7. user-intent-assets.md
8. asset-manifest.md
9. selected-frames/

## 网页交互目标

如果压缩包中包含 interactive-targets.md，请结合该文件理解程序识别到的网页交互目标。

- interactive-targets.md：可交互元素清单，包括链接、按钮、菜单、CTA、弹层入口等。
- interactive-targets.json：结构化交互目标数据。
- 如果该文件显示 unavailable，说明本次为外部浏览器坐标式观察，不包含 DOM 级交互采集。

## DOM 交互采集状态说明

如果 interactive-targets.md 中显示 unavailable，说明本次没有真实 DOM 交互目标。
这通常是因为当前使用外部浏览器录屏模式，程序无法读取网页 DOM。

这种情况下，请主要参考：

1. selected-frames/
2. observation.md
3. action-log.md
4. user-intent-images/

不要误以为程序已经完整点击或 hover 了所有网页元素。

## 用户重点参考图片

如果压缩包中包含 user-intent-images/，请优先查看这些图片。

这些图片是用户手动粘贴或上传的重点区域，可能代表：

- 用户喜欢的页面局部；
- 用户希望复刻的动效状态；
- 用户希望避免照搬的设计；
- 用户希望重点分析的视觉区域。

分析时，请把这些图片与 selected-frames/ 中的自动抽帧结合起来，不要只看自动抽帧。

## 分析重点

请重点分析：

1. 页面整体结构；
2. 首屏视觉重心；
3. 导航、CTA、图片、文字之间的空间关系；
4. 滚动节奏；
5. hover / 点击 / 菜单 / 弹层 / 转场动效；
6. 哪些设计语言值得借鉴；
7. 哪些部分不应照搬；
8. 如果用于用户自己的网页，应如何转译；
9. 最后生成给 Codex 的网页重构任务指令草案。

## 注意

本 zip 通常只包含精选截图，不包含全部抽帧，也不默认包含录屏视频。
如需完整逐帧检查，请参考本地项目目录中的 frames/ 和 recording/。
""";
    }

    private static string BuildChatGptPrompt()
    {
        return """
请解压我上传的 zip，先阅读 README_FOR_CHATGPT.md、observation.md、selected-frames-index.md、interactive-targets.md 和 user-intent-assets.md。

然后结合 selected-frames/ 中的截图、user-intent-images/ 中的用户重点参考图，以及 interactive-targets.md 中的网页交互目标，分析这个参考网站的页面结构、动效、视觉语言和可借鉴设计点。

请不要只描述页面内容，而要输出：

1. 页面结构拆解；
2. 首屏构图分析；
3. 视觉重心与留白方式；
4. 导航、CTA、图片、文字的空间关系；
5. 滚动节奏；
6. hover / 点击 / 菜单 / 弹层 / 转场动效；
7. 可借鉴部分；
8. 不应照搬部分；
9. 如果用于用户自己的项目，应如何转译；
10. 最后生成一份给 Codex 的网页重构任务指令草案。

如果 user-intent.md 中显示用户未填写主观意图，请不要中止分析。请先根据截图、observation.md、selected-frames-index.md、interactive-targets.md 和 action-log.md 做基础分析，并在最后列出需要用户补充的问题。
""";
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|").Replace(Environment.NewLine, " ");
    }

    private sealed record TimedEvent(string Type, TimeSpan Time);

    private sealed record SelectedFrameItem(FrameIndexItem Frame, string PackagePath, string Reason);
}
