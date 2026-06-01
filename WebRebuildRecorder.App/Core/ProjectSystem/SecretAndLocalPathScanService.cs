using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Security;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class SecretAndLocalPathScanService
{
    private static readonly SecretPattern[] Patterns =
    [
        new("openaiSecretKey", new Regex(@"(?<![A-Za-z0-9_])sk-[A-Za-z0-9_\-]{3,}", RegexOptions.IgnoreCase | RegexOptions.Compiled), "error", "Possible OpenAI-style secret key."),
        new("openAiApiKeyName", new Regex(@"OPENAI_API_KEY", RegexOptions.IgnoreCase | RegexOptions.Compiled), "error", "OpenAI API key variable name."),
        new("apiKey", new Regex(@"api[_-]?key", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Generic API key marker."),
        new("secret", new Regex(@"\bsecret\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Generic secret marker."),
        new("token", new Regex(@"\btoken\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Generic token marker."),
        new("password", new Regex(@"\bpassword\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Generic password marker."),
        new("windowsUsersPath", new Regex(@"[A-Za-z]:\\+Users\\+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Windows user profile path."),
        new("windowsDrivePath", new Regex(@"[A-Za-z]:\\+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Windows absolute local path."),
        new("unixHomePath", new Regex(@"/home/", RegexOptions.IgnoreCase | RegexOptions.Compiled), "warning", "Unix home path."),
        new("sshDirectory", new Regex(@"\.ssh", RegexOptions.IgnoreCase | RegexOptions.Compiled), "error", "SSH credential directory marker."),
        new("codexDirectory", new Regex(@"\.codex", RegexOptions.IgnoreCase | RegexOptions.Compiled), "error", "Codex local state directory marker."),
        new("openAiDirectory", new Regex(@"\.openai", RegexOptions.IgnoreCase | RegexOptions.Compiled), "error", "OpenAI local state directory marker.")
    ];

    private static readonly string[] ManifestRelativePaths =
    [
        WrbProjectSchema.FileName,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath,
        AssetsManifestSchema.RelativePath,
        ObservationPackageSchema.RelativePath,
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        ConstructionPackageContextSchema.PackageIndexRelativePath
    ];

    private static readonly string[] OutputExtensions =
    [
        ".html",
        ".css",
        ".js"
    ];

    private const long MaxScannedFileBytes = 2L * 1024L * 1024L;

    public async Task<SecretScanResult> ScanProjectAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateProjectRoot(projectRoot);
        var result = new SecretScanResult();
        foreach (var relativePath in EnumerateScanRelativePaths(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanFileAsync(root, relativePath, result, cancellationToken);
        }

        result.IsOk = result.Findings.All(finding =>
            !string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static IEnumerable<string> EnumerateScanRelativePaths(string projectRoot)
    {
        foreach (var relativePath in ManifestRelativePaths)
        {
            if (File.Exists(Path.Combine(projectRoot, relativePath)))
            {
                yield return relativePath;
            }
        }

        var outputRoot = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent);
        if (!Directory.Exists(outputRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkip(file))
            {
                continue;
            }

            if (!OutputExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return Path.GetRelativePath(projectRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }

    private static async Task ScanFileAsync(
        string projectRoot,
        string relativePath,
        SecretScanResult result,
        CancellationToken cancellationToken)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, "secret scan file");
        if (!validation.IsAllowed || !File.Exists(validation.NormalizedTargetPath))
        {
            return;
        }

        var fileInfo = new FileInfo(validation.NormalizedTargetPath);
        if (fileInfo.Length > MaxScannedFileBytes)
        {
            result.Findings.Add(new SecretScanFinding
            {
                RelativePath = relativePath,
                Key = "largeFileSkipped",
                Severity = "warning",
                Message = "File skipped by secret scanner because it exceeds the scaffold size limit."
            });
            return;
        }

        var content = await File.ReadAllTextAsync(validation.NormalizedTargetPath, cancellationToken);
        foreach (var pattern in Patterns)
        {
            if (pattern.Regex.IsMatch(content))
            {
                result.Findings.Add(new SecretScanFinding
                {
                    RelativePath = relativePath,
                    Key = pattern.Key,
                    Severity = pattern.Severity,
                    Message = pattern.Message
                });
            }
        }
    }

    private static bool ShouldSkip(string path)
    {
        var parts = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part =>
            string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".ssh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".codex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, ".openai", StringComparison.OrdinalIgnoreCase));
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

    private sealed record SecretPattern(string Key, Regex Regex, string Severity, string Message);
}

public sealed class SecretScanResult
{
    public bool IsOk { get; set; } = true;
    public List<SecretScanFinding> Findings { get; set; } = [];
}

public sealed class SecretScanFinding
{
    public string RelativePath { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
