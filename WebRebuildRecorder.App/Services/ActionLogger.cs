using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class ActionLogger
{
    private readonly AppLogger _logger;
    private RebuildProject? _project;
    private DateTime? _sessionStart;
    private DateTime? _recordingStart;
    private DateTime? _recordingEnd;

    public ActionLogger(AppLogger logger)
    {
        _logger = logger;
    }

    public void AttachProject(RebuildProject project)
    {
        _project = project;
    }

    public void DetachProject()
    {
        _project = null;
        ClearRuntime();
    }

    public void ClearRuntime()
    {
        _sessionStart = null;
        _recordingStart = null;
        _recordingEnd = null;
    }

    public void SetSessionStart(DateTime startTime)
    {
        _sessionStart = startTime;
    }

    public void SetRecordingStart(DateTime startTime)
    {
        _recordingStart = startTime;
        _recordingEnd = null;
    }

    public void SetRecordingEnd(DateTime endTime)
    {
        _recordingEnd = endTime;
    }

    public TimeSpan CurrentElapsed => _recordingStart is null ? TimeSpan.Zero : (_recordingEnd ?? DateTime.Now) - _recordingStart.Value;

    public TimeSpan CurrentSessionElapsed => _sessionStart is null ? TimeSpan.Zero : DateTime.Now - _sessionStart.Value;

    public async Task LogAsync(string type, string target = "", string description = "", object? data = null)
    {
        if (_project is null)
        {
            return;
        }

        var sessionTimeText = TimeUtil.FormatTime(CurrentSessionElapsed);
        var videoTimeText = _recordingStart is null ? "--" : TimeUtil.FormatTime(CurrentElapsed);
        var jsonRecord = new Dictionary<string, object?>
        {
            ["sessionTime"] = sessionTimeText,
            ["videoTime"] = videoTimeText,
            ["type"] = type
        };

        if (!string.IsNullOrWhiteSpace(target))
        {
            jsonRecord["target"] = target;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            jsonRecord["description"] = description;
        }

        if (data is not null)
        {
            jsonRecord["data"] = data;
        }

        var jsonLine = JsonSerializer.Serialize(jsonRecord);
        await File.AppendAllTextAsync(Path.Combine(_project.ActionsDirectory, "action-log.jsonl"), jsonLine + Environment.NewLine, Encoding.UTF8);

        var mdLine = $"| {sessionTimeText} | {videoTimeText} | {EscapeMarkdown(type)} | {EscapeMarkdown(target)} | {EscapeMarkdown(description)} |{Environment.NewLine}";
        await File.AppendAllTextAsync(Path.Combine(_project.ActionsDirectory, "action-log.md"), mdLine, Encoding.UTF8);

        _logger.Info($"action session={sessionTimeText} video={videoTimeText} {type} {target}".Trim());
    }

    public static string CreateMarkdownHeader()
    {
        return "# Action Log" + Environment.NewLine
            + Environment.NewLine
            + "| 会话时间 | 视频时间 | 动作 | 目标 | 说明 |" + Environment.NewLine
            + "|---|---|---|---|---|" + Environment.NewLine;
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|").Replace(Environment.NewLine, " ");
    }
}
