namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ConstructionPackageContextSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.3-context";
    public const string ContextRootRelativePath = $"{ProjectDirectoryV2.CodexTask}/context";
    public const string ProjectBriefRelativePath = $"{ContextRootRelativePath}/project-brief.md";
    public const string ObservationSummaryRelativePath = $"{ContextRootRelativePath}/observation-summary.md";
    public const string AssetIndexRelativePath = $"{ContextRootRelativePath}/asset-index.md";
    public const string ThemeSummaryRelativePath = $"{ContextRootRelativePath}/theme-summary.md";
    public const string ContentMapSummaryRelativePath = $"{ContextRootRelativePath}/content-map-summary.md";
    public const string ConstraintsRelativePath = $"{ContextRootRelativePath}/constraints.md";
    public const string AcceptanceChecklistRelativePath = $"{ContextRootRelativePath}/acceptance-checklist.md";
    public const string PackageIndexRelativePath = $"{ContextRootRelativePath}/package-index.json";

    public static readonly IReadOnlyList<ConstructionContextFileDefinition> ContextFiles =
    [
        new(ProjectBriefRelativePath, "projectBrief"),
        new(ObservationSummaryRelativePath, "observationSummary"),
        new(AssetIndexRelativePath, "assetIndex"),
        new(ThemeSummaryRelativePath, "themeSummary"),
        new(ContentMapSummaryRelativePath, "contentMapSummary"),
        new(ConstraintsRelativePath, "constraints"),
        new(AcceptanceChecklistRelativePath, "acceptanceChecklist"),
        new(PackageIndexRelativePath, "packageIndex")
    ];
}

public sealed record ConstructionContextFileDefinition(string RelativePath, string Kind);

public sealed class ConstructionPackageContentBuildResult
{
    public bool IsOk { get; set; } = true;
    public List<ConstructionPackageContextFileItem> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ConstructionPackageContextIndex
{
    public string SchemaVersion { get; set; } = ConstructionPackageContextSchema.CurrentSchemaVersion;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ConstructionPackageContextFileItem> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ConstructionPackageContextFileItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
