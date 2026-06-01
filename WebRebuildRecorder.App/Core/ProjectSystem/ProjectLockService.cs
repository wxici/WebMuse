using System.Diagnostics;
using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ProjectLockService
{
    private const string LockFileName = "project.lock";

    public bool IsLocked(string projectPath)
    {
        if (!TryReadLock(projectPath, out var existing, out _))
        {
            return true;
        }

        return existing is not null
            && !string.Equals(existing.Status, ProjectLockStatuses.Released, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(existing.Status, ProjectLockStatuses.Abandoned, StringComparison.OrdinalIgnoreCase);
    }

    public ProjectLock CreateLock(string projectPath, string taskType)
    {
        return CreateLock(projectPath, taskType, projectId: string.Empty, reason: string.Empty);
    }

    public ProjectLock CreateLock(string projectPath, string taskType, string projectId, string reason)
    {
        if (!TryReadLock(projectPath, out var existing, out var readError))
        {
            throw new InvalidDataException(readError);
        }

        if (existing is not null
            && !string.Equals(existing.Status, ProjectLockStatuses.Released, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(existing.Status, ProjectLockStatuses.Abandoned, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project is already locked: {GetLockFilePath(projectPath)}");
        }

        var lockFilePath = GetLockFilePath(projectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);

        var now = DateTimeOffset.UtcNow;
        var projectLock = new ProjectLock
        {
            ProjectId = projectId.Trim(),
            TaskId = Guid.NewGuid().ToString("N"),
            TaskType = string.IsNullOrWhiteSpace(taskType) ? "unknown" : taskType.Trim(),
            LockedAt = now,
            StartedAt = now,
            ProcessId = Environment.ProcessId,
            MachineName = Environment.MachineName,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Project task is running." : reason.Trim(),
            Status = ProjectLockStatuses.Running,
            AllowCancel = true,
            RecoveryHint = "If this task stops unexpectedly, inspect logs and mark this lock abandoned before retrying.",
            LockFilePath = lockFilePath
        };

        WriteLock(lockFilePath, projectLock);
        return projectLock;
    }

    public ProjectLock? ReadLock(string projectPath)
    {
        if (!TryReadLock(projectPath, out var projectLock, out var error))
        {
            throw new InvalidDataException(error);
        }

        return projectLock;
    }

    public bool TryReadLock(string projectPath, out ProjectLock? projectLock, out string? error)
    {
        projectLock = null;
        error = null;
        var lockFilePath = GetLockFilePath(projectPath);
        if (!File.Exists(lockFilePath))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllText(lockFilePath);
            projectLock = JsonSerializer.Deserialize<ProjectLock>(json, WrbJsonOptions.Default);
            if (projectLock is null)
            {
                error = $"Project lock file is empty or invalid: {lockFilePath}";
                return false;
            }

            if (projectLock.LockedAt == default)
            {
                projectLock.LockedAt = projectLock.StartedAt == default ? DateTimeOffset.UtcNow : projectLock.StartedAt;
            }

            if (projectLock.StartedAt == default)
            {
                projectLock.StartedAt = projectLock.LockedAt;
            }

            if (string.IsNullOrWhiteSpace(projectLock.LockFilePath))
            {
                projectLock.LockFilePath = lockFilePath;
            }

            if (string.IsNullOrWhiteSpace(projectLock.MachineName))
            {
                projectLock.MachineName = Environment.MachineName;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Project lock file JSON is invalid: {lockFilePath}. {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            error = $"Project lock file could not be read: {lockFilePath}. {ex.Message}";
            return false;
        }
    }

    public void ReleaseLock(string projectPath)
    {
        var lockFilePath = GetLockFilePath(projectPath);
        if (File.Exists(lockFilePath))
        {
            File.Delete(lockFilePath);
        }
    }

    public ProjectLock MarkAbandoned(string projectPath, string reason)
    {
        var lockFilePath = GetLockFilePath(projectPath);
        var projectLock = TryReadLock(projectPath, out var existing, out _)
            ? existing
            : null;

        projectLock ??= new ProjectLock
        {
            TaskId = Guid.NewGuid().ToString("N"),
            TaskType = "unknown",
            StartedAt = DateTimeOffset.UtcNow,
            ProcessId = Process.GetCurrentProcess().Id,
            MachineName = Environment.MachineName,
            LockFilePath = lockFilePath
        };

        if (projectLock.LockedAt == default)
        {
            projectLock.LockedAt = DateTimeOffset.UtcNow;
        }

        projectLock.Status = ProjectLockStatuses.Abandoned;
        projectLock.AllowCancel = false;
        projectLock.AbnormalExit = string.IsNullOrWhiteSpace(reason) ? "Marked abandoned without a reason." : reason.Trim();
        projectLock.Reason = projectLock.AbnormalExit;
        projectLock.RecoveryHint = "Review the previous task state, logs, and generated output before creating a new lock.";
        projectLock.LockFilePath = lockFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
        WriteLock(lockFilePath, projectLock);
        return projectLock;
    }

    public static string GetLockFilePath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Project path cannot be empty.", nameof(projectPath));
        }

        return Path.Combine(Path.GetFullPath(Environment.ExpandEnvironmentVariables(projectPath.Trim())), LockFileName);
    }

    private static void WriteLock(string lockFilePath, ProjectLock projectLock)
    {
        var json = JsonSerializer.Serialize(projectLock, WrbJsonOptions.Default);
        File.WriteAllText(lockFilePath, json);
    }
}
