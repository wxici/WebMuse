namespace WebRebuildRecorder.App.Core.State;

public enum ProjectState
{
    ProjectCreated,
    InputCompleted,
    AssetsCopied,
    ThemeSelected,
    ObservationReady,
    TaskPackageGenerated,
    CodexRunning,
    CodexFailed,
    CodexCompleted,
    OutputValidated,
    PreviewReady,
    TuneDirty,
    TuneSaved,
    ExportReady,
    Exported,
    Archived
}

