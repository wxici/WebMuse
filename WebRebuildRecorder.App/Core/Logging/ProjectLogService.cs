using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.ProjectSystem;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.Logging;

public interface IProjectLogService
{
    Task WriteAsync(
        string projectRoot,
        string channel,
        string message,
        ProjectLogLevel level,
        CancellationToken cancellationToken = default);
}

public enum ProjectLogLevel
{
    Trace,
    Info,
    Warning,
    Error
}

public sealed class ProjectLogService : IProjectLogService
{
    public async Task WriteAsync(
        string projectRoot,
        string channel,
        string message,
        ProjectLogLevel level,
        CancellationToken cancellationToken = default)
    {
        var fileName = ResolveChannelFileName(channel);
        var relativePath = $"{ProjectDirectoryV2.Logs}/{fileName}";
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, "project log");
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(validation.NormalizedTargetPath)!);
        var entry = new ProjectLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Channel = Path.GetFileNameWithoutExtension(fileName),
            Message = SanitizeMessage(message)
        };

        var line = JsonSerializer.Serialize(entry, WrbJsonOptions.Compact) + Environment.NewLine;
        await File.AppendAllTextAsync(validation.NormalizedTargetPath, line, Encoding.UTF8, cancellationToken);
    }

    private static string ResolveChannelFileName(string channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized switch
        {
            "app" => LogChannels.App,
            "project" => LogChannels.Project,
            "observation" => LogChannels.Observation,
            "codex-task" or "codextask" or "codexrun" or "codex-run" => LogChannels.CodexTask,
            "export" => LogChannels.Export,
            "security" => LogChannels.Security,
            "build" => LogChannels.Build,
            "validation" => LogChannels.Validation,
            "error" => LogChannels.Error,
            _ => throw new InvalidOperationException($"Unknown project log channel: {channel}")
        };
    }

    private static string SanitizeMessage(string message)
    {
        var value = message?.ReplaceLineEndings(" ").Trim() ?? string.Empty;
        value = Regex.Replace(value, @"sk-[A-Za-z0-9_\-]{3,}", "sk-[redacted]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"(?i)(api[_-]?key|token|secret|password)\s*[:=]\s*[^,\s;]+", "$1=[redacted]");
        return value;
    }

    private sealed class ProjectLogEntry
    {
        public DateTimeOffset Timestamp { get; init; }
        public ProjectLogLevel Level { get; init; }
        public string Channel { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
