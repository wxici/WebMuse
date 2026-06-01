namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ConstructionReadinessSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.5-readiness";
    public const string ReadinessRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/readiness";
    public const string ReportJsonRelativePath = $"{ReadinessRootRelativePath}/readiness-report.json";
    public const string ReportMarkdownRelativePath = $"{ReadinessRootRelativePath}/readiness-report.md";
}

public enum ConstructionReadinessMode
{
    Draft,
    Strict,
    PreCodexDryRun
}

public enum ConstructionReadinessStatus
{
    Ok,
    Warning,
    Error,
    Skipped
}

public sealed class ConstructionReadinessResult
{
    public string SchemaVersion { get; set; } = ConstructionReadinessSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public ConstructionReadinessMode Mode { get; set; } = ConstructionReadinessMode.Strict;
    public bool IsReady { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ConstructionReadinessItem> Items { get; set; } = [];
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ConstructionReadinessItem
{
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ConstructionReadinessStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
    public bool BlocksExecution { get; set; }
    public TaskFailureCategory? FailureCategory { get; set; }
}
