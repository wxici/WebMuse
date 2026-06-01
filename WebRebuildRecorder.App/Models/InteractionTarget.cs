using System.Text.Json.Serialization;

namespace WebRebuildRecorder.App.Models;

public sealed class InteractionTarget
{
    public string Id { get; set; } = "";
    public string TagName { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Href { get; set; }
    public string? AriaLabel { get; set; }
    public string? Role { get; set; }
    public string? Title { get; set; }
    public string? ClassName { get; set; }
    public string? Cursor { get; set; }

    public ElementRect Rect { get; set; } = new();

    public bool Visible { get; set; }
    public bool InViewport { get; set; }
    public bool SameOrigin { get; set; }

    public string UrlKind { get; set; } = "unknown";
    public int Priority { get; set; }
    public string RecommendedAction { get; set; } = "hover";
    public string Reason { get; set; } = "";
}

public sealed class ElementRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    [JsonIgnore]
    public double CenterX => X + Width / 2;

    [JsonIgnore]
    public double CenterY => Y + Height / 2;
}

public sealed class InteractionTargetCollectionResult
{
    public string Status { get; set; } = InteractionTargetCollectionStatus.Unknown;
    public string? Reason { get; set; }
    public string? Fallback { get; set; }
    public List<InteractionTarget> Targets { get; set; } = new();
}

public static class InteractionTargetCollectionStatus
{
    public const string Unknown = "unknown";
    public const string Available = "available";
    public const string Unavailable = "unavailable";
    public const string Failed = "failed";
    public const string Fallback = "fallback";
}
