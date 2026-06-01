namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ContentMapSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1-data";
    public const string RelativePath = $"{ProjectDirectoryV2.Maps}/content-map.json";
}

public sealed class ContentMap
{
    public string SchemaVersion { get; set; } = ContentMapSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PageMap> Pages { get; set; } = [];
}

public sealed class PageMap
{
    public string PageId { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<SectionMap> Sections { get; set; } = [];
}

public sealed class SectionMap
{
    public string SectionId { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataTuneId { get; set; } = string.Empty;
    public List<ElementMap> Elements { get; set; } = [];
}

public sealed class ElementMap
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;
    public string DataTuneId { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string? AssetId { get; set; }
    public bool IsUserEditableInBasicMode { get; set; }
}
