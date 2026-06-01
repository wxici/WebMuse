using WebRebuildRecorder.App.Core.ProjectSystem;

namespace WebRebuildRecorder.App.Core.Security;

public static class SandboxPathPolicy
{
    private static readonly string[] SensitiveDirectoryNames =
    [
        ".git",
        ".ssh",
        ".codex",
        ".openai",
        ".aws",
        ".azure",
        ".gnupg"
    ];

    public static bool IsPathAllowedForCodex(string projectRoot, string targetPath)
    {
        return ValidateCodexWritePath(projectRoot, targetPath).IsAllowed;
    }

    public static bool IsInsideProject(string projectRoot, string targetPath)
    {
        return ValidateProjectPath(projectRoot, targetPath).IsAllowed;
    }

    public static SandboxPathValidationResult ValidateProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return SandboxPathValidationResult.Deny("Project root cannot be empty.");
        }

        var normalizedRoot = NormalizeFullPath(projectRoot);
        var rootSegmentCheck = CheckSensitivePathSegments(normalizedRoot);
        if (!rootSegmentCheck.IsAllowed)
        {
            return rootSegmentCheck;
        }

        var forbiddenRootCheck = CheckForbiddenRoots(normalizedRoot, normalizedRoot);
        if (!forbiddenRootCheck.IsAllowed)
        {
            return forbiddenRootCheck;
        }

        var reparsePointCheck = CheckReparsePointRisk(normalizedRoot, normalizedRoot);
        if (!reparsePointCheck.IsAllowed)
        {
            return reparsePointCheck;
        }

        return SandboxPathValidationResult.Allow(normalizedRoot, normalizedRoot, "Project root is allowed.");
    }

    public static SandboxPathValidationResult ValidateProjectPath(string projectRoot, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(targetPath))
        {
            return SandboxPathValidationResult.Deny("Project root and target path are required.");
        }

        var rootValidation = ValidateProjectRoot(projectRoot);
        if (!rootValidation.IsAllowed)
        {
            return rootValidation;
        }

        var normalizedProjectRoot = rootValidation.NormalizedProjectRoot;
        var normalizedTarget = NormalizeTargetPath(normalizedProjectRoot, targetPath);

        if (!IsSameOrInside(normalizedProjectRoot, normalizedTarget))
        {
            return SandboxPathValidationResult.Deny(
                $"Target path escapes the project root. Root: {normalizedProjectRoot}; Target: {normalizedTarget}",
                normalizedProjectRoot,
                normalizedTarget);
        }

        var segmentCheck = CheckSensitivePathSegments(normalizedTarget, normalizedProjectRoot);
        if (!segmentCheck.IsAllowed)
        {
            return segmentCheck with
            {
                NormalizedProjectRoot = normalizedProjectRoot,
                NormalizedTargetPath = normalizedTarget
            };
        }

        var reparsePointCheck = CheckReparsePointRisk(normalizedProjectRoot, normalizedTarget);
        if (!reparsePointCheck.IsAllowed)
        {
            return reparsePointCheck;
        }

        return SandboxPathValidationResult.Allow(normalizedProjectRoot, normalizedTarget, "Path is inside the project root.");
    }

    public static SandboxPathValidationResult ValidateManifestRelativePath(
        string projectRoot,
        string manifestPathValue,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(manifestPathValue))
        {
            return SandboxPathValidationResult.Deny($"Manifest path '{fieldName}' cannot be empty.");
        }

        var rootValidation = ValidateProjectRoot(projectRoot);
        if (!rootValidation.IsAllowed)
        {
            return rootValidation;
        }

        var normalizedProjectRoot = rootValidation.NormalizedProjectRoot;
        var pathValue = Environment.ExpandEnvironmentVariables(manifestPathValue.Trim());

        if (Path.IsPathRooted(pathValue))
        {
            return SandboxPathValidationResult.Deny(
                $"Manifest path '{fieldName}' must be relative, not absolute: {manifestPathValue}",
                normalizedProjectRoot,
                NormalizeFullPath(pathValue));
        }

        var segments = pathValue.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return SandboxPathValidationResult.Deny(
                $"Manifest path '{fieldName}' cannot contain '..': {manifestPathValue}",
                normalizedProjectRoot,
                pathValue);
        }

        var sensitive = segments.FirstOrDefault(segment =>
            SensitiveDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
        if (sensitive is not null)
        {
            return SandboxPathValidationResult.Deny(
                $"Manifest path '{fieldName}' contains a sensitive directory segment: {sensitive}",
                normalizedProjectRoot,
                pathValue);
        }

        var normalizedTarget = NormalizeFullPath(Path.Combine(normalizedProjectRoot, pathValue));
        if (!IsSameOrInside(normalizedProjectRoot, normalizedTarget))
        {
            return SandboxPathValidationResult.Deny(
                $"Manifest path '{fieldName}' escapes the project root: {manifestPathValue}",
                normalizedProjectRoot,
                normalizedTarget);
        }

        var reparsePointCheck = CheckReparsePointRisk(normalizedProjectRoot, normalizedTarget);
        if (!reparsePointCheck.IsAllowed)
        {
            return reparsePointCheck;
        }

        return SandboxPathValidationResult.Allow(
            normalizedProjectRoot,
            normalizedTarget,
            $"Manifest path '{fieldName}' is a safe relative project path.");
    }

    public static SandboxPathValidationResult ValidateCodexWritePath(string projectRoot, string targetPath)
    {
        var projectPathCheck = ValidateProjectPath(projectRoot, targetPath);
        if (!projectPathCheck.IsAllowed)
        {
            return projectPathCheck;
        }

        var allowed = GetAllowedCodexWriteRoots(projectPathCheck.NormalizedProjectRoot)
            .Any(root => IsSameOrInside(root, projectPathCheck.NormalizedTargetPath));

        return allowed
            ? SandboxPathValidationResult.Allow(
                projectPathCheck.NormalizedProjectRoot,
                projectPathCheck.NormalizedTargetPath,
                "Codex write path is inside an allowed project area.")
            : SandboxPathValidationResult.Deny(
                $"Codex write path is not in an allowed output/log/theme area: {projectPathCheck.NormalizedTargetPath}",
                projectPathCheck.NormalizedProjectRoot,
                projectPathCheck.NormalizedTargetPath);
    }

    public static IReadOnlyList<string> GetAllowedCodexWriteRoots(string projectRoot)
    {
        var normalizedProjectRoot = NormalizeFullPath(projectRoot);
        return
        [
            Path.Combine(normalizedProjectRoot, ProjectDirectoryV2.CodexWorkspace),
            Path.Combine(normalizedProjectRoot, ProjectDirectoryV2.OutputCurrent),
            Path.Combine(normalizedProjectRoot, ProjectDirectoryV2.Logs),
            Path.Combine(normalizedProjectRoot, ProjectDirectoryV2.Theme)
        ];
    }

    private static SandboxPathValidationResult CheckForbiddenRoots(string projectRoot, string targetPath)
    {
        foreach (var forbiddenRoot in GetForbiddenRoots())
        {
            if (IsSameOrInside(forbiddenRoot, targetPath))
            {
                return SandboxPathValidationResult.Deny(
                    $"Path is inside a forbidden root: {forbiddenRoot}",
                    projectRoot,
                    targetPath);
            }
        }

        return SandboxPathValidationResult.Allow(projectRoot, targetPath, "Path is not inside a forbidden root.");
    }

    private static IReadOnlyList<string> GetForbiddenRoots()
    {
        var roots = new List<string>();
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.System));
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddIfNotEmpty(roots, Path.Combine(userProfile, ".ssh"));
            AddIfNotEmpty(roots, Path.Combine(userProfile, ".codex"));
            AddIfNotEmpty(roots, Path.Combine(userProfile, ".openai"));
        }

        foreach (var sourceRoot in DetectSourceRepositoryRoots())
        {
            AddIfNotEmpty(roots, sourceRoot);
        }

        foreach (var applicationRoot in DetectApplicationRuntimeRoots())
        {
            AddIfNotEmpty(roots, applicationRoot);
        }

        return roots
            .Select(NormalizeFullPath)
            .Distinct(GetPathComparer())
            .ToList();
    }

    private static IEnumerable<string> DetectSourceRepositoryRoots()
    {
        var startPoints = new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory
        };

        foreach (var startPoint in startPoints)
        {
            var directory = new DirectoryInfo(NormalizeFullPath(startPoint));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "WebRebuildRecorder.slnx")))
                {
                    yield return directory.FullName;
                    break;
                }

                directory = directory.Parent;
            }
        }
    }

    private static IEnumerable<string> DetectApplicationRuntimeRoots()
    {
        var appBase = NormalizeFullPath(AppContext.BaseDirectory);
        yield return appBase;

        var directory = new DirectoryInfo(appBase);
        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.csproj").Any()
                || File.Exists(Path.Combine(directory.FullName, "WebRebuildRecorder.slnx")))
            {
                yield return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    private static SandboxPathValidationResult CheckSensitivePathSegments(
        string targetPath,
        string projectRoot = "")
    {
        var parts = targetPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        var sensitive = parts.FirstOrDefault(part =>
            SensitiveDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));

        return sensitive is null
            ? SandboxPathValidationResult.Allow(projectRoot, targetPath, "Path has no sensitive directory segment.")
            : SandboxPathValidationResult.Deny(
                $"Path contains a sensitive directory segment: {sensitive}",
                projectRoot,
                targetPath);
    }

    private static string NormalizeTargetPath(string normalizedProjectRoot, string targetPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(targetPath.Trim());
        var candidate = Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(normalizedProjectRoot, expanded);

        return NormalizeFullPath(candidate);
    }

    private static string NormalizeFullPath(string path)
    {
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrInside(string root, string target)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var normalizedRoot = NormalizeFullPath(root);
        var normalizedTarget = NormalizeFullPath(target);

        return string.Equals(normalizedRoot, normalizedTarget, comparison)
            || normalizedTarget.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison)
            || normalizedTarget.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, comparison);
    }

    private static SandboxPathValidationResult CheckReparsePointRisk(string projectRoot, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return SandboxPathValidationResult.Allow(projectRoot, targetPath, "Reparse point checks are Windows-specific.");
        }

        var normalizedProjectRoot = NormalizeFullPath(projectRoot);
        var normalizedTarget = NormalizeFullPath(targetPath);
        var existingPath = GetNearestExistingPath(normalizedTarget);
        while (!string.IsNullOrWhiteSpace(existingPath))
        {
            if (HasReparsePoint(existingPath))
            {
                return SandboxPathValidationResult.Deny(
                    $"Path passes through a reparse point and is not allowed: {existingPath}",
                    normalizedProjectRoot,
                    normalizedTarget);
            }

            if (string.Equals(
                    NormalizeFullPath(existingPath),
                    normalizedProjectRoot,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                break;
            }

            var parent = Directory.GetParent(existingPath);
            if (parent is null)
            {
                break;
            }

            existingPath = parent.FullName;
        }

        return SandboxPathValidationResult.Allow(
            normalizedProjectRoot,
            normalizedTarget,
            "Path does not pass through an existing reparse point.");
    }

    private static string GetNearestExistingPath(string path)
    {
        var current = NormalizeFullPath(path);
        while (!string.IsNullOrWhiteSpace(current)
            && !Directory.Exists(current)
            && !File.Exists(current))
        {
            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                return string.Empty;
            }

            current = parent.FullName;
        }

        return current;
    }

    private static bool HasReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static void AddIfNotEmpty(List<string> roots, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            roots.Add(path);
        }
    }
}

public sealed record SandboxPathValidationResult(
    bool IsAllowed,
    string NormalizedProjectRoot,
    string NormalizedTargetPath,
    string Message)
{
    public static SandboxPathValidationResult Allow(
        string projectRoot,
        string targetPath,
        string message)
    {
        return new SandboxPathValidationResult(true, projectRoot, targetPath, message);
    }

    public static SandboxPathValidationResult Deny(
        string message,
        string projectRoot = "",
        string targetPath = "")
    {
        return new SandboxPathValidationResult(false, projectRoot, targetPath, message);
    }
}
