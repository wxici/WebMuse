namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ProjectLock
{
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public DateTimeOffset LockedAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public int ProcessId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectLockStatuses.Running;
    public bool AllowCancel { get; set; } = true;
    public string RecoveryHint { get; set; } = string.Empty;
    public string LockFilePath { get; set; } = string.Empty;
    public string AbnormalExit { get; set; } = string.Empty;
}

public static class ProjectLockStatuses
{
    public const string Running = "running";
    public const string Released = "released";
    public const string Abandoned = "abandoned";
}
