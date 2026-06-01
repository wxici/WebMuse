using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IAssetsManifestService
{
    Task<AssetsManifest> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default);

    Task<AssetsManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        AssetsManifest manifest,
        CancellationToken cancellationToken = default);

    Task<AssetsManifest> AddOrUpdateItemAsync(
        string projectRoot,
        AssetManifestItem item,
        CancellationToken cancellationToken = default);
}

public sealed class AssetsManifestService : IAssetsManifestService
{
    public Task<AssetsManifest> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateProjectRoot(projectRoot);
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new AssetsManifest
        {
            SchemaVersion = AssetsManifestSchema.CurrentSchemaVersion,
            ProjectId = projectId.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            Assets = []
        });
    }

    public async Task<AssetsManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetManifestPath(projectRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Assets manifest was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<AssetsManifest>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (manifest is null)
            {
                throw new InvalidDataException($"Assets manifest is empty or invalid: {path}");
            }

            ValidateSchema(manifest, path);
            NormalizeAndValidate(projectRoot, manifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Assets manifest JSON is invalid: {path}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to read assets manifest: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        AssetsManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var path = GetManifestPath(projectRoot);

        try
        {
            NormalizeAndValidate(projectRoot, manifest);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to serialize assets manifest: {path}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save assets manifest: {path}. {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"No permission to save assets manifest: {path}. {ex.Message}", ex);
        }
    }

    public async Task<AssetsManifest> AddOrUpdateItemAsync(
        string projectRoot,
        AssetManifestItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var manifest = File.Exists(GetManifestPath(projectRoot))
            ? await LoadAsync(projectRoot, cancellationToken)
            : await CreateNewAsync(projectRoot, await TryLoadProjectIdAsync(projectRoot, cancellationToken), cancellationToken);

        NormalizeItem(projectRoot, item);
        var existingIndex = manifest.Assets.FindIndex(asset =>
            string.Equals(asset.AssetId, item.AssetId, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            manifest.Assets[existingIndex] = item;
        }
        else
        {
            manifest.Assets.Add(item);
        }

        await SaveAsync(projectRoot, manifest, cancellationToken);
        return manifest;
    }

    public static string GetManifestPath(string projectRoot)
    {
        return ValidateRelativeFilePath(projectRoot, AssetsManifestSchema.RelativePath, "assets manifest");
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

    private static void ValidateSchema(AssetsManifest manifest, string path)
    {
        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            throw new InvalidDataException($"Assets manifest has no schemaVersion: {path}");
        }

        if (!string.Equals(
                manifest.SchemaVersion,
                AssetsManifestSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Assets manifest schemaVersion '{manifest.SchemaVersion}' is not supported. Expected '{AssetsManifestSchema.CurrentSchemaVersion}'. File: {path}");
        }
    }

    private static void NormalizeAndValidate(string projectRoot, AssetsManifest manifest)
    {
        var now = DateTimeOffset.UtcNow;
        manifest.SchemaVersion = AssetsManifestSchema.CurrentSchemaVersion;
        manifest.ProjectId = manifest.ProjectId?.Trim() ?? string.Empty;
        manifest.CreatedAt = manifest.CreatedAt == default ? now : manifest.CreatedAt;
        manifest.UpdatedAt = now;
        manifest.Assets ??= [];

        foreach (var item in manifest.Assets)
        {
            NormalizeItem(projectRoot, item);
        }
    }

    private static void NormalizeItem(string projectRoot, AssetManifestItem item)
    {
        item.AssetId = string.IsNullOrWhiteSpace(item.AssetId)
            ? $"asset-{Guid.NewGuid():N}"
            : item.AssetId.Trim();
        item.Kind = NormalizeStableText(item.Kind, "unknown");
        item.Role = NormalizeStableText(item.Role, "unknown");
        item.SourceType = NormalizeStableText(item.SourceType, "unknown");
        item.OriginalFileName = string.IsNullOrWhiteSpace(item.OriginalFileName)
            ? string.Empty
            : Path.GetFileName(item.OriginalFileName.Trim());
        item.SourceNote = NormalizeSourceNote(item.SourceNote);
        item.MimeType = string.IsNullOrWhiteSpace(item.MimeType) ? null : item.MimeType.Trim();
        item.Tags = item.Tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        item.RelativePath = NormalizeRelativePath(projectRoot, item.RelativePath, "asset.relativePath");
        var fullPath = Path.Combine(ValidateProjectRoot(projectRoot), item.RelativePath);
        if (File.Exists(fullPath))
        {
            var fileInfo = new FileInfo(fullPath);
            item.SizeBytes ??= fileInfo.Length;
            item.Sha256 ??= ComputeSha256(fullPath);
        }
    }

    private static string NormalizeStableText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeSourceNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (ContainsSensitiveLocalPath(trimmed))
        {
            throw new InvalidOperationException("Asset sourceNote contains a sensitive local path or credential directory.");
        }

        return trimmed;
    }

    private static string NormalizeRelativePath(string projectRoot, string value, string fieldName)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, value, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return Path.GetRelativePath(validation.NormalizedProjectRoot, validation.NormalizedTargetPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string ValidateRelativeFilePath(string projectRoot, string relativePath, string fieldName)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
    }

    private static bool ContainsSensitiveLocalPath(string value)
    {
        return Regex.IsMatch(value, @"(?i)([a-z]:\\users\\|[a-z]:\\|/home/|\.ssh|\.codex|\.openai)");
    }

    private static string ComputeSha256(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> TryLoadProjectIdAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            var projectManifest = await new ProjectManifestService().LoadAsync(projectRoot, cancellationToken);
            return projectManifest.ProjectId;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or IOException or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
