using System.Text.Json;
using WebRebuildRecorder.App.Core.Logging;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface ISnapshotRestoreService
{
    Task<IReadOnlyList<ProjectSnapshotManifest>> ListSnapshotsAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task<ProjectSnapshotManifest> LoadSnapshotAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task<SnapshotValidationResult> ValidateSnapshotAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task<SnapshotRestorePlan> CreateRestorePlanAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task<SnapshotRestoreResult> RestoreAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default);
}

public sealed class SnapshotRestoreService : ISnapshotRestoreService
{
    private static readonly string[] ForbiddenDirectorySegments =
    [
        ".git",
        ".ssh",
        ".codex",
        ".openai",
        "bin",
        "obj",
        ".vs",
        "node_modules"
    ];

    private static readonly string[] SkippedExtensions =
    [
        ".zip",
        ".mp4",
        ".webm",
        ".mov",
        ".avi",
        ".mkv",
        ".log",
        ".dll",
        ".exe"
    ];

    private static readonly string[] AllowedExactRestoreTargets =
    [
        WrbProjectSchema.FileName,
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath,
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        ObservationPackageSchema.RelativePath,
        "observation/legacy-bridge-report.json"
    ];

    private static readonly string[] AllowedRestoreTargetPrefixes =
    [
        ConstructionPackageContextSchema.ContextRootRelativePath + "/",
        ProjectDirectoryV2.OutputCurrent + "/"
    ];

    private readonly IProjectSnapshotService snapshotService;
    private readonly IProjectLogService logService;

    public SnapshotRestoreService()
        : this(new ProjectSnapshotService(), new ProjectLogService())
    {
    }

    public SnapshotRestoreService(
        IProjectSnapshotService snapshotService,
        IProjectLogService logService)
    {
        this.snapshotService = snapshotService;
        this.logService = logService;
    }

    public async Task<IReadOnlyList<ProjectSnapshotManifest>> ListSnapshotsAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var snapshotsRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            ProjectSnapshotSchema.SnapshotRootRelativePath,
            "snapshots root");

        if (!Directory.Exists(snapshotsRoot))
        {
            return [];
        }

        var snapshots = new List<ProjectSnapshotManifest>();
        foreach (var directory in Directory.EnumerateDirectories(snapshotsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshotId = Path.GetFileName(directory);
            if (!IsSafeSnapshotId(snapshotId))
            {
                continue;
            }

            var manifestPath = Path.Combine(directory, ProjectSnapshotSchema.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                snapshots.Add(await LoadSnapshotAsync(root, snapshotId, cancellationToken));
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
            {
                // List operation stays non-destructive and only returns readable snapshots.
            }
        }

        return snapshots
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToList();
    }

    public async Task<ProjectSnapshotManifest> LoadSnapshotAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var normalizedSnapshotId = NormalizeSnapshotId(snapshotId);
        var manifestRelativePath = GetSnapshotManifestRelativePath(normalizedSnapshotId);
        var manifestPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            manifestRelativePath,
            "snapshot manifest");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Snapshot manifest was not found: {manifestRelativePath}", manifestPath);
        }

        ProjectSnapshotManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<ProjectSnapshotManifest>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Snapshot manifest JSON is invalid: {manifestRelativePath}. {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new InvalidDataException($"Snapshot manifest is empty or invalid: {manifestRelativePath}");
        }

        if (!string.Equals(
                manifest.SchemaVersion,
                ProjectSnapshotSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Snapshot schemaVersion '{manifest.SchemaVersion}' is not supported. Expected '{ProjectSnapshotSchema.CurrentSchemaVersion}'.");
        }

        if (!string.Equals(manifest.SnapshotId, normalizedSnapshotId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Snapshot manifest id '{manifest.SnapshotId}' does not match requested id '{normalizedSnapshotId}'.");
        }

        foreach (var file in manifest.Files)
        {
            _ = NormalizeSnapshotFileRelativePath(file.RelativePath, "snapshot file");
        }

        return manifest;
    }

    public async Task<SnapshotValidationResult> ValidateSnapshotAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var result = new SnapshotValidationResult
        {
            SnapshotId = snapshotId?.Trim() ?? string.Empty
        };

        string root;
        string normalizedSnapshotId;
        try
        {
            root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
            normalizedSnapshotId = NormalizeSnapshotId(snapshotId ?? string.Empty);
            result.SnapshotId = normalizedSnapshotId;
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error("snapshot.rootOrId", ex.Message));
            Finalize(result);
            return result;
        }

        ProjectSnapshotManifest manifest;
        try
        {
            manifest = await LoadSnapshotAsync(root, normalizedSnapshotId, cancellationToken);
            result.Items.Add(Ok(
                "snapshot.manifest",
                "snapshot-manifest.json exists and can be loaded.",
                GetSnapshotManifestRelativePath(normalizedSnapshotId)));
            result.Items.Add(Ok(
                "snapshot.schemaVersion",
                "schemaVersion is supported.",
                GetSnapshotManifestRelativePath(normalizedSnapshotId)));
        }
        catch (FileNotFoundException ex)
        {
            result.Items.Add(Error("snapshot.manifestMissing", ex.Message, GetSnapshotManifestRelativePath(normalizedSnapshotId)));
            Finalize(result);
            return result;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            result.Items.Add(Error("snapshot.manifestInvalid", ex.Message, GetSnapshotManifestRelativePath(normalizedSnapshotId)));
            Finalize(result);
            return result;
        }

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSnapshotFile(root, normalizedSnapshotId, file, result);
        }

        Finalize(result);
        return result;
    }

    public async Task<SnapshotRestorePlan> CreateRestorePlanAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var normalizedSnapshotId = NormalizeSnapshotId(snapshotId);
        var manifest = await LoadSnapshotAsync(root, normalizedSnapshotId, cancellationToken);
        var restoreId = CreateRestoreId();
        var plan = new SnapshotRestorePlan
        {
            SchemaVersion = SnapshotRestoreSchema.CurrentSchemaVersion,
            RestoreId = restoreId,
            ProjectId = manifest.ProjectId,
            SnapshotId = normalizedSnapshotId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreateSafetySnapshotBeforeRestore = true
        };

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceRelativePath = NormalizeSnapshotFileRelativePath(file.RelativePath, "snapshot file");
            var targetRelativePath = MapSnapshotFileToRestoreTarget(sourceRelativePath);
            var policy = EvaluateRestoreTarget(targetRelativePath);
            var sourceProjectRelativePath = CombineRelativePath(
                ProjectSnapshotSchema.SnapshotRootRelativePath,
                normalizedSnapshotId,
                sourceRelativePath);
            var sourcePath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                root,
                sourceProjectRelativePath,
                "snapshot source file");

            var item = new SnapshotRestoreFileItem
            {
                SourceRelativePath = sourceRelativePath,
                TargetRelativePath = targetRelativePath,
                Status = policy.Status,
                Message = policy.Message,
                ExpectedSha256 = file.Sha256,
                ActualSha256 = File.Exists(sourcePath)
                    ? ProjectPackagePathHelpers.ComputeSha256(sourcePath)
                    : null
            };

            if (!string.Equals(policy.Status, "planned", StringComparison.OrdinalIgnoreCase))
            {
                plan.Warnings.Add($"{policy.Status}: {targetRelativePath}: {policy.Message}");
            }

            plan.Files.Add(item);
        }

        await SaveRestorePlanAsync(root, plan, cancellationToken);
        return plan;
    }

    public async Task<SnapshotRestoreResult> RestoreAsync(
        string projectRoot,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var normalizedSnapshotId = NormalizeSnapshotId(snapshotId);
        var startedAt = DateTimeOffset.UtcNow;

        var validation = await ValidateSnapshotAsync(root, normalizedSnapshotId, cancellationToken);
        if (!validation.IsOk)
        {
            var failedValidationResult = new SnapshotRestoreResult
            {
                SchemaVersion = SnapshotRestoreSchema.CurrentSchemaVersion,
                RestoreId = CreateRestoreId(),
                IsOk = false,
                SnapshotId = normalizedSnapshotId,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Errors = validation.Items
                    .Where(item => string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase))
                    .Select(item => $"{item.Key}: {item.Message}")
                    .ToList(),
                Warnings = validation.Items
                    .Where(item => string.Equals(item.Severity, "warning", StringComparison.OrdinalIgnoreCase))
                    .Select(item => $"{item.Key}: {item.Message}")
                    .ToList()
            };

            await SaveRestoreResultAsync(root, failedValidationResult, cancellationToken);
            await WriteRestoreLogsAsync(root, failedValidationResult, "Snapshot restore blocked by validation errors.", cancellationToken);
            return failedValidationResult;
        }

        var plan = await CreateRestorePlanAsync(root, normalizedSnapshotId, cancellationToken);
        var result = new SnapshotRestoreResult
        {
            SchemaVersion = SnapshotRestoreSchema.CurrentSchemaVersion,
            RestoreId = plan.RestoreId,
            IsOk = false,
            SnapshotId = normalizedSnapshotId,
            StartedAt = startedAt,
            Warnings = [.. plan.Warnings]
        };

        if (plan.Files.Any(file => string.Equals(file.Status, "error", StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.AddRange(plan.Files
                .Where(file => string.Equals(file.Status, "error", StringComparison.OrdinalIgnoreCase))
                .Select(file => $"{file.TargetRelativePath}: {file.Message}"));
            result.CompletedAt = DateTimeOffset.UtcNow;
            await SaveRestoreResultAsync(root, result, cancellationToken);
            await WriteRestoreLogsAsync(root, result, "Snapshot restore blocked by restore-plan errors.", cancellationToken);
            return result;
        }

        ProjectSnapshotManifest safetySnapshot;
        try
        {
            safetySnapshot = await snapshotService.CreateSnapshotAsync(
                root,
                $"before-restore:{normalizedSnapshotId}",
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            result.Errors.Add($"Failed to create before-restore safety snapshot: {ex.Message}");
            result.CompletedAt = DateTimeOffset.UtcNow;
            await SaveRestoreResultAsync(root, result, cancellationToken);
            await WriteRestoreLogsAsync(root, result, "Snapshot restore blocked because safety snapshot failed.", cancellationToken);
            return result;
        }

        plan.SafetySnapshotId = safetySnapshot.SnapshotId;
        result.SafetySnapshotId = safetySnapshot.SnapshotId;
        await SaveRestorePlanAsync(root, plan, cancellationToken);

        foreach (var file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(file.Status, "skipped", StringComparison.OrdinalIgnoreCase))
            {
                result.Files.Add(file);
                result.Warnings.Add($"{file.TargetRelativePath}: {file.Message}");
                continue;
            }

            var restored = await RestoreOneFileAsync(root, normalizedSnapshotId, file, cancellationToken);
            result.Files.Add(restored);
            if (string.Equals(restored.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"{restored.TargetRelativePath}: {restored.Message}");
                break;
            }
        }

        result.IsOk = result.Errors.Count == 0;
        result.CompletedAt = DateTimeOffset.UtcNow;
        await SaveRestoreResultAsync(root, result, cancellationToken);
        await WriteRestoreLogsAsync(
            root,
            result,
            result.IsOk ? "Snapshot restore completed." : "Snapshot restore failed.",
            cancellationToken);
        return result;
    }

    public static string GetRestorePlanPath(string projectRoot, string restoreId)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            CombineRelativePath(
                SnapshotRestoreSchema.RestoreReportsRootRelativePath,
                NormalizeRestoreId(restoreId),
                SnapshotRestoreSchema.RestorePlanFileName),
            "restore plan");
    }

    public static string GetRestoreResultPath(string projectRoot, string restoreId)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            CombineRelativePath(
                SnapshotRestoreSchema.RestoreReportsRootRelativePath,
                NormalizeRestoreId(restoreId),
                SnapshotRestoreSchema.RestoreResultFileName),
            "restore result");
    }

    private static void ValidateSnapshotFile(
        string projectRoot,
        string snapshotId,
        SnapshotFileItem file,
        SnapshotValidationResult result)
    {
        string sourceRelativePath;
        try
        {
            sourceRelativePath = NormalizeSnapshotFileRelativePath(file.RelativePath, "snapshot file");
            result.Items.Add(Ok(
                $"snapshot.file.path.{sourceRelativePath}",
                "Snapshot file path is relative.",
                sourceRelativePath));
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error("snapshot.file.path", ex.Message, file.RelativePath));
            return;
        }

        var sourceProjectRelativePath = CombineRelativePath(
            ProjectSnapshotSchema.SnapshotRootRelativePath,
            snapshotId,
            sourceRelativePath);
        string sourcePath;
        try
        {
            sourcePath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                projectRoot,
                sourceProjectRelativePath,
                "snapshot source file");
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error("snapshot.file.sourcePath", ex.Message, sourceRelativePath));
            return;
        }

        if (!File.Exists(sourcePath))
        {
            result.Items.Add(Error("snapshot.file.missing", "Snapshot source file is missing.", sourceRelativePath));
        }
        else if (string.IsNullOrWhiteSpace(file.Sha256))
        {
            result.Items.Add(Error("snapshot.file.hashMissing", "Snapshot manifest does not record a SHA-256 hash.", sourceRelativePath));
        }
        else
        {
            var actualSha256 = ProjectPackagePathHelpers.ComputeSha256(sourcePath);
            result.Items.Add(string.Equals(file.Sha256, actualSha256, StringComparison.OrdinalIgnoreCase)
                ? Ok("snapshot.file.hash", "Snapshot file hash matches manifest.", sourceRelativePath)
                : Error("snapshot.file.hashMismatch", "Snapshot file hash does not match manifest.", sourceRelativePath));
        }

        var targetRelativePath = MapSnapshotFileToRestoreTarget(sourceRelativePath);
        try
        {
            _ = ProjectPackagePathHelpers.NormalizeProjectRelativePath(
                projectRoot,
                targetRelativePath,
                "restore target");
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error("snapshot.restoreTarget.path", ex.Message, targetRelativePath));
            return;
        }

        var policy = EvaluateRestoreTarget(targetRelativePath);
        result.Items.Add(policy.Severity switch
        {
            "error" => Error("snapshot.restoreTarget.blocked", policy.Message, targetRelativePath),
            "warning" => Warning("snapshot.restoreTarget.skipped", policy.Message, targetRelativePath),
            _ => Ok("snapshot.restoreTarget.allowed", policy.Message, targetRelativePath)
        });
    }

    private static async Task<SnapshotRestoreFileItem> RestoreOneFileAsync(
        string projectRoot,
        string snapshotId,
        SnapshotRestoreFileItem file,
        CancellationToken cancellationToken)
    {
        var restored = new SnapshotRestoreFileItem
        {
            SourceRelativePath = file.SourceRelativePath,
            TargetRelativePath = file.TargetRelativePath,
            Status = "failed",
            Message = string.Empty,
            ExpectedSha256 = file.ExpectedSha256
        };

        try
        {
            var sourceRelativePath = NormalizeSnapshotFileRelativePath(file.SourceRelativePath, "restore source");
            var sourceProjectRelativePath = CombineRelativePath(
                ProjectSnapshotSchema.SnapshotRootRelativePath,
                snapshotId,
                sourceRelativePath);
            var sourcePath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                projectRoot,
                sourceProjectRelativePath,
                "restore source file");

            if (!File.Exists(sourcePath))
            {
                restored.Message = "Restore source file is missing.";
                return restored;
            }

            var actualSha256 = ProjectPackagePathHelpers.ComputeSha256(sourcePath);
            restored.ActualSha256 = actualSha256;
            if (string.IsNullOrWhiteSpace(file.ExpectedSha256)
                || !string.Equals(file.ExpectedSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
            {
                restored.Message = "Restore source hash does not match the snapshot manifest.";
                return restored;
            }

            var targetRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(
                projectRoot,
                file.TargetRelativePath,
                "restore target");
            var policy = EvaluateRestoreTarget(targetRelativePath);
            if (!string.Equals(policy.Status, "planned", StringComparison.OrdinalIgnoreCase))
            {
                restored.Status = policy.Status;
                restored.Message = policy.Message;
                return restored;
            }

            var targetPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                projectRoot,
                targetRelativePath,
                "restore target file");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using (var source = File.OpenRead(sourcePath))
            await using (var target = File.Create(targetPath))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            restored.ActualSha256 = ProjectPackagePathHelpers.ComputeSha256(targetPath);
            restored.Status = "restored";
            restored.Message = "File restored.";
            return restored;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            restored.Message = ex.Message;
            return restored;
        }
    }

    private async Task SaveRestorePlanAsync(
        string projectRoot,
        SnapshotRestorePlan plan,
        CancellationToken cancellationToken)
    {
        var path = GetRestorePlanPath(projectRoot, plan.RestoreId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, plan, WrbJsonOptions.Default, cancellationToken);
    }

    private async Task SaveRestoreResultAsync(
        string projectRoot,
        SnapshotRestoreResult result,
        CancellationToken cancellationToken)
    {
        var path = GetRestoreResultPath(projectRoot, result.RestoreId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, result, WrbJsonOptions.Default, cancellationToken);
    }

    private async Task WriteRestoreLogsAsync(
        string projectRoot,
        SnapshotRestoreResult result,
        string summary,
        CancellationToken cancellationToken)
    {
        var level = result.IsOk ? ProjectLogLevel.Info : ProjectLogLevel.Error;
        await logService.WriteAsync(
            projectRoot,
            "project",
            $"{summary} snapshot={result.SnapshotId}; restore={result.RestoreId}; safetySnapshot={result.SafetySnapshotId ?? "none"}",
            level,
            cancellationToken);

        await logService.WriteAsync(
            projectRoot,
            "security",
            $"Snapshot restore boundary check completed. isOk={result.IsOk}; errors={result.Errors.Count}; warnings={result.Warnings.Count}",
            result.IsOk ? ProjectLogLevel.Info : ProjectLogLevel.Warning,
            cancellationToken);
    }

    private static RestoreTargetPolicy EvaluateRestoreTarget(string targetRelativePath)
    {
        var normalized = NormalizeSlash(targetRelativePath);
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var forbiddenSegment = parts.FirstOrDefault(part =>
            ForbiddenDirectorySegments.Contains(part, StringComparer.OrdinalIgnoreCase));
        if (forbiddenSegment is not null)
        {
            return new RestoreTargetPolicy(
                "error",
                "error",
                $"Restore target contains a forbidden directory segment: {forbiddenSegment}");
        }

        var extension = Path.GetExtension(normalized);
        if (SkippedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return new RestoreTargetPolicy(
                "skipped",
                "warning",
                $"Restore target extension is not restored by P1.4 policy: {extension}");
        }

        if (AllowedExactRestoreTargets.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            || AllowedRestoreTargetPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return new RestoreTargetPolicy("planned", "ok", "Restore target is allowed.");
        }

        return new RestoreTargetPolicy(
            "skipped",
            "warning",
            "Restore target is outside the P1.4 allowed restore surface.");
    }

    private static string MapSnapshotFileToRestoreTarget(string snapshotFileRelativePath)
    {
        var normalized = NormalizeSlash(snapshotFileRelativePath);
        const string snapshotOutputPrefix = "output-site/";
        var outputCurrentPrefix = ProjectDirectoryV2.OutputCurrent + "/";
        if (normalized.StartsWith(snapshotOutputPrefix, StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith(outputCurrentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return CombineRelativePath(
                ProjectDirectoryV2.OutputCurrent,
                normalized[snapshotOutputPrefix.Length..]);
        }

        return normalized;
    }

    private static string NormalizeSnapshotFileRelativePath(string relativePath, string fieldName)
    {
        return NormalizeSlash(ProjectPackagePathHelpers.NormalizeRelativeToken(relativePath, fieldName));
    }

    private static string NormalizeSnapshotId(string snapshotId)
    {
        var normalized = ProjectPackagePathHelpers.NormalizeRelativeToken(snapshotId, "snapshot id");
        if (normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Snapshot id must be a single path segment: {snapshotId}");
        }

        return normalized;
    }

    private static string NormalizeRestoreId(string restoreId)
    {
        var normalized = ProjectPackagePathHelpers.NormalizeRelativeToken(restoreId, "restore id");
        if (normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Restore id must be a single path segment: {restoreId}");
        }

        return normalized;
    }

    private static bool IsSafeSnapshotId(string snapshotId)
    {
        try
        {
            _ = NormalizeSnapshotId(snapshotId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string CreateRestoreId()
    {
        return $"restore-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..41];
    }

    private static string GetSnapshotManifestRelativePath(string snapshotId)
    {
        return CombineRelativePath(
            ProjectSnapshotSchema.SnapshotRootRelativePath,
            snapshotId,
            ProjectSnapshotSchema.ManifestFileName);
    }

    private static string CombineRelativePath(params string[] parts)
    {
        return string.Join(
            '/',
            parts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => NormalizeSlash(part).Trim('/')));
    }

    private static string NormalizeSlash(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Replace('\\', '/');
    }

    private static SnapshotValidationItem Ok(string key, string message, string? relativePath = null)
    {
        return new SnapshotValidationItem
        {
            Key = key,
            Severity = "ok",
            Message = message,
            RelativePath = relativePath
        };
    }

    private static SnapshotValidationItem Warning(string key, string message, string? relativePath = null)
    {
        return new SnapshotValidationItem
        {
            Key = key,
            Severity = "warning",
            Message = message,
            RelativePath = relativePath
        };
    }

    private static SnapshotValidationItem Error(string key, string message, string? relativePath = null)
    {
        return new SnapshotValidationItem
        {
            Key = key,
            Severity = "error",
            Message = message,
            RelativePath = relativePath
        };
    }

    private static void Finalize(SnapshotValidationResult result)
    {
        result.IsOk = result.Items.All(item =>
            !string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record RestoreTargetPolicy(
        string Status,
        string Severity,
        string Message);
}
