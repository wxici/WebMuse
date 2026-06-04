namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ExecutionPreconditionSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.7.3-execution-preconditions";
    public const string ExecutionRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/execution";
    public const string PreconditionsFileName = "execution-preconditions.json";
    public const string PreconditionsMarkdownFileName = "execution-preconditions.md";
}

public enum ExecutionPreconditionStatus
{
    Passed,
    Warning,
    Blocked,
    NotApplicable,
    NotImplemented
}

public enum ExecutionPreconditionSeverity
{
    Info,
    Warning,
    Error
}

public enum ExecutionPreconditionDecision
{
    Blocked,
    ReadyForFutureProofCheckOnly,
    ReadyForFutureRealExecution
}

public sealed class ExecutionPreconditionReport
{
    public string SchemaVersion { get; set; } = ExecutionPreconditionSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ExecutionPreconditionDecision Decision { get; set; } = ExecutionPreconditionDecision.Blocked;
    public bool AllowsRealCodexExecution { get; set; }
    public bool ExecutesCodexCli { get; set; }
    public bool CallsOpenAiApi { get; set; }
    public bool CallsLocalModel { get; set; }
    public bool GeneratesWebsite { get; set; }

    public string StoredRelativePath { get; set; } = string.Empty;
    public string MarkdownRelativePath { get; set; } = string.Empty;

    public List<ExecutionPreconditionItem> Items { get; set; } = [];
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ExecutionPreconditionItem
{
    public string Key { get; set; } = string.Empty;
    public ExecutionPreconditionSeverity Severity { get; set; } = ExecutionPreconditionSeverity.Info;
    public ExecutionPreconditionStatus Status { get; set; } = ExecutionPreconditionStatus.NotApplicable;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
    public bool BlocksExecution { get; set; }
    public string? FailureCategory { get; set; }
}

public sealed class ExecutionPreconditionOptions
{
    public bool RequireProofExecutionPassed { get; set; } = true;
    public bool RequireApprovalApproved { get; set; } = true;
    public bool RequireSafetySnapshot { get; set; } = true;
    public bool RequireManualFallbackAvailable { get; set; } = true;
}
