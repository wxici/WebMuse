namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ObservationPackageSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.2-package";
    public const string RelativePath = $"{ProjectDirectoryV2.Observation}/observation-package.json";
}

public sealed class ObservationPackageManifest
{
    public string SchemaVersion { get; set; } = ObservationPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ObservationPackageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ReferenceUrl { get; set; } = string.Empty;
    public string CaptureMode { get; set; } = string.Empty;
    public List<ObservationArtifactItem> Artifacts { get; set; } = [];
    public List<ObservationSectionItem> Sections { get; set; } = [];
    public List<ObservationInteractionItem> Interactions { get; set; } = [];
    public List<ObservationFindingItem> Findings { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ObservationArtifactItem
{
    public string ArtifactId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
    public long? SizeBytes { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class ObservationSectionItem
{
    public string SectionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public string VisualIntent { get; set; } = string.Empty;
    public List<string> RelatedArtifactIds { get; set; } = [];
}

public sealed class ObservationInteractionItem
{
    public string InteractionId { get; set; } = string.Empty;
    public string TargetHint { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string ObservedEffect { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
}

public sealed class ObservationFindingItem
{
    public string FindingId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
}
