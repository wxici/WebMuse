namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ApprovalGateSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.7.2-approval-gate";
    public const string ApprovalsRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/approvals";
    public const string ApprovalRequestFileName = "approval-request.json";
    public const string ApprovalResultFileName = "approval-result.json";
    public const string ApprovalValidationReportJsonFileName = "approval-validation-report.json";
    public const string ApprovalValidationReportMarkdownFileName = "approval-validation-report.md";
}

public enum ApprovalGateType
{
    ApprovalRequiredBeforeProofCheck,
    ApprovalRequiredBeforeRealCodexExecution,
    ApprovalRequiredBeforeWritingOutputSite,
    ApprovalRequiredBeforeOverwritingExistingOutput,
    ApprovalRequiredBeforeExportZip,
    ApprovalRequiredBeforeUploadingAnything
}

public enum ApprovalDecision
{
    Pending,
    Approved,
    Rejected,
    Expired,
    Cancelled,
    Superseded
}

public sealed class ApprovalGateRequest
{
    public string SchemaVersion { get; set; } = ApprovalGateSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ApprovalId { get; set; } = string.Empty;
    public string GateId { get; set; } = string.Empty;
    public ApprovalGateType GateType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Purpose { get; set; } = string.Empty;
    public string RequiredSummary { get; set; } = string.Empty;
    public string RiskWarning { get; set; } = string.Empty;
    public bool CannotBeBypassedByAi { get; set; } = true;
    public string StoredRelativePath { get; set; } = string.Empty;
    public ApprovalBinding Binding { get; set; } = new();
}

public sealed class ApprovalGateResult
{
    public string SchemaVersion { get; set; } = ApprovalGateSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ApprovalId { get; set; } = string.Empty;
    public string GateId { get; set; } = string.Empty;
    public ApprovalGateType GateType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string Reason { get; set; } = string.Empty;
    public bool CannotBeBypassedByAi { get; set; } = true;
    public string StoredRelativePath { get; set; } = string.Empty;
    public string RequestRelativePath { get; set; } = string.Empty;
    public ApprovalBinding Binding { get; set; } = new();
}

public sealed class ApprovalBinding
{
    public DateTimeOffset BoundAt { get; set; } = DateTimeOffset.UtcNow;
    public string TaskPackageRelativePath { get; set; } = string.Empty;
    public string TaskPackageSha256 { get; set; } = string.Empty;
    public string InstructionsRelativePath { get; set; } = string.Empty;
    public string InstructionsSha256 { get; set; } = string.Empty;
    public string? DryRunPlanRelativePath { get; set; }
    public string? DryRunPlanSha256 { get; set; }
    public string? ProofManifestRelativePath { get; set; }
    public string? ProofManifestSha256 { get; set; }
    public string? ExecutionPlanRelativePath { get; set; }
    public string? ExecutionPlanSha256 { get; set; }
}

public sealed class ApprovalGateValidationResult
{
    public bool IsOk { get; set; }
    public bool IsBindingCurrent { get; set; }
    public bool IsExecutable { get; set; }
    public string ApprovalId { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public List<ApprovalGateValidationItem> Items { get; set; } = [];
}

public sealed class ApprovalGateValidationItem
{
    public string Key { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
    public bool BlocksExecution { get; set; }
    public string? FailureCategory { get; set; }
}
