namespace WebRebuildRecorder.App.Models;

public sealed class SourceSnapshotResult
{
    public string SnapshotId { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool HttpSucceeded { get; set; }
    public int? StatusCode { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public bool RenderSucceeded { get; set; }
    public string RenderError { get; set; } = string.Empty;
    public SourceSnapshotPaths Paths { get; set; } = new();
    public SourceSnapshotResourceManifest ResourceManifest { get; set; } = new();
    public SourceSnapshotAnalysis Analysis { get; set; } = new();
    public SourceSnapshotViewportPreset CaptureViewport { get; set; } = new();
    public List<SourceSnapshotTextResource> TextResources { get; set; } = [];
    public SourceSnapshotDependencyGraph DependencyGraph { get; set; } = new();
    public SourceSnapshotReconstructionEvidenceGraph ReconstructionGraph { get; set; } = new();
}

public sealed class SourceSnapshotPaths
{
    public string Root { get; set; } = string.Empty;
    public string RawHtml { get; set; } = string.Empty;
    public string ResponseHeaders { get; set; } = string.Empty;
    public string RenderedDom { get; set; } = string.Empty;
    public string VisibleText { get; set; } = string.Empty;
    public string Viewport { get; set; } = string.Empty;
    public string ElementMap { get; set; } = string.Empty;
    public string FirstScreenPng { get; set; } = string.Empty;
    public string ResourceManifest { get; set; } = string.Empty;
    public string TextResourceManifest { get; set; } = string.Empty;
    public string DependencyGraph { get; set; } = string.Empty;
    public string SectionMap { get; set; } = string.Empty;
    public string MediaPlacementMap { get; set; } = string.Empty;
    public string ResponsiveMediaMap { get; set; } = string.Empty;
    public string BehaviorMap { get; set; } = string.Empty;
    public string AnimationSignalMap { get; set; } = string.Empty;
    public string CssRuleMap { get; set; } = string.Empty;
    public string JsBehaviorReferenceMap { get; set; } = string.Empty;
    public string ReconstructionEvidenceGraph { get; set; } = string.Empty;
    public string AiReconstructionBrief { get; set; } = string.Empty;
    public string ReportMarkdown { get; set; } = string.Empty;
    public string ReportJson { get; set; } = string.Empty;
}

public sealed class SourceSnapshotResourceManifest
{
    public List<SourceSnapshotResourceItem> Css { get; set; } = [];
    public List<SourceSnapshotResourceItem> JavaScript { get; set; } = [];
    public List<SourceSnapshotResourceItem> Images { get; set; } = [];
    public List<SourceSnapshotResourceItem> Fonts { get; set; } = [];
    public List<SourceSnapshotResourceItem> Videos { get; set; } = [];
    public List<SourceSnapshotResourceItem> Other { get; set; } = [];
}

public sealed class SourceSnapshotResourceItem
{
    public string Url { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string SourceTag { get; set; } = string.Empty;
    public bool IsSameOrigin { get; set; }
}

public sealed class SourceSnapshotRenderedEvidence
{
    public bool RenderSucceeded { get; set; }
    public string RenderError { get; set; } = string.Empty;
    public string DomHtml { get; set; } = string.Empty;
    public string VisibleText { get; set; } = string.Empty;
    public SourceSnapshotViewport Viewport { get; set; } = new();
    public List<SourceSnapshotElementItem> Elements { get; set; } = [];
    public List<SourceSnapshotStyleSignal> StyleSamples { get; set; } = [];
}

public sealed class SourceSnapshotViewport
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double DevicePixelRatio { get; set; }
    public int ScrollHeight { get; set; }
}

public sealed class SourceSnapshotElementItem
{
    public string Tag { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string Src { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public Dictionary<string, string> DataAttributes { get; set; } = [];
    public List<string> CssClasses { get; set; } = [];
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class SourceSnapshotStyleSignal
{
    public string Selector { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public string FontFamily { get; set; } = string.Empty;
    public string FontSize { get; set; } = string.Empty;
    public string FontWeight { get; set; } = string.Empty;
}

public sealed class SourceSnapshotAnalysis
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Headings { get; set; } = [];
    public List<string> Buttons { get; set; } = [];
    public List<string> Links { get; set; } = [];
    public List<string> LayoutSignals { get; set; } = [];
    public List<string> ColorSignals { get; set; } = [];
    public List<string> TypographySignals { get; set; } = [];
    public List<string> AssetSlotCandidates { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
