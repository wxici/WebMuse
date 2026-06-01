namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class AssetsManifestSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1-data";
    public const string RelativePath = $"{ProjectDirectoryV2.Assets}/assets-manifest.json";
}

public sealed class AssetsManifest
{
    public string SchemaVersion { get; set; } = AssetsManifestSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<AssetManifestItem> Assets { get; set; } = [];
}

public sealed class AssetManifestItem
{
    public string AssetId { get; set; } = string.Empty;
    public string Kind { get; set; } = "unknown";
    public string Role { get; set; } = "unknown";
    public string RelativePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string SourceType { get; set; } = "unknown";
    public string? SourceNote { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool IsUserProvided { get; set; }
    public bool IsAiGenerated { get; set; }
    public bool IsExternalReference { get; set; }
    public bool IsApprovedForExport { get; set; }
    public List<string> Tags { get; set; } = [];
}
