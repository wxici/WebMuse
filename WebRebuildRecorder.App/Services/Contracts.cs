using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public interface IBrowserController
{
    Task OpenAsync(string url, BrowserProfile profile);
    Task StartAutoObserveAsync(ActionProfile profile, CancellationToken token);
    Task PauseAutoObserveAsync();
    Task ResumeAutoObserveAsync();
    Task StopAutoObserveAsync();
    Task<string?> GetCurrentUrlAsync();
    Task<string?> GetPageTitleAsync();
}

public interface IDomInteractionProvider
{
    Task<IReadOnlyList<InteractionTarget>> CollectInteractionTargetsAsync(
        Uri baseUri,
        CancellationToken cancellationToken = default);
}

public interface IProjectManager
{
    RebuildProject? CurrentProject { get; }
    Task<IReadOnlyList<ProjectHistoryItem>> LoadRecentProjectsAsync();
    Task<RebuildProject> CreateNewProjectAsync(NewProjectOptions options);
    Task<RebuildProject> OpenProjectAsync(string projectDirectory);
    Task CloseCurrentProjectAsync();
    Task SaveCurrentProjectAsync();
}

public interface IScreenRecorder
{
    bool IsRecording { get; }
    DateTime? StartedAt { get; }
    Task StartAsync(RecordingOptions options);
    Task StopAsync();
}

public interface IFrameExtractor
{
    Task<FrameExtractResult> ExtractAsync(FrameExtractOptions options, CancellationToken token = default);
}

public interface IObservationMarkdownGenerator
{
    Task GenerateAsync(ObservationContext context);
}

public interface IAiTaskGenerator
{
    Task<string> GenerateCodexTaskAsync(ProjectContext context);
    Task<string> GenerateFixTaskAsync(ProjectContext context);
}
