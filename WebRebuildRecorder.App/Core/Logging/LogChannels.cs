namespace WebRebuildRecorder.App.Core.Logging;

public static class LogChannels
{
    public const string App = "app.log";
    public const string Project = "project.log";
    public const string Observation = "observation.log";
    public const string CodexTask = "codex-task.log";
    public const string Security = "security.log";
    public const string CodexRun = "codex-run.log";
    public const string Build = "build.log";
    public const string Validation = "validation.log";
    public const string Export = "export.log";
    public const string Error = "error.log";

    public static IReadOnlyList<string> All { get; } =
    [
        App,
        Project,
        Observation,
        CodexTask,
        Security,
        CodexRun,
        Build,
        Validation,
        Export,
        Error
    ];
}

public sealed class LoggingPlan
{
    public IReadOnlyList<string> Channels => LogChannels.All;
    public string UserVisiblePolicy { get; init; } = "Normal users see summaries. Advanced and developer modes may inspect full logs.";
    public string StorageDirectory { get; init; } = "logs";
}
