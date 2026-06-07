namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ProjectDirectoryV2
{
    public const string Input = "input";
    public const string Assets = "input/assets";
    public const string AssetsOriginal = "input/assets/original";
    public const string AssetsSelected = "input/assets/selected";
    public const string Theme = "theme";
    public const string Observation = "observation";
    public const string ObservationScreenshots = "observation/screenshots";
    public const string ObservationDom = "observation/dom";
    public const string SourceSnapshot = "source-snapshot";
    public const string SourceSnapshotRaw = "source-snapshot/raw";
    public const string SourceSnapshotRendered = "source-snapshot/rendered";
    public const string SourceSnapshotResources = "source-snapshot/resources";
    public const string SourceSnapshotAnalysis = "source-snapshot/analysis";
    public const string CodexTask = "codex-task";
    public const string OutputSite = "output-site";
    public const string OutputCurrent = "output-site/current";
    public const string OutputVersions = "output-site/versions";
    public const string Tune = "tune";
    public const string Maps = "maps";
    public const string Exports = "exports";
    public const string Logs = "logs";
    public const string Versions = "versions";
    public const string Review = "review";
    public const string Runtime = "runtime";
    public const string CodexWorkspace = "codex-workspace";

    public static IReadOnlyList<string> RequiredDirectories { get; } =
    [
        Input,
        Assets,
        AssetsOriginal,
        AssetsSelected,
        Theme,
        Observation,
        ObservationScreenshots,
        ObservationDom,
        SourceSnapshot,
        SourceSnapshotRaw,
        SourceSnapshotRendered,
        SourceSnapshotResources,
        SourceSnapshotAnalysis,
        CodexTask,
        OutputSite,
        OutputCurrent,
        OutputVersions,
        Tune,
        Maps,
        Exports,
        Logs,
        Versions,
        Review,
        Runtime,
        CodexWorkspace
    ];

    public static IReadOnlyDictionary<string, string> CreateDefaultPathMap()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["input"] = Input,
            ["assets"] = Assets,
            ["assetsOriginal"] = AssetsOriginal,
            ["assetsSelected"] = AssetsSelected,
            ["theme"] = Theme,
            ["observation"] = Observation,
            ["sourceSnapshot"] = SourceSnapshot,
            ["codexTask"] = CodexTask,
            ["outputCurrent"] = OutputCurrent,
            ["outputVersions"] = OutputVersions,
            ["tune"] = Tune,
            ["maps"] = Maps,
            ["exports"] = Exports,
            ["logs"] = Logs,
            ["versions"] = Versions,
            ["runtime"] = Runtime
        };
    }
}
