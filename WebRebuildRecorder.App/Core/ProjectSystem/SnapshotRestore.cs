namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class SnapshotRestoreSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.4-restore";
    public const string RestoreReportsRootRelativePath = $"{ProjectDirectoryV2.Versions}/restore-reports";
    public const string RestorePlanFileName = "restore-plan.json";
    public const string RestoreResultFileName = "restore-result.json";
}

public sealed class SnapshotRestorePlan
{
    public string SchemaVersion { get; set; } = SnapshotRestoreSchema.CurrentSchemaVersion;
    public string RestoreId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool CreateSafetySnapshotBeforeRestore { get; set; } = true;
    public string? SafetySnapshotId { get; set; }
    public List<SnapshotRestoreFileItem> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class SnapshotRestoreFileItem
{
    public string SourceRelativePath { get; set; } = string.Empty;
    public string TargetRelativePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExpectedSha256 { get; set; }
    public string? ActualSha256 { get; set; }
}

public sealed class SnapshotRestoreResult
{
    public string SchemaVersion { get; set; } = SnapshotRestoreSchema.CurrentSchemaVersion;
    public string RestoreId { get; set; } = string.Empty;
    public bool IsOk { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public string? SafetySnapshotId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SnapshotRestoreFileItem> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class SnapshotValidationResult
{
    public bool IsOk { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public List<SnapshotValidationItem> Items { get; set; } = [];
}

public sealed class SnapshotValidationItem
{
    public string Key { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
}
