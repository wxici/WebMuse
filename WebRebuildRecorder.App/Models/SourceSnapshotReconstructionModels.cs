namespace WebRebuildRecorder.App.Models;

public sealed class SourceSnapshotViewportPreset
{
    public string Name { get; set; } = "Desktop 1440x900";
    public int Width { get; set; } = 1440;
    public int Height { get; set; } = 900;
}

public sealed class SourceSnapshotCaptureOptions
{
    public SourceSnapshotViewportPreset Viewport { get; set; } = new();
    public byte[]? FirstScreenPngBytes { get; set; }
    public IReadOnlyList<SourceSnapshotTextResource>? KnownTextResources { get; set; }
}

public sealed class SourceSnapshotCaptureOutput
{
    public SourceSnapshotRenderedEvidence Evidence { get; set; } = new();
    public byte[]? FirstScreenPngBytes { get; set; }
}

public sealed class SourceSnapshotTextResource
{
    public string Url { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string LocalRelativePath { get; set; } = string.Empty;
    public bool Fetched { get; set; }
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public bool Truncated { get; set; }
    public string Error { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
}

public sealed class SourceSnapshotDependencyGraph
{
    public List<SourceSnapshotDependencyNode> Nodes { get; set; } = [];
}

public sealed class SourceSnapshotDependencyNode
{
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SourceTag { get; set; } = string.Empty;
    public bool SameOrigin { get; set; }
    public bool TextContentFetched { get; set; }
    public bool BinaryOnlyListed { get; set; }
    public string ReferencedBySection { get; set; } = string.Empty;
    public string LocalRelativePath { get; set; } = string.Empty;
}

public sealed class SourceSnapshotSectionMapItem
{
    public string SectionKey { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string BodyPreview { get; set; } = string.Empty;
    public List<string> CssClasses { get; set; } = [];
    public List<string> CtaTexts { get; set; } = [];
    public List<string> MediaRefs { get; set; } = [];
    public List<string> BehaviorRefs { get; set; } = [];
    public List<string> ResponsiveFlags { get; set; } = [];
    public string VisualRole { get; set; } = string.Empty;
}

public sealed class SourceSnapshotMediaPlacementItem
{
    public string SectionKey { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string AspectRatio { get; set; } = string.Empty;
    public string WidthHeight { get; set; } = string.Empty;
    public List<string> ResponsiveVariants { get; set; } = [];
    public List<string> CssClasses { get; set; } = [];
    public List<string> Behaviors { get; set; } = [];
    public string ReplacementAdvice { get; set; } = string.Empty;
}

public sealed class SourceSnapshotBehaviorItem
{
    public string SectionKey { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string Plugin { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string AnimationName { get; set; } = string.Empty;
    public string RawDeclaration { get; set; } = string.Empty;
    public string LikelyJsOwner { get; set; } = string.Empty;
    public string EffectSummary { get; set; } = string.Empty;
    public string RebuildSuggestion { get; set; } = string.Empty;
}

public sealed class SourceSnapshotCssRuleMapItem
{
    public string Selector { get; set; } = string.Empty;
    public string SourceCssFile { get; set; } = string.Empty;
    public List<string> MatchedClasses { get; set; } = [];
    public List<string> ImportantProperties { get; set; } = [];
    public string RebuildSuggestion { get; set; } = string.Empty;
}

public sealed class SourceSnapshotJsBehaviorReferenceItem
{
    public string SourceJsFile { get; set; } = string.Empty;
    public bool MinifiedLikely { get; set; }
    public List<string> PluginNamesFound { get; set; } = [];
    public List<string> AnimationNamesFound { get; set; } = [];
    public List<string> DataAttributeNamesFound { get; set; } = [];
    public List<string> MatchedHtmlDeclarations { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
}

public sealed class SourceSnapshotReconstructionEvidenceGraph
{
    public List<string> Summary { get; set; } = [];
    public List<SourceSnapshotSectionMapItem> Sections { get; set; } = [];
    public List<SourceSnapshotMediaPlacementItem> Media { get; set; } = [];
    public List<SourceSnapshotBehaviorItem> Behaviors { get; set; } = [];
    public List<SourceSnapshotCssRuleMapItem> CssRules { get; set; } = [];
    public List<SourceSnapshotJsBehaviorReferenceItem> JsReferences { get; set; } = [];
    public List<string> RebuildSuggestions { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class SourceSnapshotReconstructionBundle
{
    public SourceSnapshotDependencyGraph DependencyGraph { get; set; } = new();
    public List<SourceSnapshotSectionMapItem> SectionMap { get; set; } = [];
    public List<SourceSnapshotMediaPlacementItem> MediaPlacementMap { get; set; } = [];
    public List<SourceSnapshotMediaPlacementItem> ResponsiveMediaMap { get; set; } = [];
    public List<SourceSnapshotBehaviorItem> BehaviorMap { get; set; } = [];
    public List<SourceSnapshotBehaviorItem> AnimationSignalMap { get; set; } = [];
    public List<SourceSnapshotCssRuleMapItem> CssRuleMap { get; set; } = [];
    public List<SourceSnapshotJsBehaviorReferenceItem> JsBehaviorReferenceMap { get; set; } = [];
    public SourceSnapshotReconstructionEvidenceGraph ReconstructionEvidenceGraph { get; set; } = new();
    public string AiReconstructionBriefMarkdown { get; set; } = string.Empty;
}
