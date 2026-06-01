using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Core.State;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IProjectManifestService
{
    Task<WrbProjectManifest> CreateNewAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default);

    Task<WrbProjectManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        WrbProjectManifest manifest,
        CancellationToken cancellationToken = default);

    Task SetProjectStateAsync(
        string projectRoot,
        ProjectState state,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectManifestService : IProjectManifestService
{
    public Task<WrbProjectManifest> CreateNewAsync(
        string projectRoot,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRoot = ValidateProjectRoot(projectRoot);
        var now = DateTimeOffset.UtcNow;
        var trimmedName = string.IsNullOrWhiteSpace(projectName)
            ? Path.GetFileName(normalizedRoot)
            : projectName.Trim();

        var manifest = new WrbProjectManifest
        {
            SchemaVersion = WrbProjectSchema.CurrentSchemaVersion,
            AppVersion = WrbProjectSchema.CurrentAppVersion,
            ProjectId = CreateProjectId(trimmedName, now),
            ProjectName = trimmedName,
            ProjectRoot = ".",
            State = ProjectState.ProjectCreated,
            CreatedAt = now,
            UpdatedAt = now,
            Paths = new WrbProjectPaths()
        };

        return Task.FromResult(manifest);
    }

    public async Task<WrbProjectManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoot = ValidateProjectRoot(projectRoot);
        var manifestPath = GetManifestPath(normalizedRoot);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Project manifest was not found: {manifestPath}", manifestPath);
        }

        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<WrbProjectManifest>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (manifest is null)
            {
                throw new InvalidDataException($"Project manifest is empty or invalid: {manifestPath}");
            }

            ValidateSchema(manifest, manifestPath);
            NormalizeLoadedManifest(manifest);
            ValidateManifestPaths(normalizedRoot, manifest, manifestPath);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Project manifest JSON is invalid: {manifestPath}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to read project manifest: {manifestPath}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        WrbProjectManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var normalizedRoot = ValidateProjectRoot(projectRoot);
        var manifestPath = GetManifestPath(normalizedRoot);

        try
        {
            Directory.CreateDirectory(normalizedRoot);
            NormalizeManifestForSave(normalizedRoot, manifest);
            await using var stream = File.Create(manifestPath);
            await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to serialize project manifest: {manifestPath}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save project manifest: {manifestPath}. {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"No permission to save project manifest: {manifestPath}. {ex.Message}", ex);
        }
    }

    public async Task SetProjectStateAsync(
        string projectRoot,
        ProjectState state,
        CancellationToken cancellationToken = default)
    {
        var manifest = await LoadAsync(projectRoot, cancellationToken);
        manifest.State = state;
        await SaveAsync(projectRoot, manifest, cancellationToken);
    }

    public static string GetManifestPath(string projectRoot)
    {
        return Path.Combine(
            Path.GetFullPath(Environment.ExpandEnvironmentVariables(projectRoot.Trim())),
            WrbProjectSchema.FileName);
    }

    private static string ValidateProjectRoot(string projectRoot)
    {
        var validation = SandboxPathPolicy.ValidateProjectRoot(projectRoot);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
    }

    private static void ValidateSchema(WrbProjectManifest manifest, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            throw new InvalidDataException($"Project manifest has no schemaVersion: {manifestPath}");
        }

        if (!string.Equals(
                manifest.SchemaVersion,
                WrbProjectSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Project manifest schemaVersion '{manifest.SchemaVersion}' is not supported. Expected '{WrbProjectSchema.CurrentSchemaVersion}'. File: {manifestPath}");
        }
    }

    private static void NormalizeLoadedManifest(WrbProjectManifest manifest)
    {
        manifest.SchemaVersion = string.IsNullOrWhiteSpace(manifest.SchemaVersion)
            ? WrbProjectSchema.CurrentSchemaVersion
            : manifest.SchemaVersion.Trim();
        manifest.AppVersion = string.IsNullOrWhiteSpace(manifest.AppVersion)
            ? WrbProjectSchema.CurrentAppVersion
            : manifest.AppVersion.Trim();
        manifest.ProjectId = manifest.ProjectId?.Trim() ?? string.Empty;
        manifest.ProjectName = manifest.ProjectName?.Trim() ?? string.Empty;
        manifest.ProjectRoot = string.IsNullOrWhiteSpace(manifest.ProjectRoot) ? "." : manifest.ProjectRoot.Trim();
        manifest.ReferenceUrl = manifest.ReferenceUrl?.Trim() ?? string.Empty;
        manifest.CurrentOutputVersion = manifest.CurrentOutputVersion?.Trim() ?? string.Empty;
        manifest.Paths ??= new WrbProjectPaths();
        manifest.Features ??= new WrbProjectFeatures();
        manifest.LastExportPath ??= string.Empty;
    }

    private static void ValidateManifestPaths(
        string normalizedRoot,
        WrbProjectManifest manifest,
        string manifestPath)
    {
        ValidateManifestPath(normalizedRoot, manifest.Paths.Input, nameof(manifest.Paths.Input), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Assets, nameof(manifest.Paths.Assets), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Theme, nameof(manifest.Paths.Theme), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Observation, nameof(manifest.Paths.Observation), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.CodexTask, nameof(manifest.Paths.CodexTask), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.OutputCurrent, nameof(manifest.Paths.OutputCurrent), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.OutputVersions, nameof(manifest.Paths.OutputVersions), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Tune, nameof(manifest.Paths.Tune), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Maps, nameof(manifest.Paths.Maps), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Exports, nameof(manifest.Paths.Exports), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Logs, nameof(manifest.Paths.Logs), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Versions, nameof(manifest.Paths.Versions), manifestPath);
        ValidateManifestPath(normalizedRoot, manifest.Paths.Runtime, nameof(manifest.Paths.Runtime), manifestPath);
    }

    private static void ValidateManifestPath(
        string normalizedRoot,
        string value,
        string fieldName,
        string manifestPath)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(normalizedRoot, value, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidDataException(
                $"Project manifest path '{fieldName}' is invalid in {manifestPath}: {validation.Message}");
        }
    }

    private static void NormalizeManifestForSave(string normalizedRoot, WrbProjectManifest manifest)
    {
        var now = DateTimeOffset.UtcNow;
        manifest.SchemaVersion = WrbProjectSchema.CurrentSchemaVersion;
        manifest.AppVersion = string.IsNullOrWhiteSpace(manifest.AppVersion)
            ? WrbProjectSchema.CurrentAppVersion
            : manifest.AppVersion.Trim();
        manifest.ProjectId = string.IsNullOrWhiteSpace(manifest.ProjectId)
            ? CreateProjectId(manifest.ProjectName, now)
            : manifest.ProjectId.Trim();
        manifest.ProjectName = string.IsNullOrWhiteSpace(manifest.ProjectName)
            ? Path.GetFileName(normalizedRoot)
            : manifest.ProjectName.Trim();
        manifest.ProjectRoot = ".";
        manifest.ReferenceUrl = manifest.ReferenceUrl?.Trim() ?? string.Empty;
        manifest.CurrentOutputVersion = manifest.CurrentOutputVersion?.Trim() ?? string.Empty;
        manifest.CreatedAt = manifest.CreatedAt == default ? now : manifest.CreatedAt;
        manifest.UpdatedAt = now;
        manifest.Paths ??= new WrbProjectPaths();
        manifest.Features ??= new WrbProjectFeatures();
        manifest.LastExportPath ??= string.Empty;

        manifest.Paths.Input = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Input, ProjectDirectoryV2.Input);
        manifest.Paths.Assets = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Assets, ProjectDirectoryV2.Assets);
        manifest.Paths.Theme = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Theme, ProjectDirectoryV2.Theme);
        manifest.Paths.Observation = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Observation, ProjectDirectoryV2.Observation);
        manifest.Paths.CodexTask = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.CodexTask, ProjectDirectoryV2.CodexTask);
        manifest.Paths.OutputCurrent = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.OutputCurrent, ProjectDirectoryV2.OutputCurrent);
        manifest.Paths.OutputVersions = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.OutputVersions, ProjectDirectoryV2.OutputVersions);
        manifest.Paths.Tune = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Tune, ProjectDirectoryV2.Tune);
        manifest.Paths.Maps = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Maps, ProjectDirectoryV2.Maps);
        manifest.Paths.Exports = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Exports, ProjectDirectoryV2.Exports);
        manifest.Paths.Logs = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Logs, ProjectDirectoryV2.Logs);
        manifest.Paths.Versions = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Versions, ProjectDirectoryV2.Versions);
        manifest.Paths.Runtime = NormalizeRelativeProjectPath(normalizedRoot, manifest.Paths.Runtime, ProjectDirectoryV2.Runtime);
    }

    private static string NormalizeRelativeProjectPath(string projectRoot, string value, string fallback)
    {
        var path = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, path, "manifest path");
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return Path.GetRelativePath(projectRoot, validation.NormalizedTargetPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string CreateProjectId(string projectName, DateTimeOffset timestamp)
    {
        var source = string.IsNullOrWhiteSpace(projectName) ? "project" : projectName.Trim().ToLowerInvariant();
        var slug = Regex.Replace(source, @"[^a-z0-9\u4e00-\u9fa5]+", "-");
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        return $"{timestamp:yyyy-MM-dd}_{(string.IsNullOrWhiteSpace(slug) ? "project" : slug)}";
    }
}
