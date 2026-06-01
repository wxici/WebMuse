using WebRebuildRecorder.App.Core.Security;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ExportIntegrityCheckService
{
    private static readonly string[] DangerousExtensions =
    [
        ".zip",
        ".mp4",
        ".webm",
        ".mov",
        ".avi",
        ".mkv"
    ];

    private static readonly string[] DangerousDirectoryNames =
    [
        ".git",
        ".ssh",
        ".codex",
        ".openai",
        "bin",
        "obj"
    ];

    public async Task<ExportIntegrityCheckResult> CheckAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ValidateProjectRoot(projectRoot);
        var result = new ExportIntegrityCheckResult();

        AddFileExists(result, root, WrbProjectSchema.FileName, "projectManifest", required: true);
        AddFileExists(result, root, ThemeManifestSchema.RelativePath, "themeManifest", required: true);
        AddFileExists(result, root, ContentMapSchema.RelativePath, "contentMap", required: true);
        AddFileExists(result, root, AssetsManifestSchema.RelativePath, "assetsManifest", required: true);
        AddDirectoryExists(result, root, ProjectDirectoryV2.OutputCurrent, "outputCurrent", required: true);
        CheckDangerousFiles(result, root);

        var secretScan = await new SecretAndLocalPathScanService().ScanProjectAsync(root, cancellationToken);
        foreach (var finding in secretScan.Findings)
        {
            result.Items.Add(new ExportIntegrityCheckItem
            {
                Key = $"secretScan.{finding.Key}",
                Status = string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase) ? "error" : "warning",
                Message = finding.Message,
                RelativePath = finding.RelativePath
            });
        }

        result.IsOk = result.Items.All(item =>
            !string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static void AddFileExists(
        ExportIntegrityCheckResult result,
        string projectRoot,
        string relativePath,
        string key,
        bool required)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, key);
        if (!validation.IsAllowed)
        {
            result.Items.Add(new ExportIntegrityCheckItem
            {
                Key = key,
                Status = "error",
                Message = validation.Message,
                RelativePath = relativePath
            });
            return;
        }

        result.Items.Add(new ExportIntegrityCheckItem
        {
            Key = key,
            Status = File.Exists(validation.NormalizedTargetPath) ? "ok" : required ? "error" : "warning",
            Message = File.Exists(validation.NormalizedTargetPath)
                ? "Required file exists."
                : "Required file is missing.",
            RelativePath = relativePath
        });
    }

    private static void AddDirectoryExists(
        ExportIntegrityCheckResult result,
        string projectRoot,
        string relativePath,
        string key,
        bool required)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, key);
        if (!validation.IsAllowed)
        {
            result.Items.Add(new ExportIntegrityCheckItem
            {
                Key = key,
                Status = "error",
                Message = validation.Message,
                RelativePath = relativePath
            });
            return;
        }

        result.Items.Add(new ExportIntegrityCheckItem
        {
            Key = key,
            Status = Directory.Exists(validation.NormalizedTargetPath) ? "ok" : required ? "error" : "warning",
            Message = Directory.Exists(validation.NormalizedTargetPath)
                ? "Required directory exists."
                : "Required directory is missing.",
            RelativePath = relativePath
        });
    }

    private static void CheckDangerousFiles(ExportIntegrityCheckResult result, string projectRoot)
    {
        var outputRoot = Path.Combine(projectRoot, ProjectDirectoryV2.OutputCurrent);
        if (!Directory.Exists(outputRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories))
        {
            if (HasDangerousSegment(path)
                || DangerousExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                result.Items.Add(new ExportIntegrityCheckItem
                {
                    Key = "dangerousFile",
                    Status = "error",
                    Message = "Dangerous file or directory is present in export output.",
                    RelativePath = Path.GetRelativePath(projectRoot, path)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/')
                });
            }
        }
    }

    private static bool HasDangerousSegment(string path)
    {
        var parts = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => DangerousDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
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
}

public sealed class ExportIntegrityCheckResult
{
    public bool IsOk { get; set; }
    public List<ExportIntegrityCheckItem> Items { get; set; } = [];
}

public sealed class ExportIntegrityCheckItem
{
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
}
