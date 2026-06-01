namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class LegacyObservationBridgeSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.3-bridge";
    public const string ReportRelativePath = $"{ProjectDirectoryV2.Observation}/legacy-bridge-report.json";
}

public sealed class LegacyObservationBridgeResult
{
    public string SchemaVersion { get; set; } = LegacyObservationBridgeSchema.CurrentSchemaVersion;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsOk { get; set; } = true;
    public List<LegacyObservationBridgeItem> Items { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class LegacyObservationBridgeItem
{
    public string Key { get; set; } = string.Empty;
    public string SourceRelativePath { get; set; } = string.Empty;
    public string TargetKind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
