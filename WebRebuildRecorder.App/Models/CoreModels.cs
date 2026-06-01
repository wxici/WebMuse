using System.Text.Json.Serialization;

namespace WebRebuildRecorder.App.Models;

public static class RecordingAreaModes
{
    public const string FullScreen = "fullscreen";
    public const string Region = "region";
    public const string ActiveWindow = "active-window";
}

public enum WorkflowState
{
    Idle,
    ProjectCreated,
    WebsiteOpened,
    ToolbarReady,
    Countdown,
    Recording,
    AutoObserving,
    ManualControl,
    RecordingStopped,
    ExtractingFrames,
    Packaging,
    GeneratingMarkdown,
    Completed,
    Error
}

public enum RecordingStartMode
{
    OpenUrlBeforeRecording,
    RecordBeforeOpenUrl,
    Manual
}

public static class ObserveModes
{
    public const string QuickScan = "quick-scan";
    public const string Standard = "standard";
    public const string HotspotPriority = "hotspot-priority";
    public const string ManualLed = "manual-led";
}

public sealed class RecordingArea
{
    public string Mode { get; set; } = RecordingAreaModes.FullScreen;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    [JsonIgnore]
    public bool IsRegion => string.Equals(Mode, RecordingAreaModes.Region, StringComparison.OrdinalIgnoreCase)
        && Width > 0
        && Height > 0;

    public static RecordingArea FullScreen() => new() { Mode = RecordingAreaModes.FullScreen };

    public override string ToString()
    {
        return IsRegion ? $"region x={X}, y={Y}, {Width}x{Height}" : Mode;
    }
}

public sealed class RebuildProject
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ProjectSlug
    {
        get => Slug;
        set => Slug = value;
    }
    public string ReferenceUrl { get; set; } = string.Empty;
    public string LocalCodeProjectPath { get; set; } = string.Empty;
    public string UserAssetSourceDirectory { get; set; } = string.Empty;
    public string RootDirectory { get; set; } = string.Empty;
    public string ProjectDirectory { get; set; } = string.Empty;
    public string RecordingId { get; set; } = "001";
    public string LastRunId { get; set; } = string.Empty;
    public string LastRecordingId
    {
        get => RecordingId;
        set => RecordingId = string.IsNullOrWhiteSpace(value) ? "001" : value;
    }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ProjectRecordingSettings RecordingSettings { get; set; } = new();
    public ProjectFrameExtractSettings FrameExtractSettings { get; set; } = new();
    public ProjectBrowserSettings BrowserSettings { get; set; } = new();
    public ProjectAutoObserveSettings AutoObserveSettings { get; set; } = new();

    [JsonIgnore] public string InputDirectory => Path.Combine(ProjectDirectory, "input");
    [JsonIgnore] public string UserIntentDirectory => Path.Combine(InputDirectory, "user-intent");
    [JsonIgnore] public string UserIntentAssetManifestJsonPath => Path.Combine(UserIntentDirectory, "user-intent-assets.json");
    [JsonIgnore] public string UserIntentAssetManifestMarkdownPath => Path.Combine(UserIntentDirectory, "user-intent-assets.md");
    [JsonIgnore] public string RecordingDirectory => Path.Combine(ProjectDirectory, "recording");
    [JsonIgnore] public string FramesDirectory => Path.Combine(ProjectDirectory, "frames");
    [JsonIgnore] public string MarkdownDirectory => Path.Combine(ProjectDirectory, "md");
    [JsonIgnore] public string MarkersDirectory => Path.Combine(ProjectDirectory, "markers");
    [JsonIgnore] public string ActionsDirectory => Path.Combine(ProjectDirectory, "actions");
    [JsonIgnore] public string PackageDirectory => Path.Combine(ProjectDirectory, "package");
    [JsonIgnore] public string ConfigDirectory => Path.Combine(ProjectDirectory, "config");
    [JsonIgnore] public string LogsDirectory => Path.Combine(ProjectDirectory, "logs");
    [JsonIgnore] public string SourceAssetsDirectory => Path.Combine(ProjectDirectory, "source-assets");
    [JsonIgnore] public string ImportedAssetsDirectory => Path.Combine(SourceAssetsDirectory, "imported");
    [JsonIgnore] public string AssetThumbnailsDirectory => Path.Combine(SourceAssetsDirectory, "thumbnails");
    [JsonIgnore] public string AssetManifestPath => Path.Combine(SourceAssetsDirectory, "asset-manifest.json");
    [JsonIgnore] public string RunsDirectory => Path.Combine(ProjectDirectory, "runs");
    [JsonIgnore] public string RecordingFilePath => Path.Combine(RecordingDirectory, $"{Slug}_{RecordingId}.mp4");
}

public sealed class AssetManifest
{
    public string ProjectName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<ProjectAsset> Assets { get; set; } = [];
}

public sealed class ProjectAsset
{
    public string Id { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Type { get; set; } = "raw";
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> PossibleUse { get; set; } = [];
    public string Thumb { get; set; } = string.Empty;
    public string Status { get; set; } = "imported";
}

public sealed class UserIntent
{
    public string FirstImpression { get; set; } = string.Empty;
    public string FavoriteParts { get; set; } = string.Empty;
    public string TargetEffects { get; set; } = string.Empty;
    public string AvoidParts { get; set; } = string.Empty;
    public string DesiredOutcome { get; set; } = string.Empty;
    public string IntentStatus { get; set; } = "provided";

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(FirstImpression) || !string.IsNullOrWhiteSpace(TargetEffects);

    [JsonIgnore]
    public bool IsFallback => string.Equals(IntentStatus, "fallback", StringComparison.OrdinalIgnoreCase);

    public static UserIntent CreateEmptyFallback()
    {
        return new UserIntent
        {
            IntentStatus = "fallback",
            FirstImpression = "用户未填写第一印象。",
            FavoriteParts = "用户未填写喜欢的部分。",
            TargetEffects = "用户未填写目标动效。请先根据截图、action-log、selected-frames 和 observation.md 进行基础网页设计分析。",
            AvoidParts = "用户未填写避免照搬的部分。请在分析中主动区分可借鉴设计语言与不应照搬的元素。",
            DesiredOutcome = "生成网页结构、动效拆解和 Codex 重构指令草案。"
        };
    }
}

public sealed class SiteRunManifest
{
    public string RunId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string UserIntentPath { get; set; } = string.Empty;
    public string GptPackageZipPath { get; set; } = string.Empty;
    public string CodexPackageZipPath { get; set; } = string.Empty;
    public UserIntent UserIntent { get; set; } = new();
}

public sealed class GptAnalysisPackageResult
{
    public string RunId { get; set; } = string.Empty;
    public string ZipPath { get; set; } = string.Empty;
    public string PromptPath { get; set; } = string.Empty;
    public string AssetManifestForGptPath { get; set; } = string.Empty;
    public int SelectedAssetCount { get; set; }
    public int ReferenceFrameCount { get; set; }
    public long ZipSizeBytes { get; set; }
}

public sealed class AssetRequirementReport
{
    public bool HasAssetWarning { get; set; }
    public string BlockingLevel { get; set; } = "none";
    public List<AssetRequirementItem> Items { get; set; } = [];
}

public sealed class AssetRequirementItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public List<string> RequiredAssets { get; set; } = [];
    public string CurrentStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Fallback { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string UserDecision { get; set; } = string.Empty;
}

public sealed class CodexPackageResult
{
    public string RunId { get; set; } = string.Empty;
    public string ZipPath { get; set; } = string.Empty;
    public string FinalTaskPath { get; set; } = string.Empty;
    public long ZipSizeBytes { get; set; }
}

public sealed class ProjectRecordingSettings
{
    public string RecordingMode { get; set; } = RecordingAreaModes.FullScreen;
    public int FrameRate { get; set; } = 30;
    public int Crf { get; set; } = 23;
    public RecordingStartMode RecordingStartMode { get; set; } = RecordingStartMode.RecordBeforeOpenUrl;
}

public sealed class ProjectFrameExtractSettings
{
    public int IntervalMs { get; set; } = 500;
    public string ImageFormat { get; set; } = "JPEG";
    public int ImageQuality { get; set; } = 90;
}

public sealed class ProjectBrowserSettings
{
    public string Browser { get; set; } = "SystemDefault";
    public string WindowPreset { get; set; } = "desktop-1440x900";
}

public sealed class ProjectAutoObserveSettings
{
    public string SpeedPreset { get; set; } = "fast";
    public string ObserveMode { get; set; } = ObserveModes.HotspotPriority;
    public int MaxDurationSeconds { get; set; } = 60;
    public bool TryClickHotspots { get; set; }
    public bool EnableDomTargetCollection { get; set; } = true;
    public bool EnableSafeClick { get; set; }
    public int MaxTargetsPerViewport { get; set; } = 6;
    public int HoverBeforeClickMs { get; set; } = 800;
    public int AfterClickWaitMs { get; set; } = 1800;
    public bool BackAfterInternalNavigation { get; set; } = true;
    public bool ExcludeDangerousActions { get; set; } = true;
}

public sealed class NewProjectOptions
{
    public string ProjectName { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public string RootDirectory { get; set; } = string.Empty;
}

public sealed class ProjectHistoryItem
{
    public string ProjectName { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public string ProjectDirectory { get; set; } = string.Empty;
    public DateTime LastOpenedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string DisplayText => $"{ProjectName} | {ReferenceUrl} | {LastOpenedAt:yyyy-MM-dd HH:mm}";

    [JsonIgnore]
    public bool DirectoryExists => IsValidProjectDirectory(ProjectDirectory);

    [JsonIgnore]
    public string StatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ProjectDirectory) || !Directory.Exists(ProjectDirectory))
            {
                return "项目目录不存在";
            }

            return DirectoryExists
                ? "项目目录可用"
                : "目录存在，但缺少 project.json / project-info.json";
        }
    }

    private static bool IsValidProjectDirectory(string directory)
    {
        return !string.IsNullOrWhiteSpace(directory)
            && Directory.Exists(directory)
            && (File.Exists(Path.Combine(directory, "project.json"))
                || File.Exists(Path.Combine(directory, "project-info.json"))
                || File.Exists(Path.Combine(directory, "project.wrbproj")));
    }
}

public sealed class ToolProfile
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string FfprobePath { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public string ProjectsRootDirectory { get; set; } = string.Empty;
    public string LastProjectName { get; set; } = string.Empty;
    public string LastBrowser { get; set; } = "SystemDefault";
    public string LastRecordingMode { get; set; } = RecordingAreaModes.FullScreen;
    public HotkeySettings Hotkeys { get; set; } = new();
    public FloatingToolbarSettings FloatingToolbar { get; set; } = new();
}

public sealed class FloatingToolbarSettings
{
    public string Mode { get; set; } = "compact";
    public double? Left { get; set; }
    public double Top { get; set; } = 80;
    public double OpacityIdle { get; set; } = 0.70;
    public double OpacityHover { get; set; } = 0.95;
    public bool AutoCollapse { get; set; } = true;
}

public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = true;
    public string PauseAuto { get; set; } = "Ctrl+Alt+F8";
    public string ResumeAuto { get; set; } = "Ctrl+Alt+F9";
    public string Marker { get; set; } = "Ctrl+Alt+F10";
    public string Screenshot { get; set; } = "Ctrl+Alt+F11";
    public string StopRecording { get; set; } = "Ctrl+Alt+F12";
}

public sealed class RecordingOptions
{
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string OutputPath { get; set; } = string.Empty;
    public int FrameRate { get; set; } = 30;
    public RecordingArea Area { get; set; } = RecordingArea.FullScreen();
    public bool DrawMouse { get; set; } = true;
    public string VideoCodec { get; set; } = "libx264";
    public string Preset { get; set; } = "ultrafast";
    public int Crf { get; set; } = 23;
}

public sealed class RecordingInfo
{
    public string VideoPath { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public RecordingArea RecordingArea { get; set; } = RecordingArea.FullScreen();
    public string Browser { get; set; } = string.Empty;
    public bool IsAutoObserve { get; set; }
    public bool HadManualTakeover { get; set; }
    public RecordingStartMode RecordingStartMode { get; set; } = RecordingStartMode.RecordBeforeOpenUrl;
    public bool RecordingStartedBeforeOpenUrl { get; set; }
    public DateTime? OpenUrlTime { get; set; }
    public bool IncludesInitialLoadAnimation { get; set; }
    public bool UsedFloatingToolbar { get; set; } = true;
    public string FloatingToolbarMode { get; set; } = "compact";
    public double FloatingToolbarLeft { get; set; }
    public double FloatingToolbarTop { get; set; } = 80;
    public bool FloatingToolbarMayOcclude { get; set; }
}

public sealed class FrameExtractOptions
{
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string VideoPath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string ProjectDirectory { get; set; } = string.Empty;
    public string Prefix { get; set; } = "frame";
    public int IntervalMs { get; set; } = 500;
    public string Format { get; set; } = "JPEG";
    public int JpegQuality { get; set; } = 90;
    public bool UseOriginalSize { get; set; } = true;
    public int CustomWidth { get; set; } = 640;
    public int CustomHeight { get; set; } = 480;
    public bool KeepAspectRatio { get; set; } = true;
    public int? MaxFrames { get; set; }
    public double SourceFrameRate { get; set; } = 30;
    public bool AllowDuplicateFramesAboveSourceFps { get; set; }
}

public sealed class FrameExtractResult
{
    public string OutputDirectory { get; set; } = string.Empty;
    public string FrameIndexCsvPath { get; set; } = string.Empty;
    public string FrameIndexMarkdownPath { get; set; } = string.Empty;
    public int TotalFrames { get; set; }
    public int IntervalMs { get; set; }
    public double EffectiveFps { get; set; }
    public string Format { get; set; } = "JPEG";
    public List<FrameIndexItem> Frames { get; set; } = [];
}

public sealed class FrameIndexItem
{
    public int Index { get; set; }
    public string FileName { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ChatGptPackageResult
{
    public string ZipPath { get; set; } = string.Empty;
    public long ZipSizeBytes { get; set; }
    public int SelectedFrameCount { get; set; }
    public string PromptText { get; set; } = string.Empty;
    public string SelectedFramesIndexMarkdownPath { get; set; } = string.Empty;
    public string SelectedFramesIndexCsvPath { get; set; } = string.Empty;
}

public sealed class DeliveryProfile
{
    public int MaxSelectedFrames { get; set; } = 50;
    public int MarkerContextFrameCount { get; set; } = 5;
    public bool IncludeActionContextFrames { get; set; } = true;
    public int ActionContextFrameCount { get; set; } = 3;
    public int WarnPackageSizeMb { get; set; } = 50;
    public int StrongWarnPackageSizeMb { get; set; } = 100;
    public int UploadImageMaxWidth { get; set; } = 1600;
    public int UploadImageJpegQuality { get; set; } = 82;
}

public sealed class ActionProfile
{
    public string SpeedPreset { get; set; } = "fast";
    public string ObserveMode { get; set; } = ObserveModes.HotspotPriority;
    public bool TryClickHotspots { get; set; }
    public bool EnableDomTargetCollection { get; set; } = true;
    public bool EnableSafeClick { get; set; }
    public int MaxTargetsPerViewport { get; set; } = 6;
    public int HoverBeforeClickMs { get; set; } = 800;
    public int AfterClickWaitMs { get; set; } = 1800;
    public bool BackAfterInternalNavigation { get; set; } = true;
    public bool ExcludeDangerousActions { get; set; } = true;
    [JsonIgnore] public List<InteractionTarget> RuntimeInteractionTargets { get; set; } = new();
    [JsonIgnore] public string RuntimeInteractionCollectionStatus { get; set; } = InteractionTargetCollectionStatus.Unknown;
    public int AutoObserveDurationSeconds { get; set; } = 55;
    public int MouseMoveDurationMsMin { get; set; } = 180;
    public int MouseMoveDurationMsMax { get; set; } = 420;
    public int HoverDurationMsMin { get; set; } = 300;
    public int HoverDurationMsMax { get; set; } = 700;
    public int ScrollDistancePxMin { get; set; } = 650;
    public int ScrollDistancePxMax { get; set; } = 1200;
    public int ScrollPauseMsMin { get; set; } = 250;
    public int ScrollPauseMsMax { get; set; } = 600;
    public int ClickWaitMsMin { get; set; } = 1500;
    public int ClickWaitMsMax { get; set; } = 3500;
    public int PageLoadWaitMs { get; set; } = 3000;
    public int MaxSamePageInteractions { get; set; } = 50;
    public int MaxAutoScrollSteps { get; set; } = 18;
    public bool StopAtPageBottom { get; set; } = true;
    public int MaxNavigationDepth { get; set; } = 2;
    public List<string> DangerousKeywords { get; set; } =
    [
        "删除", "移除", "提交", "付款", "购买", "注册", "登录", "退出", "授权", "下载", "上传", "支付", "确认订单",
        "delete", "remove", "submit", "pay", "buy", "purchase", "login", "logout", "signup", "download", "upload", "checkout"
    ];
}

public sealed class AutoFlowOptions
{
    public bool ShowToolbarAfterOpenWebsite { get; set; } = true;
    public bool ShowCountdownBeforeRecording { get; set; } = true;
    public bool AutoObservePage { get; set; } = true;
    public bool AutoExtractAfterStop { get; set; } = true;
    public bool AutoPackageAfterExtract { get; set; } = true;
    public bool AutoGenerateMarkdownAfterPackage { get; set; } = true;
    public bool ShowCompletionPrompt { get; set; } = true;
}

public sealed class BrowserProfile
{
    public string DefaultBrowser { get; set; } = "SystemDefault";
    public bool EnablePlaywright { get; set; }
    public int WindowWidth { get; set; } = 1440;
    public int WindowHeight { get; set; } = 900;
    public bool UseNewUserDataDirectory { get; set; } = true;
    public bool AllowExternalLinkClick { get; set; }
    public RecordingArea RecordingArea { get; set; } = RecordingArea.FullScreen();
}

public sealed class MdGenerationOptions
{
    public string ObservationTemplatePath { get; set; } = "Templates/observation-template.md";
    public bool IncludeFullFrameTable { get; set; }
    public int MaxFrameRowsInMarkdown { get; set; } = 500;
    public bool IncludeActionLog { get; set; } = true;
    public bool IncludeMarkers { get; set; } = true;
    public bool IncludeChatGptPrompt { get; set; } = true;
    public bool IncludeCodexReservedSection { get; set; } = true;
}

public sealed class ObservationContext
{
    public RebuildProject Project { get; set; } = new();
    public RecordingInfo RecordingInfo { get; set; } = new();
    public FrameExtractResult? FrameResult { get; set; }
    public FrameExtractOptions? FrameOptions { get; set; }
    public ActionProfile ActionProfile { get; set; } = new();
    public MdGenerationOptions MarkdownOptions { get; set; } = new();
    public string OutputPath { get; set; } = string.Empty;
}

public sealed class ProjectContext
{
    public RebuildProject Project { get; set; } = new();
    public RecordingInfo? RecordingInfo { get; set; }
    public FrameExtractResult? FrameResult { get; set; }
}
