namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ProjectSnapshotSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1-data";
    public const string SnapshotRootRelativePath = $"{ProjectDirectoryV2.Versions}/snapshots";
    public const string ManifestFileName = "snapshot-manifest.json";
}

public sealed class ProjectSnapshotManifest
{
    public string SchemaVersion { get; set; } = ProjectSnapshotSchema.CurrentSchemaVersion;
    public string SnapshotId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = string.Empty;
    public List<SnapshotFileItem> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class SnapshotFileItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
