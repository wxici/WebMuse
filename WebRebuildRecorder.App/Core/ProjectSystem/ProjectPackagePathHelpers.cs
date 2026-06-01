using System.Security.Cryptography;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Security;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

internal static class ProjectPackagePathHelpers
{
    private static readonly Regex SecretOrLocalPathPattern = new(
        @"(?i)(sk-[A-Za-z0-9_\-]{3,}|OPENAI_API_KEY|api[_-]?key|\bsecret\b|\btoken\b|\bpassword\b|[A-Za-z]:\\+|/home/|\.ssh|\.codex|\.openai)");

    public static string ValidateProjectRoot(string projectRoot)
    {
        var validation = SandboxPathPolicy.ValidateProjectRoot(projectRoot);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
    }

    public static string ResolveRelativeFilePath(
        string projectRoot,
        string relativePath,
        string fieldName)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
    }

    public static string NormalizeProjectRelativePath(
        string projectRoot,
        string relativePath,
        string fieldName)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return Path.GetRelativePath(validation.NormalizedProjectRoot, validation.NormalizedTargetPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public static string NormalizeRelativeToken(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} cannot be empty.");
        }

        var normalized = value.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException($"{fieldName} must be relative, not absolute: {value}");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"{fieldName} cannot contain '..': {value}");
        }

        return string.Join('/', segments);
    }

    public static bool ContainsSecretOrLocalPath(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && SecretOrLocalPathPattern.IsMatch(value);
    }

    public static string ComputeSha256(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<(string ProjectId, string ReferenceUrl)> TryReadProjectIdentityAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await new ProjectManifestService().LoadAsync(projectRoot, cancellationToken);
            return (manifest.ProjectId, manifest.ReferenceUrl);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or IOException or InvalidOperationException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
