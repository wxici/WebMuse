namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class AlphaValidationProbeSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.8-0-alpha-validation-probe";
    public const string AlphaValidationRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/alpha-validation";
    public const string ReportJsonFileName = "alpha-validation-report.json";
    public const string ReportMarkdownFileName = "alpha-validation-report.md";
}

public enum AlphaValidationStepStatus
{
    Passed,
    Warning,
    Blocked,
    Skipped,
    NotImplemented
}

public sealed class AlphaValidationProbeReport
{
    public string SchemaVersion { get; set; } = AlphaValidationProbeSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ProbeId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsUsableAsAlphaEvidence { get; set; }
    public bool ExecutesCodexCli { get; set; }
    public bool CallsOpenAiApi { get; set; }
    public bool CallsLocalModel { get; set; }
    public bool GeneratesWebsite { get; set; }

    public string StoredRelativePath { get; set; } = string.Empty;
    public string MarkdownRelativePath { get; set; } = string.Empty;

    public List<AlphaValidationStep> Steps { get; set; } = [];
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string NextRecommendedAction { get; set; } = string.Empty;
}

public sealed class AlphaValidationStep
{
    public string Key { get; set; } = string.Empty;
    public AlphaValidationStepStatus Status { get; set; } = AlphaValidationStepStatus.Skipped;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
    public bool BlocksAlphaEvidence { get; set; }
}
