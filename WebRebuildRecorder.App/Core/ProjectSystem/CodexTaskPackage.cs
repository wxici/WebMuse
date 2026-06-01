namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class CodexTaskPackageSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.2-package";
    public const string RelativePath = $"{ProjectDirectoryV2.CodexTask}/task-package.json";
    public const string InstructionsRelativePath = $"{ProjectDirectoryV2.CodexTask}/instructions.md";
}

public sealed class CodexTaskPackage
{
    public string SchemaVersion { get; set; } = CodexTaskPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskPackageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CodexTaskInstruction Instruction { get; set; } = new();
    public CodexTaskSandbox Sandbox { get; set; } = new();
    public List<CodexTaskInputFile> InputFiles { get; set; } = [];
    public List<CodexTaskExpectedOutput> ExpectedOutputs { get; set; } = [];
    public List<string> ProhibitedActions { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class CodexTaskInstruction
{
    public string Title { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string PromptMarkdown { get; set; } = string.Empty;
}

public sealed class CodexTaskSandbox
{
    public string WorkspaceRelativePath { get; set; } = string.Empty;
    public List<string> AllowedWriteRoots { get; set; } = [];
    public List<string> ForbiddenRoots { get; set; } = [];
}

public sealed class CodexTaskInputFile
{
    public string RelativePath { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool Required { get; set; }
}

public sealed class CodexTaskExpectedOutput
{
    public string RelativePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public static class CodexTaskRunSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.2-package";
    public const string RunsRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/runs";
    public const string RunRecordFileName = "run-record.json";
}

public sealed class CodexTaskRunRecord
{
    public string SchemaVersion { get; set; } = CodexTaskRunSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskPackageId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public CodexTaskRunStatus Status { get; set; }
    public List<CodexTaskFailureItem> Failures { get; set; } = [];
    public List<string> OutputRelativePaths { get; set; } = [];
}

public enum CodexTaskRunStatus
{
    Created,
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

public sealed class CodexTaskFailureItem
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public enum TaskFailureCategory
{
    Unknown,
    ValidationError,
    MissingInput,
    SandboxViolation,
    SecretDetected,
    EnvironmentMissing,
    CodexUnavailable,
    NetworkUnavailable,
    Timeout,
    CancelledByUser,
    OutputMissing,
    BuildFailed,
    InternalError
}

public sealed class TaskFailureClassification
{
    public TaskFailureCategory Category { get; set; }
    public string Message { get; set; } = string.Empty;
}
