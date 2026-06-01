namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ConstructionPackageSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1.2-package";
    public const string RelativePath = $"{ProjectDirectoryV2.CodexTask}/construction-package.json";
}

public sealed class ConstructionPackageManifest
{
    public string SchemaVersion { get; set; } = ConstructionPackageSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ConstructionPackageId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ReferenceUrl { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string GoalSummary { get; set; } = string.Empty;
    public List<ConstructionInputItem> Inputs { get; set; } = [];
    public List<ConstructionConstraintItem> Constraints { get; set; } = [];
    public List<ConstructionDeliverableItem> Deliverables { get; set; } = [];
    public List<string> RequiredProjectFiles { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class ConstructionInputItem
{
    public string InputId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
}

public sealed class ConstructionConstraintItem
{
    public string ConstraintId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public sealed class ConstructionDeliverableItem
{
    public string DeliverableId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string TargetRelativePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
