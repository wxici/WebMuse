using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class CodexTaskRunService
{
    public async Task<CodexTaskRunRecord> CreateRunAsync(
        string projectRoot,
        string projectId,
        string taskPackageId,
        CancellationToken cancellationToken = default)
    {
        ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var now = DateTimeOffset.UtcNow;
        var record = new CodexTaskRunRecord
        {
            SchemaVersion = CodexTaskRunSchema.CurrentSchemaVersion,
            ProjectId = projectId?.Trim() ?? string.Empty,
            TaskPackageId = taskPackageId?.Trim() ?? string.Empty,
            RunId = CreateRunId(now),
            CreatedAt = now,
            Status = CodexTaskRunStatus.Created
        };

        await SaveAsync(projectRoot, record, cancellationToken);
        return record;
    }

    public Task<CodexTaskRunRecord> MarkRunningAsync(
        string projectRoot,
        string runId,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(projectRoot, runId, CodexTaskRunStatus.Running, null, null, cancellationToken);
    }

    public Task<CodexTaskRunRecord> MarkQueuedAsync(
        string projectRoot,
        string runId,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(projectRoot, runId, CodexTaskRunStatus.Queued, null, null, cancellationToken);
    }

    public Task<CodexTaskRunRecord> MarkSucceededAsync(
        string projectRoot,
        string runId,
        IEnumerable<string>? outputRelativePaths = null,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(projectRoot, runId, CodexTaskRunStatus.Succeeded, null, outputRelativePaths, cancellationToken);
    }

    public Task<CodexTaskRunRecord> MarkFailedAsync(
        string projectRoot,
        string runId,
        TaskFailureCategory category,
        string message,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        var failure = new CodexTaskFailureItem
        {
            Category = category.ToString(),
            Message = string.IsNullOrWhiteSpace(message) ? "Task failed." : message.Trim(),
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim()
        };

        return TransitionAsync(projectRoot, runId, CodexTaskRunStatus.Failed, failure, null, cancellationToken);
    }

    public async Task<CodexTaskRunRecord> LoadAsync(
        string projectRoot,
        string runId,
        CancellationToken cancellationToken = default)
    {
        var path = GetRunRecordPath(projectRoot, runId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Codex task run record was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var record = await JsonSerializer.DeserializeAsync<CodexTaskRunRecord>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (record is null)
            {
                throw new InvalidDataException($"Codex task run record is empty or invalid: {path}");
            }

            NormalizeAndValidate(projectRoot, record);
            return record;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Codex task run record JSON is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        CodexTaskRunRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        NormalizeAndValidate(projectRoot, record);
        var path = GetRunRecordPath(projectRoot, record.RunId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, record, WrbJsonOptions.Default, cancellationToken);
    }

    public static string GetRunRecordPath(string projectRoot, string runId)
    {
        var safeRunId = ProjectPackagePathHelpers.NormalizeRelativeToken(runId, "run id");
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            $"{CodexTaskRunSchema.RunsRootRelativePath}/{safeRunId}/{CodexTaskRunSchema.RunRecordFileName}",
            "codex task run record");
    }

    private async Task<CodexTaskRunRecord> TransitionAsync(
        string projectRoot,
        string runId,
        CodexTaskRunStatus nextStatus,
        CodexTaskFailureItem? failure,
        IEnumerable<string>? outputRelativePaths,
        CancellationToken cancellationToken)
    {
        var record = await LoadAsync(projectRoot, runId, cancellationToken);
        EnsureTransition(record.Status, nextStatus);
        var now = DateTimeOffset.UtcNow;

        if (nextStatus == CodexTaskRunStatus.Running)
        {
            record.StartedAt ??= now;
        }

        if (IsTerminal(nextStatus))
        {
            record.CompletedAt ??= now;
        }

        record.Status = nextStatus;
        if (failure is not null)
        {
            record.Failures.Add(failure);
        }

        if (outputRelativePaths is not null)
        {
            record.OutputRelativePaths = outputRelativePaths
                .Select(path => ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, path, "run output path"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        await SaveAsync(projectRoot, record, cancellationToken);
        return record;
    }

    private static void EnsureTransition(CodexTaskRunStatus current, CodexTaskRunStatus next)
    {
        if (IsTerminal(current))
        {
            throw new InvalidOperationException($"Cannot transition terminal run status {current} to {next}.");
        }

        var allowed = current switch
        {
            CodexTaskRunStatus.Created => next is CodexTaskRunStatus.Queued
                or CodexTaskRunStatus.Running
                or CodexTaskRunStatus.Cancelled,
            CodexTaskRunStatus.Queued => next is CodexTaskRunStatus.Running or CodexTaskRunStatus.Cancelled,
            CodexTaskRunStatus.Running => next is CodexTaskRunStatus.Succeeded
                or CodexTaskRunStatus.Failed
                or CodexTaskRunStatus.TimedOut
                or CodexTaskRunStatus.Cancelled,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"Invalid Codex task run transition: {current} -> {next}.");
        }
    }

    private static bool IsTerminal(CodexTaskRunStatus status)
    {
        return status is CodexTaskRunStatus.Succeeded
            or CodexTaskRunStatus.Failed
            or CodexTaskRunStatus.Cancelled
            or CodexTaskRunStatus.TimedOut;
    }

    private static void NormalizeAndValidate(string projectRoot, CodexTaskRunRecord record)
    {
        record.SchemaVersion = CodexTaskRunSchema.CurrentSchemaVersion;
        record.ProjectId = record.ProjectId?.Trim() ?? string.Empty;
        record.TaskPackageId = record.TaskPackageId?.Trim() ?? string.Empty;
        record.RunId = ProjectPackagePathHelpers.NormalizeRelativeToken(
            string.IsNullOrWhiteSpace(record.RunId) ? CreateRunId(DateTimeOffset.UtcNow) : record.RunId,
            "run id");
        record.CreatedAt = record.CreatedAt == default ? DateTimeOffset.UtcNow : record.CreatedAt;
        record.Failures ??= [];
        record.OutputRelativePaths = record.OutputRelativePaths?
            .Select(path => ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, path, "run output path"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        foreach (var failure in record.Failures)
        {
            failure.Category = string.IsNullOrWhiteSpace(failure.Category) ? TaskFailureCategory.Unknown.ToString() : failure.Category.Trim();
            failure.Message = failure.Message?.Trim() ?? string.Empty;
            failure.Detail = string.IsNullOrWhiteSpace(failure.Detail) ? null : failure.Detail.Trim();
        }
    }

    private static string CreateRunId(DateTimeOffset now)
    {
        return $"run-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..46];
    }
}
