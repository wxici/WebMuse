namespace WebRebuildRecorder.App.Models;

public sealed class UserIntentAssetManifest
{
    public int SchemaVersion { get; set; } = 1;
    public List<UserIntentAssetItem> Items { get; set; } = new();
}

public sealed class UserIntentAssetItem
{
    public string Id { get; set; } = "";
    public string Field { get; set; } = "";
    public string FieldDisplayName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class UserIntentFieldNames
{
    public const string FirstImpression = "first-impression";
    public const string FavoriteParts = "favorite-parts";
    public const string TargetEffects = "target-effects";
    public const string Avoid = "avoid";
    public const string DesiredResult = "desired-result";

    public static readonly string[] All =
    [
        FirstImpression,
        FavoriteParts,
        TargetEffects,
        Avoid,
        DesiredResult
    ];

    public static string GetDisplayName(string field) => field switch
    {
        FirstImpression => "第一印象",
        FavoriteParts => "喜欢的部分",
        TargetEffects => "目标动效",
        Avoid => "避免内容",
        DesiredResult => "期望结果",
        _ => field
    };
}
