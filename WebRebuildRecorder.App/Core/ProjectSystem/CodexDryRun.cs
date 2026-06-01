namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class CodexDryRunSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.6-dry-run";
    public const string DryRunsRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/dry-runs";
    public const string PlanFileName = "dry-run-plan.json";
    public const string ResultFileName = "dry-run-result.json";
    public const string ReportFileName = "dry-run-report.md";
    public const string RecordFileName = "dry-run-record.json";
}

public sealed class CodexDryRunPlan
{
    public string SchemaVersion { get; set; } = CodexDryRunSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskPackageId { get; set; } = string.Empty;
    public string DryRunId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Mode { get; set; } = "preCodexDryRun";
    public bool WouldExecuteCodexCli { get; set; }
    public bool IsReadyForFutureExecution { get; set; }
    public List<CodexDryRunStep> Steps { get; set; } = [];
    public List<CodexDryRunInputFile> InputFiles { get; set; } = [];
    public List<CodexDryRunOutputTarget> OutputTargets { get; set; } = [];
    public List<CodexDryRunSafetyCheck> SafetyChecks { get; set; } = [];
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class CodexDryRunStep
{
    public string StepId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool WouldRunExternalProcess { get; set; }
    public string? RelativePath { get; set; }
}

public sealed class CodexDryRunInputFile
{
    public string RelativePath { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public bool Required { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class CodexDryRunOutputTarget
{
    public string RelativePath { get; set; } = string.Empty;
    public bool IsAllowedWriteTarget { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CodexDryRunSafetyCheck
{
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FailureCategory { get; set; }
    public bool BlocksFutureExecution { get; set; }
}

public sealed class CodexDryRunResult
{
    public string SchemaVersion { get; set; } = CodexDryRunSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskPackageId { get; set; } = string.Empty;
    public string DryRunId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsOk { get; set; }
    public bool IsReadyForFutureExecution { get; set; }
    public bool ExecutedCodexCli { get; set; }
    public bool CalledOpenAiApi { get; set; }
    public bool GeneratedWebsite { get; set; }
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class CodexDryRunRecord
{
    public string SchemaVersion { get; set; } = CodexDryRunSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskPackageId { get; set; } = string.Empty;
    public string DryRunId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDryRun { get; set; } = true;
    public string Status { get; set; } = string.Empty;
    public bool ExecutedCodexCli { get; set; }
    public bool CalledOpenAiApi { get; set; }
    public bool GeneratedWebsite { get; set; }
    public string PlanRelativePath { get; set; } = string.Empty;
    public string ResultRelativePath { get; set; } = string.Empty;
    public string ReportRelativePath { get; set; } = string.Empty;
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
