using WebRebuildRecorder.App.Core.State;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public class WrbProjectManifest
{
    public string SchemaVersion { get; set; } = WrbProjectSchema.CurrentSchemaVersion;
    public string AppVersion { get; set; } = WrbProjectSchema.CurrentAppVersion;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectRoot { get; set; } = ".";
    public string ReferenceUrl { get; set; } = string.Empty;
    public ProjectState State { get; set; } = ProjectState.ProjectCreated;
    public string CurrentOutputVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public WrbProjectPaths Paths { get; set; } = new();
    public WrbProjectFeatures Features { get; set; } = new();
    public WrbCodexTaskSummary? LastCodexTask { get; set; }
    public string LastExportPath { get; set; } = string.Empty;
}

public sealed class ProjectFileManifest : WrbProjectManifest
{
}

public sealed class WrbProjectPaths
{
    public string Input { get; set; } = ProjectDirectoryV2.Input;
    public string Assets { get; set; } = ProjectDirectoryV2.Assets;
    public string Theme { get; set; } = ProjectDirectoryV2.Theme;
    public string Observation { get; set; } = ProjectDirectoryV2.Observation;
    public string CodexTask { get; set; } = ProjectDirectoryV2.CodexTask;
    public string OutputCurrent { get; set; } = ProjectDirectoryV2.OutputCurrent;
    public string OutputVersions { get; set; } = ProjectDirectoryV2.OutputVersions;
    public string Tune { get; set; } = ProjectDirectoryV2.Tune;
    public string Maps { get; set; } = ProjectDirectoryV2.Maps;
    public string Exports { get; set; } = ProjectDirectoryV2.Exports;
    public string Logs { get; set; } = ProjectDirectoryV2.Logs;
    public string Versions { get; set; } = ProjectDirectoryV2.Versions;
    public string Runtime { get; set; } = ProjectDirectoryV2.Runtime;
}

public sealed class WrbProjectFeatures
{
    public bool UsesLegacyProjectJson { get; set; } = true;
    public bool HasTheme { get; set; }
    public bool HasContentMap { get; set; }
    public bool HasTuneOverrides { get; set; }
    public bool HasCodexTaskPackage { get; set; }
    public bool HasValidatedOutput { get; set; }
}

public sealed class WrbCodexTaskSummary
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
}

public static class WrbProjectSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p0";
    public const string CurrentAppVersion = "0.1.9-alpha";
    public const string FileName = "project.wrbproj";
}
