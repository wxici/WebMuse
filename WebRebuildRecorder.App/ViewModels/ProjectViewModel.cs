namespace WebRebuildRecorder.App.ViewModels;

public sealed class ProjectViewModel
{
    public string ProjectName { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public string OutputRootDirectory { get; set; } = string.Empty;
}
