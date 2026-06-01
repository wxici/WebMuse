using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class TemplateObservationMarkdownGenerator : IObservationMarkdownGenerator
{
    private readonly AppLogger _logger;

    public TemplateObservationMarkdownGenerator(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task GenerateAsync(ObservationContext context)
    {
        var project = context.Project;
        var outputPath = string.IsNullOrWhiteSpace(context.OutputPath)
            ? Path.Combine(project.MarkdownDirectory, $"{project.Slug}_{project.RecordingId}_{context.FrameResult?.IntervalMs ?? context.FrameOptions?.IntervalMs ?? 500}ms_observation.md")
            : context.OutputPath;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var template = await LoadTemplateAsync(context);
        var markdown = template is null
            ? BuildMarkdown(context)
            : ApplyPlaceholders(template, BuildPlaceholders(context));
        await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8);
        _logger.Info($"Observation markdown generated: {outputPath}");
    }

    private static async Task<string?> LoadTemplateAsync(ObservationContext context)
    {
        var configured = context.MarkdownOptions.ObservationTemplatePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var candidates = new List<string>();
        if (Path.IsPathRooted(configured))
        {
            candidates.Add(configured);
        }
        else
        {
            candidates.Add(Path.Combine(context.Project.ProjectDirectory, configured));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, configured));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return await File.ReadAllTextAsync(candidate, Encoding.UTF8);
            }
        }

        return null;
    }

    private static string ApplyPlaceholders(string template, IReadOnlyDictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
        {
            template = template.Replace("{{" + key + "}}", value);
        }

        return template;
    }

    private static Dictionary<string, string> BuildPlaceholders(ObservationContext context)
    {
        var project = context.Project;
        var frameResult = context.FrameResult;
        var frameOptions = context.FrameOptions;
        var interval = frameResult?.IntervalMs ?? frameOptions?.IntervalMs ?? 0;

        return new Dictionary<string, string>
        {
            ["ProjectName"] = project.ProjectName,
            ["ProjectSlug"] = project.Slug,
            ["ReferenceUrl"] = project.ReferenceUrl,
            ["ProjectDirectory"] = project.ProjectDirectory,
            ["RecordingFile"] = ToRelative(project.ProjectDirectory, project.RecordingFilePath),
            ["RecordingId"] = project.RecordingId,
            ["FramesDirectory"] = ToRelative(project.ProjectDirectory, frameResult?.OutputDirectory ?? string.Empty),
            ["FrameInterval"] = interval.ToString(),
            ["IntervalMs"] = interval.ToString(),
            ["ImageFormat"] = frameResult?.Format ?? frameOptions?.Format ?? string.Empty,
            ["ImageQuality"] = frameOptions?.JpegQuality.ToString() ?? string.Empty,
            ["GeneratedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["RecordingInfo"] = BuildRecordingInfo(context.RecordingInfo),
            ["RecordingStartMode"] = BuildRecordingStartModeInfo(context.RecordingInfo),
            ["FloatingToolbarInfo"] = BuildFloatingToolbarInfo(context.RecordingInfo),
            ["AutoObserveConfiguration"] = BuildAutoObserveConfiguration(context),
            ["ActualObserveMode"] = BuildActualObserveMode(context),
            ["AutomaticManualNotes"] = BuildAutomaticManualNotes(),
            ["SelectedFramesNotes"] = BuildSelectedFramesNotes(),
            ["UserNotes"] = ReadFileOrEmpty(Path.Combine(project.InputDirectory, "user-notes.md")),
            ["StyleNotes"] = ReadFileOrEmpty(Path.Combine(project.InputDirectory, "style-notes.md")),
            ["Markers"] = context.MarkdownOptions.IncludeMarkers ? ReadFileOrEmpty(Path.Combine(project.MarkersDirectory, "markers.md")) : "（已禁用）",
            ["ActionLog"] = context.MarkdownOptions.IncludeActionLog ? ReadFileOrEmpty(Path.Combine(project.ActionsDirectory, "action-log.md")) : "（已禁用）",
            ["InteractiveTargets"] = ReadFileOrEmpty(Path.Combine(project.ActionsDirectory, "interactive-targets.md")),
            ["UserIntentAssets"] = BuildUserIntentAssetNotes(project),
            ["FrameIndexTable"] = BuildFrameIndexTable(context),
            ["SegmentIndex"] = BuildSegmentIndex(context),
            ["ExtractionInfo"] = BuildExtractionInfo(context),
            ["ChatGptPrompt"] = context.MarkdownOptions.IncludeChatGptPrompt ? ChatGptPrompt() : string.Empty,
            ["CodexReservedSection"] = context.MarkdownOptions.IncludeCodexReservedSection ? "## 后续 Codex 任务预留区\n" : string.Empty,
            ["ManualObservationSection"] = "### 人工观察备注\n\n-",
            ["RecordingControlInfo"] = BuildAutoObserveConfiguration(context)
        };
    }

    private static string BuildMarkdown(ObservationContext context)
    {
        var project = context.Project;
        var recording = context.RecordingInfo;
        var frameResult = context.FrameResult;
        var frameOptions = context.FrameOptions;
        var interval = frameResult?.IntervalMs ?? frameOptions?.IntervalMs ?? 0;
        var actionStats = ReadActionStats(project);
        var actualMode = ResolveActualObserveMode(recording, actionStats);

        var builder = new StringBuilder();
        builder.AppendLine("# 网站观察资料包");
        builder.AppendLine();
        builder.AppendLine("## 项目信息");
        builder.AppendLine();
        builder.AppendLine($"- 项目名称：{project.ProjectName}");
        builder.AppendLine($"- 项目 slug：{project.Slug}");
        builder.AppendLine($"- 参考网站：{project.ReferenceUrl}");
        builder.AppendLine($"- 项目目录：{project.ProjectDirectory}");
        builder.AppendLine($"- 录屏文件：{ToRelative(project.ProjectDirectory, project.RecordingFilePath)}");
        builder.AppendLine($"- 录屏编号：{project.RecordingId}");
        builder.AppendLine($"- 抽帧目录：{ToRelative(project.ProjectDirectory, frameResult?.OutputDirectory ?? string.Empty)}");
        builder.AppendLine($"- 抽帧间隔：{interval} ms");
        builder.AppendLine($"- 图片格式：{frameResult?.Format ?? frameOptions?.Format ?? string.Empty}");
        builder.AppendLine($"- 图片质量：{frameOptions?.JpegQuality.ToString() ?? string.Empty}");
        builder.AppendLine($"- 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        builder.AppendLine("## 录屏信息");
        builder.AppendLine();
        builder.AppendLine(BuildRecordingInfo(recording));
        builder.AppendLine();

        builder.AppendLine("## 录屏启动方式");
        builder.AppendLine();
        builder.AppendLine($"- 录制启动模式：{recording.RecordingStartMode}");
        builder.AppendLine($"- 是否先录屏再打开网站：{YesNo(recording.RecordingStartedBeforeOpenUrl)}");
        builder.AppendLine($"- 录屏启动时间：{recording.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine($"- 打开 URL 时间：{(recording.OpenUrlTime is null ? "--" : recording.OpenUrlTime.Value.ToString("yyyy-MM-dd HH:mm:ss.fff"))}");
        builder.AppendLine($"- 是否包含网页加载/首屏动画：{YesNo(recording.IncludesInitialLoadAnimation)}");
        builder.AppendLine();

        builder.AppendLine("## 工具条信息");
        builder.AppendLine();
        builder.AppendLine($"- 是否使用悬浮工具条：{YesNo(recording.UsedFloatingToolbar)}");
        builder.AppendLine($"- 工具条模式：{recording.FloatingToolbarMode}");
        builder.AppendLine($"- 工具条位置：{recording.FloatingToolbarLeft:0},{recording.FloatingToolbarTop:0}");
        builder.AppendLine($"- 工具条是否可能遮挡画面：{YesNo(recording.FloatingToolbarMayOcclude)}");
        builder.AppendLine();

        builder.AppendLine("## 自动观察配置");
        builder.AppendLine();
        builder.AppendLine($"- 自动观察速度：{context.ActionProfile.SpeedPreset}");
        builder.AppendLine($"- 自动观察模式：{context.ActionProfile.ObserveMode}");
        builder.AppendLine($"- 最大自动观察时长：{context.ActionProfile.AutoObserveDurationSeconds}s");
        builder.AppendLine($"- 实际录屏时长：{TimeUtil.FormatTime(TimeSpan.FromSeconds(recording.DurationSeconds))}");
        builder.AppendLine($"- 是否发生人工接管：{YesNo(recording.HadManualTakeover)}");
        builder.AppendLine($"- 人工接管时间点：{string.Join(", ", actionStats.ManualTakeoverTimes.DefaultIfEmpty("--"))}");
        builder.AppendLine($"- 自动动作数量：{actionStats.AutoActionCount}");
        builder.AppendLine($"- 鼠标移动次数：{actionStats.MouseMoveCount}");
        builder.AppendLine($"- 滚动次数：{actionStats.ScrollCount}");
        builder.AppendLine($"- hover 次数：{actionStats.HoverCount}");
        builder.AppendLine();

        builder.AppendLine("## 实际观察模式");
        builder.AppendLine();
        builder.AppendLine($"- 配置观察模式：{context.ActionProfile.ObserveMode}");
        builder.AppendLine($"- 实际观察模式：{actualMode}");
        builder.AppendLine($"- 自动控制时长：{EstimateAutoDuration(recording, actionStats):0.0}s");
        builder.AppendLine($"- 人工接管时长：{EstimateManualDuration(recording, actionStats):0.0}s");
        builder.AppendLine($"- 人工接管占比：{ManualRatio(recording, actionStats):P0}");
        builder.AppendLine();

        builder.AppendLine("## 自动与人工观察说明");
        builder.AppendLine();
        builder.AppendLine("本资料包可能由自动观察、人工接管或两者混合生成。");
        builder.AppendLine();
        builder.AppendLine("- 自动观察适合记录页面结构和滚动节奏。");
        builder.AppendLine("- 人工接管适合捕捉菜单、CTA、主题切换、弹层和页面转场。");
        builder.AppendLine("- 如果本包为人工主导，请优先参考 markers.md 和 selected-frames-index.md。");
        builder.AppendLine();

        builder.AppendLine("## Selected Frames 说明");
        builder.AppendLine();
        builder.AppendLine("ChatGPT 上传包不包含全部抽帧，只包含 selected-frames/ 中的精选截图。");
        builder.AppendLine("分析截图时，请优先参考 selected-frames-index.md。");
        builder.AppendLine();

        builder.AppendLine("## 用户重点参考图片");
        builder.AppendLine();
        builder.AppendLine("本项目可能包含用户粘贴或上传的设计意图图片。");
        builder.AppendLine("这些图片代表用户主观上认为重要的页面局部或目标动效。");
        builder.AppendLine();
        builder.AppendLine("请结合：");
        builder.AppendLine();
        builder.AppendLine("1. user-intent.md");
        builder.AppendLine("2. user-intent-assets.md");
        builder.AppendLine("3. user-intent-images/");
        builder.AppendLine("4. selected-frames/");
        builder.AppendLine();
        builder.AppendLine(BuildUserIntentAssetNotes(project));
        builder.AppendLine();

        builder.AppendLine("## 网页交互目标");
        builder.AppendLine();
        builder.AppendLine(ReadFileOrEmpty(Path.Combine(project.ActionsDirectory, "interactive-targets.md")));
        builder.AppendLine();

        builder.AppendLine("## 用户说明");
        builder.AppendLine();
        builder.AppendLine(ReadFileOrEmpty(Path.Combine(project.InputDirectory, "user-notes.md")));
        builder.AppendLine();

        builder.AppendLine("## 风格说明");
        builder.AppendLine();
        builder.AppendLine(ReadFileOrEmpty(Path.Combine(project.InputDirectory, "style-notes.md")));
        builder.AppendLine();

        builder.AppendLine("## 重点标记");
        builder.AppendLine();
        builder.AppendLine(context.MarkdownOptions.IncludeMarkers ? ReadFileOrEmpty(Path.Combine(project.MarkersDirectory, "markers.md")) : "（已禁用）");
        builder.AppendLine();

        builder.AppendLine("## 动作日志");
        builder.AppendLine();
        builder.AppendLine(context.MarkdownOptions.IncludeActionLog ? ReadFileOrEmpty(Path.Combine(project.ActionsDirectory, "action-log.md")) : "（已禁用）");
        builder.AppendLine();

        builder.AppendLine("## 抽帧索引预览");
        builder.AppendLine();
        builder.AppendLine(BuildFrameIndexTable(context));
        builder.AppendLine();

        builder.AppendLine("## 重点片段索引");
        builder.AppendLine();
        builder.AppendLine(BuildSegmentIndex(context));
        builder.AppendLine();

        builder.AppendLine("## 抽帧信息");
        builder.AppendLine();
        builder.AppendLine(BuildExtractionInfo(context));
        builder.AppendLine();

        if (context.MarkdownOptions.IncludeChatGptPrompt)
        {
            builder.AppendLine("## 给 ChatGPT 的分析提示");
            builder.AppendLine();
            builder.AppendLine(ChatGptPrompt());
            builder.AppendLine();
        }

        if (context.MarkdownOptions.IncludeCodexReservedSection)
        {
            builder.AppendLine("## 后续 Codex 任务预留区");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildRecordingInfo(RecordingInfo recording)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"- 视频路径：{recording.VideoPath}");
        builder.AppendLine($"- 时长：{TimeUtil.FormatTime(TimeSpan.FromSeconds(recording.DurationSeconds))}");
        builder.AppendLine($"- 文件大小：{TimeUtil.FormatFileSize(recording.FileSizeBytes)}");
        builder.AppendLine($"- 分辨率：{recording.Width}x{recording.Height}");
        builder.AppendLine($"- 录屏区域：{recording.RecordingArea}");
        builder.AppendLine($"- 浏览器：{recording.Browser}");
        builder.AppendLine($"- 是否自动观察：{YesNo(recording.IsAutoObserve)}");
        builder.AppendLine($"- 是否人工接管：{YesNo(recording.HadManualTakeover)}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildRecordingStartModeInfo(RecordingInfo recording)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"- 录制启动模式：{recording.RecordingStartMode}");
        builder.AppendLine($"- 是否先录屏再打开网站：{YesNo(recording.RecordingStartedBeforeOpenUrl)}");
        builder.AppendLine($"- 录屏启动时间：{recording.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine($"- 打开 URL 时间：{(recording.OpenUrlTime is null ? "--" : recording.OpenUrlTime.Value.ToString("yyyy-MM-dd HH:mm:ss.fff"))}");
        builder.AppendLine($"- 是否包含网页加载/首屏动画：{YesNo(recording.IncludesInitialLoadAnimation)}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildFloatingToolbarInfo(RecordingInfo recording)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"- 是否使用悬浮工具条：{YesNo(recording.UsedFloatingToolbar)}");
        builder.AppendLine($"- 工具条模式：{recording.FloatingToolbarMode}");
        builder.AppendLine($"- 工具条位置：{recording.FloatingToolbarLeft:0},{recording.FloatingToolbarTop:0}");
        builder.AppendLine($"- 工具条是否可能遮挡画面：{YesNo(recording.FloatingToolbarMayOcclude)}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildAutoObserveConfiguration(ObservationContext context)
    {
        var stats = ReadActionStats(context.Project);
        var recording = context.RecordingInfo;
        var builder = new StringBuilder();
        builder.AppendLine($"- 自动观察速度：{context.ActionProfile.SpeedPreset}");
        builder.AppendLine($"- 自动观察模式：{context.ActionProfile.ObserveMode}");
        builder.AppendLine($"- 最大自动观察时长：{context.ActionProfile.AutoObserveDurationSeconds}s");
        builder.AppendLine($"- 实际录屏时长：{TimeUtil.FormatTime(TimeSpan.FromSeconds(recording.DurationSeconds))}");
        builder.AppendLine($"- 是否发生人工接管：{YesNo(recording.HadManualTakeover)}");
        builder.AppendLine($"- 人工接管时间点：{string.Join(", ", stats.ManualTakeoverTimes.DefaultIfEmpty("--"))}");
        builder.AppendLine($"- 自动动作数量：{stats.AutoActionCount}");
        builder.AppendLine($"- 鼠标移动次数：{stats.MouseMoveCount}");
        builder.AppendLine($"- 滚动次数：{stats.ScrollCount}");
        builder.AppendLine($"- hover 次数：{stats.HoverCount}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildActualObserveMode(ObservationContext context)
    {
        var stats = ReadActionStats(context.Project);
        var recording = context.RecordingInfo;
        var builder = new StringBuilder();
        builder.AppendLine($"- 配置观察模式：{context.ActionProfile.ObserveMode}");
        builder.AppendLine($"- 实际观察模式：{ResolveActualObserveMode(recording, stats)}");
        builder.AppendLine($"- 自动控制时长：{EstimateAutoDuration(recording, stats):0.0}s");
        builder.AppendLine($"- 人工接管时长：{EstimateManualDuration(recording, stats):0.0}s");
        builder.AppendLine($"- 人工接管占比：{ManualRatio(recording, stats):P0}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildAutomaticManualNotes()
    {
        return """
本资料包可能由自动观察、人工接管或两者混合生成。

- 自动观察适合记录页面结构和滚动节奏。
- 人工接管适合捕捉菜单、CTA、主题切换、弹层和页面转场。
- 如果本包为人工主导，请优先参考 markers.md 和 selected-frames-index.md。
""";
    }

    private static string BuildSelectedFramesNotes()
    {
        return """
本 ChatGPT 上传包通常不包含全部抽帧，只包含 selected-frames/ 中的精选截图。
分析截图时，请优先参考 selected-frames-index.md。
""";
    }

    private static string BuildUserIntentAssetNotes(RebuildProject project)
    {
        var path = project.UserIntentAssetManifestMarkdownPath;
        if (!File.Exists(path))
        {
            return "用户未添加图片。";
        }

        var content = ReadFileOrEmpty(path).Trim();
        return string.IsNullOrWhiteSpace(content) ? "用户未添加图片。" : content;
    }

    private static string BuildFrameIndexTable(ObservationContext context)
    {
        if (context.FrameResult is null || context.FrameResult.Frames.Count == 0)
        {
            return "（尚未生成抽帧截图。）";
        }

        var frames = context.MarkdownOptions.IncludeFullFrameTable
            ? context.FrameResult.Frames
            : context.FrameResult.Frames.Take(context.MarkdownOptions.MaxFrameRowsInMarkdown);

        var builder = new StringBuilder();
        builder.AppendLine("| 序号 | 文件 | 时间 | 相对路径 |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var frame in frames)
        {
            builder.AppendLine($"| {frame.Index} | {frame.FileName} | {TimeUtil.FormatTime(frame.Time)} | {frame.RelativePath} |");
        }

        if (!context.MarkdownOptions.IncludeFullFrameTable && context.FrameResult.Frames.Count > context.MarkdownOptions.MaxFrameRowsInMarkdown)
        {
            builder.AppendLine();
            builder.AppendLine($"> 全量索引包含 {context.FrameResult.Frames.Count} 张截图，请查看 frames-index.csv。");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSegmentIndex(ObservationContext context)
    {
        if (context.FrameResult is null || context.FrameResult.Frames.Count == 0)
        {
            return "（尚未生成抽帧截图。）";
        }

        var builder = new StringBuilder();
        builder.AppendLine("| 片段 | 视频时间范围 | 类型 | 建议截图 | 说明 |");
        builder.AppendLine("|---|---|---|---|---|");
        builder.AppendLine($"| 首屏 | 00:00:00.000-00:00:06.000 | initial | {FrameRange(context.FrameResult, TimeSpan.Zero, TimeSpan.FromSeconds(6))} | 观察首屏布局、加载动画和入场效果 |");

        var actions = ReadActionSegments(context.Project).Take(12).ToList();
        var index = 1;
        foreach (var action in actions)
        {
            var start = action.Time < TimeSpan.FromSeconds(2) ? TimeSpan.Zero : action.Time - TimeSpan.FromSeconds(2);
            var end = action.Time + TimeSpan.FromSeconds(3);
            builder.AppendLine($"| {SegmentName(action.Type, index++)} | {TimeUtil.FormatTime(start)}-{TimeUtil.FormatTime(end)} | {Escape(action.Type)} | {FrameRange(context.FrameResult, start, end)} | {Escape(action.Description)} |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildExtractionInfo(ObservationContext context)
    {
        var frameResult = context.FrameResult;
        var frameOptions = context.FrameOptions;
        var sourceFps = frameOptions?.SourceFrameRate ?? context.RecordingInfo.FrameRate;
        var effectiveFps = frameResult?.EffectiveFps ?? 0;
        var estimated = frameResult is not null && context.RecordingInfo.DurationSeconds > 0
            ? Math.Ceiling(context.RecordingInfo.DurationSeconds * effectiveFps)
            : 0;

        return $"""
- 抽帧间隔：{frameResult?.IntervalMs ?? frameOptions?.IntervalMs ?? 0} ms
- 是否避免超过源 FPS 的重复帧：{YesNo(frameOptions?.AllowDuplicateFramesAboveSourceFps != true)}
- 源视频 FPS：{sourceFps:0.###}
- 实际抽帧 FPS：{effectiveFps:0.###}
- 预估截图数：{estimated:0}
- 实际截图数：{frameResult?.TotalFrames ?? 0}
- 输出目录：{ToRelative(context.Project.ProjectDirectory, frameResult?.OutputDirectory ?? string.Empty)}
""";
    }

    private static string ReadFileOrEmpty(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : "（无内容。）";
    }

    private static string ChatGptPrompt()
    {
        return """
请根据 observation.md 和 selected-frames/ 中的截图分析参考网站。
重点关注页面结构、首屏构图、滚动节奏、hover / 点击 / 菜单转场、视觉层级、可复用设计语言，以及不应直接照搬的部分。
最后输出一份可执行的 Codex 网页重构任务。
""";
    }

    private static IEnumerable<ActionSegment> ReadActionSegments(RebuildProject project)
    {
        var path = Path.Combine(project.ActionsDirectory, "action-log.jsonl");
        if (!File.Exists(path))
        {
            yield break;
        }

        var keyTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "scroll",
            "mouse-move",
            "hover",
            "hotspot-click",
            "manual-control-start",
            "manual-control-end",
            "manual-marker",
            "manual-screenshot",
            "recording-start-before-open-url",
            "open-url"
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
                var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;
                if (!keyTypes.Contains(type))
                {
                    continue;
                }

                var timeText = root.TryGetProperty("videoTime", out var videoTimeElement) ? videoTimeElement.GetString() : null;
                if (!TimeSpan.TryParse(timeText, out var time))
                {
                    continue;
                }

                var description = root.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() ?? string.Empty : string.Empty;
                yield return new ActionSegment(type, time, description);
            }
        }
    }

    private static ActionStats ReadActionStats(RebuildProject project)
    {
        var segments = ReadActionSegments(project).ToList();
        return new ActionStats(
            segments.Count(segment => segment.Type is "mouse-move" or "hover" or "scroll" or "hotspot-click"),
            segments.Count(segment => string.Equals(segment.Type, "mouse-move", StringComparison.OrdinalIgnoreCase)),
            segments.Count(segment => string.Equals(segment.Type, "scroll", StringComparison.OrdinalIgnoreCase)),
            segments.Count(segment => string.Equals(segment.Type, "hover", StringComparison.OrdinalIgnoreCase)),
            segments.Where(segment => string.Equals(segment.Type, "manual-control-start", StringComparison.OrdinalIgnoreCase))
                .Select(segment => TimeUtil.FormatTime(segment.Time))
                .ToList(),
            segments.Any(segment => string.Equals(segment.Type, "manual-control-start", StringComparison.OrdinalIgnoreCase)));
    }

    private static string ResolveActualObserveMode(RecordingInfo recording, ActionStats stats)
    {
        var ratio = ManualRatio(recording, stats);
        if (ratio > 0.60)
        {
            return "manual-led";
        }

        return ratio < 0.40 ? "auto-led" : "mixed";
    }

    private static double EstimateManualDuration(RecordingInfo recording, ActionStats stats)
    {
        if (!recording.HadManualTakeover && !stats.HadManualTakeover)
        {
            return 0;
        }

        return recording.DurationSeconds * 0.60;
    }

    private static double EstimateAutoDuration(RecordingInfo recording, ActionStats stats)
    {
        return Math.Max(0, recording.DurationSeconds - EstimateManualDuration(recording, stats));
    }

    private static double ManualRatio(RecordingInfo recording, ActionStats stats)
    {
        if (recording.DurationSeconds <= 0)
        {
            return 0;
        }

        return EstimateManualDuration(recording, stats) / recording.DurationSeconds;
    }

    private static string FrameRange(FrameExtractResult result, TimeSpan start, TimeSpan end)
    {
        var startFrame = result.Frames.OrderBy(frame => Math.Abs((frame.Time - start).TotalMilliseconds)).FirstOrDefault();
        var endFrame = result.Frames.OrderBy(frame => Math.Abs((frame.Time - end).TotalMilliseconds)).FirstOrDefault();
        if (startFrame is null || endFrame is null)
        {
            return "--";
        }

        return $"{startFrame.FileName}-{endFrame.FileName}";
    }

    private static string SegmentName(string type, int index)
    {
        return type switch
        {
            "scroll" => $"自动滚动 {index}",
            "hover" => $"悬停 {index}",
            "hotspot-click" => $"热点点击 {index}",
            "manual-control-start" => $"人工接管 {index}",
            "manual-control-end" => $"恢复自动 {index}",
            "manual-marker" => $"标记 {index}",
            "manual-screenshot" => $"截图 {index}",
            "recording-start-before-open-url" => "先录屏再打开网站",
            "open-url" => "打开网站",
            _ => $"{type} {index}"
        };
    }

    private static string ToRelative(string root, string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|").Replace(Environment.NewLine, " ");
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private sealed record ActionSegment(string Type, TimeSpan Time, string Description);

    private sealed record ActionStats(
        int AutoActionCount,
        int MouseMoveCount,
        int ScrollCount,
        int HoverCount,
        IReadOnlyList<string> ManualTakeoverTimes,
        bool HadManualTakeover);
}
