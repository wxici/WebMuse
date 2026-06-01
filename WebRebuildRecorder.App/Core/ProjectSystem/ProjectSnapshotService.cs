using System.Security.Cryptography;
using System.Text.Json;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IProjectSnapshotService
{
    Task<ProjectSnapshotManifest> CreateSnapshotAsync(
        string projectRoot,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectSnapshotService : IProjectSnapshotService
{
    private static readonly string[] BlockedDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".ssh",
        ".codex",
        ".openai",
        "node_modules"
    ];

    private static readonly string[] BlockedExtensions =
    [
        ".zip",
        ".mp4",
        ".webm",
        ".mov",
        ".avi",
        ".mkv",
        ".log"
    ];

    private const long MaxSnapshotFileBytes = 50L * 1024L * 1024L;

    public async Task<ProjectSnapshotManifest> CreateSnapshotAsync(
        string projectRoot,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateProjectRoot(projectRoot);
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "manual scaffold snapshot" : reason.Trim();
        var prefix = string.Equals(normalizedReason, "readiness-probe", StringComparison.OrdinalIgnoreCase)
            ? "readiness-probe"
            : "snapshot";
        var snapshotId = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..42];
        var snapshotRelativePath = $"{ProjectSnapshotSchema.SnapshotRootRelativePath}/{snapshotId}";
        var snapshotRoot = ValidateRelativePath(root, snapshotRelativePath, "snapshot root");
        Directory.CreateDirectory(snapshotRoot);

        var projectId = await TryLoadProjectIdAsync(root, cancellationToken);
        var manifest = new ProjectSnapshotManifest
        {
            SchemaVersion = ProjectSnapshotSchema.CurrentSchemaVersion,
            SnapshotId = snapshotId,
            ProjectId = projectId,
            CreatedAt = DateTimeOffset.UtcNow,
            Reason = normalizedReason
        };

        await CopyDirectoryIfExistsAsync(root, snapshotRoot, ProjectDirectoryV2.OutputCurrent, "output-site", manifest, cancellationToken);
        await CopyFileIfExistsAsync(root, snapshotRoot, ThemeManifestSchema.RelativePath, ThemeManifestSchema.RelativePath, manifest, cancellationToken);
        await CopyFileIfExistsAsync(root, snapshotRoot, ContentMapSchema.RelativePath, ContentMapSchema.RelativePath, manifest, cancellationToken);
        await CopyFileIfExistsAsync(root, snapshotRoot, AssetsManifestSchema.RelativePath, AssetsManifestSchema.RelativePath, manifest, cancellationToken);
        await CopyFileIfExistsAsync(root, snapshotRoot, WrbProjectSchema.FileName, WrbProjectSchema.FileName, manifest, cancellationToken);

        var manifestPath = Path.Combine(snapshotRoot, ProjectSnapshotSchema.ManifestFileName);
        await using var stream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default, cancellationToken);
        return manifest;
    }

    private static async Task CopyDirectoryIfExistsAsync(
        string projectRoot,
        string snapshotRoot,
        string sourceRelativePath,
        string destinationRelativePath,
        ProjectSnapshotManifest manifest,
        CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(projectRoot, sourceRelativePath);
        if (!Directory.Exists(sourcePath))
        {
            manifest.Warnings.Add($"Missing optional snapshot directory: {sourceRelativePath}");
            return;
        }

        foreach (var sourceFile in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldSkip(sourceFile))
            {
                continue;
            }

            var relativeToSource = Path.GetRelativePath(sourcePath, sourceFile);
            var destinationRelativeFile = Path.Combine(destinationRelativePath, relativeToSource)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            await CopyOneFileAsync(projectRoot, snapshotRoot, sourceFile, destinationRelativeFile, manifest, cancellationToken);
        }
    }

    private static async Task CopyFileIfExistsAsync(
        string projectRoot,
        string snapshotRoot,
        string sourceRelativePath,
        string destinationRelativePath,
        ProjectSnapshotManifest manifest,
        CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(projectRoot, sourceRelativePath);
        if (!File.Exists(sourcePath))
        {
            manifest.Warnings.Add($"Missing optional snapshot file: {sourceRelativePath}");
            return;
        }

        if (ShouldSkip(sourcePath))
        {
            manifest.Warnings.Add($"Skipped blocked snapshot file: {sourceRelativePath}");
            return;
        }

        await CopyOneFileAsync(projectRoot, snapshotRoot, sourcePath, destinationRelativePath, manifest, cancellationToken);
    }

    private static async Task CopyOneFileAsync(
        string projectRoot,
        string snapshotRoot,
        string sourcePath,
        string destinationRelativePath,
        ProjectSnapshotManifest manifest,
        CancellationToken cancellationToken)
    {
        var sourceValidation = SandboxPathPolicy.ValidateProjectPath(projectRoot, sourcePath);
        if (!sourceValidation.IsAllowed)
        {
            manifest.Warnings.Add($"Skipped unsafe snapshot source: {sourcePath}");
            return;
        }

        var sourceInfo = new FileInfo(sourceValidation.NormalizedTargetPath);
        if (sourceInfo.Length > MaxSnapshotFileBytes)
        {
            manifest.Warnings.Add($"Skipped large snapshot file: {Path.GetRelativePath(projectRoot, sourceInfo.FullName)}");
            return;
        }

        var destinationPath = Path.Combine(snapshotRoot, destinationRelativePath);
        var destinationValidation = SandboxPathPolicy.ValidateProjectPath(projectRoot, destinationPath);
        if (!destinationValidation.IsAllowed)
        {
            manifest.Warnings.Add($"Skipped unsafe snapshot destination: {destinationRelativePath}");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationValidation.NormalizedTargetPath)!);
        await using (var source = File.OpenRead(sourceValidation.NormalizedTargetPath))
        await using (var destination = File.Create(destinationValidation.NormalizedTargetPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        manifest.Files.Add(new SnapshotFileItem
        {
            RelativePath = Path.GetRelativePath(snapshotRoot, destinationValidation.NormalizedTargetPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/'),
            Sha256 = ComputeSha256(destinationValidation.NormalizedTargetPath),
            SizeBytes = new FileInfo(destinationValidation.NormalizedTargetPath).Length
        });
    }

    private static bool ShouldSkip(string path)
    {
        var parts = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => BlockedDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        return BlockedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
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

    private static string ValidateRelativePath(string projectRoot, string relativePath, string fieldName)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
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
