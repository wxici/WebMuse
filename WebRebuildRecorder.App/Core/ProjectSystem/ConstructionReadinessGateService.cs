using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Logging;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Services;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IConstructionReadinessGateService
{
    Task<ConstructionReadinessResult> CheckAsync(
        string projectRoot,
        ConstructionReadinessMode mode = ConstructionReadinessMode.Strict,
        CancellationToken cancellationToken = default);
}

public sealed class ConstructionReadinessGateService : IConstructionReadinessGateService
{
    private static readonly string[] RequiredReadinessDirectories =
    [
        ProjectDirectoryV2.Input,
        ProjectDirectoryV2.Assets,
        ProjectDirectoryV2.Theme,
        ProjectDirectoryV2.Observation,
        ProjectDirectoryV2.CodexTask,
        ConstructionPackageContextSchema.ContextRootRelativePath,
        ConstructionReadinessSchema.ReadinessRootRelativePath,
        ProjectDirectoryV2.CodexWorkspace,
        ProjectDirectoryV2.OutputCurrent,
        ProjectDirectoryV2.Tune,
        ProjectDirectoryV2.Maps,
        ProjectDirectoryV2.Exports,
        ProjectDirectoryV2.Logs,
        ProjectDirectoryV2.Versions,
        ProjectSnapshotSchema.SnapshotRootRelativePath
    ];

    private static readonly string[] CoreManifestFiles =
    [
        WrbProjectSchema.FileName,
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath,
        ObservationPackageSchema.RelativePath,
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        ConstructionPackageContextSchema.PackageIndexRelativePath
    ];

    private static readonly string[] OptionalDesignContextFiles =
    [
        "design-context/merged.DESIGN.md",
        "design-context/design-tokens.json",
        "design-context/reference-sites.json",
        "reference-sites.json"
    ];

    private static readonly string[] ExpectedTaskAllowedRoots =
    [
        ProjectDirectoryV2.CodexWorkspace,
        ProjectDirectoryV2.OutputCurrent
    ];

    private readonly PackageValidationService packageValidationService;
    private readonly SecretAndLocalPathScanService secretScanService;
    private readonly ExportIntegrityCheckService exportIntegrityCheckService;
    private readonly ProjectSnapshotService snapshotService;
    private readonly SnapshotRestoreService snapshotRestoreService;
    private readonly ProjectLogService logService;

    public ConstructionReadinessGateService()
        : this(
            new PackageValidationService(),
            new SecretAndLocalPathScanService(),
            new ExportIntegrityCheckService(),
            new ProjectSnapshotService(),
            new SnapshotRestoreService(),
            new ProjectLogService())
    {
    }

    public ConstructionReadinessGateService(
        PackageValidationService packageValidationService,
        SecretAndLocalPathScanService secretScanService,
        ExportIntegrityCheckService exportIntegrityCheckService,
        ProjectSnapshotService snapshotService,
        SnapshotRestoreService snapshotRestoreService,
        ProjectLogService logService)
    {
        this.packageValidationService = packageValidationService;
        this.secretScanService = secretScanService;
        this.exportIntegrityCheckService = exportIntegrityCheckService;
        this.snapshotService = snapshotService;
        this.snapshotRestoreService = snapshotRestoreService;
        this.logService = logService;
    }

    public async Task<ConstructionReadinessResult> CheckAsync(
        string projectRoot,
        ConstructionReadinessMode mode = ConstructionReadinessMode.Strict,
        CancellationToken cancellationToken = default)
    {
        var result = new ConstructionReadinessResult
        {
            SchemaVersion = ConstructionReadinessSchema.CurrentSchemaVersion,
            Mode = mode,
            CheckedAt = DateTimeOffset.UtcNow
        };

        string root;
        try
        {
            root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
            Directory.CreateDirectory(ProjectPackagePathHelpers.ResolveRelativeFilePath(
                root,
                ConstructionReadinessSchema.ReadinessRootRelativePath,
                "readiness report directory"));
        }
        catch (InvalidOperationException ex)
        {
            AddItem(
                result,
                "project.root",
                "project",
                ConstructionReadinessStatus.Error,
                ex.Message,
                null,
                TaskFailureCategory.SandboxViolation);
            FinalizeResult(result);
            return result;
        }

        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(root, cancellationToken);
        result.ProjectId = identity.ProjectId;

        await CheckProjectManifestAsync(root, result, mode, cancellationToken);
        CheckDirectories(root, result, mode);
        await CheckPackageValidationAsync(root, result, mode, cancellationToken);
        await CheckDataManifestsAsync(root, result, mode, cancellationToken);
        await CheckContextPackageAsync(root, result, mode, cancellationToken);
        await CheckTaskPackageAsync(root, result, mode, cancellationToken);
        await CheckSecretScanAsync(root, result, cancellationToken);
        await CheckExportIntegrityAsync(root, result, mode, cancellationToken);
        CheckOutputSurface(root, result);
        CheckSandboxPolicy(root, result);
        await CheckRollbackProbeAsync(root, result, cancellationToken);
        await CheckEnvironmentAsync(root, result, cancellationToken);
        CheckOptionalDesignAndReferenceAwareness(root, result);
        CheckFoundationWorkflow(result, mode);

        FinalizeResult(result);
        await WriteReadinessLogsAsync(root, result, cancellationToken);
        await SaveReportsAsync(root, result, cancellationToken);
        return result;
    }

    public static string GetReportJsonPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionReadinessSchema.ReportJsonRelativePath,
            "readiness JSON report");
    }

    public static string GetReportMarkdownPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionReadinessSchema.ReportMarkdownRelativePath,
            "readiness Markdown report");
    }

    private static async Task CheckProjectManifestAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await new ProjectManifestService().LoadAsync(projectRoot, cancellationToken);
            result.ProjectId = string.IsNullOrWhiteSpace(result.ProjectId) ? manifest.ProjectId : result.ProjectId;
            AddItem(
                result,
                "project.manifest",
                "project",
                ConstructionReadinessStatus.Ok,
                "project.wrbproj exists and can be loaded.",
                WrbProjectSchema.FileName);
            AddItem(
                result,
                "project.state",
                "project",
                ConstructionReadinessStatus.Ok,
                $"Project state is {manifest.State}.",
                WrbProjectSchema.FileName);
            AddItem(
                result,
                "project.referenceUrl",
                "project",
                string.IsNullOrWhiteSpace(manifest.ReferenceUrl) && mode != ConstructionReadinessMode.Draft
                    ? ConstructionReadinessStatus.Warning
                    : ConstructionReadinessStatus.Ok,
                string.IsNullOrWhiteSpace(manifest.ReferenceUrl)
                    ? "Reference URL is empty; readiness can continue, but construction context will be thinner."
                    : "Reference URL is present.",
                WrbProjectSchema.FileName);
        }
        catch (FileNotFoundException)
        {
            AddMissingItem(result, mode, "project.manifest", "project", WrbProjectSchema.FileName);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddItem(
                result,
                "project.manifestInvalid",
                "project",
                ConstructionReadinessStatus.Error,
                $"project.wrbproj could not be loaded: {ex.Message}",
                WrbProjectSchema.FileName,
                TaskFailureCategory.ValidationError);
        }
    }

    private static void CheckDirectories(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode)
    {
        foreach (var relativePath in RequiredReadinessDirectories)
        {
            try
            {
                var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                    projectRoot,
                    relativePath,
                    "readiness directory");
                AddItem(
                    result,
                    $"directory.{relativePath}",
                    "directories",
                    Directory.Exists(fullPath) ? ConstructionReadinessStatus.Ok : MissingStatus(mode),
                    Directory.Exists(fullPath)
                        ? "Required readiness directory exists."
                        : "Required readiness directory is missing.",
                    relativePath,
                    Directory.Exists(fullPath) ? null : TaskFailureCategory.MissingInput);
            }
            catch (InvalidOperationException ex)
            {
                AddItem(
                    result,
                    $"directory.{relativePath}",
                    "directories",
                    ConstructionReadinessStatus.Error,
                    ex.Message,
                    relativePath,
                    TaskFailureCategory.SandboxViolation);
            }
        }

        foreach (var relativePath in new[]
                 {
                     ProjectDirectoryV2.OutputCurrent,
                     ProjectDirectoryV2.CodexWorkspace,
                     ProjectDirectoryV2.Logs,
                     ConstructionReadinessSchema.ReadinessRootRelativePath
                 })
        {
            CheckWritableDirectory(projectRoot, result, relativePath);
        }
    }

    private async Task CheckPackageValidationAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        var validationMode = mode == ConstructionReadinessMode.Draft
            ? PackageValidationMode.Draft
            : PackageValidationMode.Strict;

        await AddPackageValidationResultAsync(
            result,
            "package.validation.observation",
            "packageValidation",
            await packageValidationService.ValidateObservationPackageAsync(projectRoot, validationMode, cancellationToken));
        await AddPackageValidationResultAsync(
            result,
            "package.validation.construction",
            "packageValidation",
            await packageValidationService.ValidateConstructionPackageAsync(projectRoot, validationMode, cancellationToken));
        await AddPackageValidationResultAsync(
            result,
            "package.validation.task",
            "packageValidation",
            await packageValidationService.ValidateCodexTaskPackageAsync(projectRoot, validationMode, cancellationToken));
    }

    private static Task AddPackageValidationResultAsync(
        ConstructionReadinessResult result,
        string prefix,
        string category,
        PackageValidationResult validation)
    {
        foreach (var item in validation.Items)
        {
            var status = ToReadinessStatus(item.Severity);
            AddItem(
                result,
                $"{prefix}.{item.Key}",
                category,
                status,
                item.Message,
                item.RelativePath,
                status == ConstructionReadinessStatus.Error
                    ? MapFailureCategory(item.Key, item.Message)
                    : null);
        }

        return Task.CompletedTask;
    }

    private static async Task CheckDataManifestsAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        await CheckAssetsAsync(projectRoot, result, mode, cancellationToken);
        await CheckThemeAsync(projectRoot, result, mode, cancellationToken);
        await CheckContentMapAsync(projectRoot, result, mode, cancellationToken);
        await CheckObservationAsync(projectRoot, result, mode, cancellationToken);
    }

    private static async Task CheckAssetsAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        AssetsManifest manifest;
        try
        {
            manifest = await new AssetsManifestService().LoadAsync(projectRoot, cancellationToken);
            AddItem(
                result,
                "assets.manifest",
                "assets",
                ConstructionReadinessStatus.Ok,
                $"assets-manifest.json loaded with {manifest.Assets.Count} asset item(s).",
                AssetsManifestSchema.RelativePath);
        }
        catch (FileNotFoundException)
        {
            AddMissingItem(result, mode, "assets.manifest", "assets", AssetsManifestSchema.RelativePath);
            return;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddItem(
                result,
                "assets.manifestInvalid",
                "assets",
                ConstructionReadinessStatus.Error,
                $"assets-manifest.json could not be loaded: {ex.Message}",
                AssetsManifestSchema.RelativePath,
                TaskFailureCategory.ValidationError);
            return;
        }

        if (manifest.Assets.Count == 0)
        {
            AddItem(
                result,
                "assets.empty",
                "assets",
                ConstructionReadinessStatus.Warning,
                "No asset items are registered; construction can continue but brand material may be thin.",
                AssetsManifestSchema.RelativePath);
        }

        foreach (var asset in manifest.Assets)
        {
            var isReferenceSite = asset.IsExternalReference
                || asset.SourceType.Contains("reference", StringComparison.OrdinalIgnoreCase)
                || asset.SourceType.Contains("observed", StringComparison.OrdinalIgnoreCase);
            if (isReferenceSite && asset.IsApprovedForExport)
            {
                AddItem(
                    result,
                    $"assets.referenceApproved.{asset.AssetId}",
                    "assets",
                    ConstructionReadinessStatus.Error,
                    "Reference-site or observed asset is marked approved for export; this must be cleared before construction.",
                    asset.RelativePath,
                    TaskFailureCategory.ValidationError);
            }
            else if (isReferenceSite)
            {
                AddItem(
                    result,
                    $"assets.referenceOnly.{asset.AssetId}",
                    "assets",
                    ConstructionReadinessStatus.Warning,
                    "Reference-site or observed asset is registered as reference-only material.",
                    asset.RelativePath);
            }

            if ((asset.IsUserProvided || asset.IsAiGenerated) && !asset.IsApprovedForExport)
            {
                AddItem(
                    result,
                    $"assets.exportApproval.{asset.AssetId}",
                    "assets",
                    ConstructionReadinessStatus.Warning,
                    "User-provided or AI-generated asset is not yet approved for export.",
                    asset.RelativePath);
            }

            if (ContainsSensitiveReportContent(asset.SourceNote ?? string.Empty))
            {
                AddItem(
                    result,
                    $"assets.sourceNoteSensitive.{asset.AssetId}",
                    "assets",
                    ConstructionReadinessStatus.Error,
                    "Asset source note contains a secret marker or local path.",
                    asset.RelativePath,
                    TaskFailureCategory.SecretDetected);
            }
        }
    }

    private static async Task CheckThemeAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var theme = await new ThemeManifestService().LoadAsync(projectRoot, cancellationToken);
            AddItem(
                result,
                "theme.manifest",
                "theme",
                ConstructionReadinessStatus.Ok,
                $"theme.json loaded with {theme.CurrentPalette.Colors.Count} current palette color(s).",
                ThemeManifestSchema.RelativePath);
        }
        catch (FileNotFoundException)
        {
            AddMissingItem(result, mode, "theme.manifest", "theme", ThemeManifestSchema.RelativePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddItem(
                result,
                "theme.manifestInvalid",
                "theme",
                ConstructionReadinessStatus.Error,
                $"theme.json could not be loaded: {ex.Message}",
                ThemeManifestSchema.RelativePath,
                TaskFailureCategory.ValidationError);
        }
    }

    private static async Task CheckContentMapAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var contentMap = await new ContentMapService().LoadAsync(projectRoot, cancellationToken);
            var tuneIds = contentMap.Pages
                .SelectMany(page => page.Sections)
                .Select(section => section.DataTuneId)
                .Concat(contentMap.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Elements).Select(element => element.DataTuneId))
                .ToList();
            AddItem(
                result,
                "contentMap.manifest",
                "contentMap",
                ConstructionReadinessStatus.Ok,
                $"content-map.json loaded with {contentMap.Pages.Count} page(s) and {tuneIds.Count} DataTuneId value(s).",
                ContentMapSchema.RelativePath);
            if (tuneIds.Count == 0)
            {
                AddItem(
                    result,
                    "contentMap.dataTuneId.empty",
                    "contentMap",
                    ConstructionReadinessStatus.Error,
                    "No DataTuneId values are available.",
                    ContentMapSchema.RelativePath,
                    TaskFailureCategory.ValidationError);
            }
        }
        catch (FileNotFoundException)
        {
            AddMissingItem(result, mode, "contentMap.manifest", "contentMap", ContentMapSchema.RelativePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddItem(
                result,
                "contentMap.manifestInvalid",
                "contentMap",
                ConstructionReadinessStatus.Error,
                $"content-map.json could not be loaded: {ex.Message}",
                ContentMapSchema.RelativePath,
                TaskFailureCategory.ValidationError);
        }
    }

    private static async Task CheckObservationAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var observation = await new ObservationPackageService().LoadAsync(projectRoot, cancellationToken);
            AddItem(
                result,
                "observation.manifest",
                "observation",
                ConstructionReadinessStatus.Ok,
                $"observation-package.json loaded with {observation.Artifacts.Count} artifact(s), {observation.Sections.Count} section(s), {observation.Interactions.Count} interaction(s), and {observation.Findings.Count} finding(s).",
                ObservationPackageSchema.RelativePath);
            if (observation.Sections.Count == 0 && mode != ConstructionReadinessMode.Draft)
            {
                AddItem(
                    result,
                    "observation.sections.empty",
                    "observation",
                    ConstructionReadinessStatus.Warning,
                    "Observation package has no structured sections yet.",
                    ObservationPackageSchema.RelativePath);
            }

            if (!File.Exists(ProjectPackagePathHelpers.ResolveRelativeFilePath(
                    projectRoot,
                    "observation/legacy-bridge-report.json",
                    "legacy bridge report")))
            {
                AddItem(
                    result,
                    "observation.legacyBridgeReport",
                    "observation",
                    ConstructionReadinessStatus.Warning,
                    "legacy-bridge-report.json is not present; this can be regenerated by the legacy observation bridge.",
                    "observation/legacy-bridge-report.json");
            }
        }
        catch (FileNotFoundException)
        {
            AddMissingItem(result, mode, "observation.manifest", "observation", ObservationPackageSchema.RelativePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddItem(
                result,
                "observation.manifestInvalid",
                "observation",
                ConstructionReadinessStatus.Error,
                $"observation-package.json could not be loaded: {ex.Message}",
                ObservationPackageSchema.RelativePath,
                TaskFailureCategory.ValidationError);
        }
    }

    private static async Task CheckContextPackageAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        var indexPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionPackageContextSchema.PackageIndexRelativePath,
            "context package index");
        ConstructionPackageContextIndex? index = null;
        if (!File.Exists(indexPath))
        {
            AddMissingItem(
                result,
                mode,
                "context.packageIndex",
                "context",
                ConstructionPackageContextSchema.PackageIndexRelativePath);
        }
        else
        {
            try
            {
                await using var stream = File.OpenRead(indexPath);
                index = await JsonSerializer.DeserializeAsync<ConstructionPackageContextIndex>(
                    stream,
                    WrbJsonOptions.Default,
                    cancellationToken);
                if (index is null)
                {
                    throw new InvalidDataException("package-index.json is empty.");
                }

                AddItem(
                    result,
                    "context.packageIndex",
                    "context",
                    ConstructionReadinessStatus.Ok,
                    $"package-index.json loaded with {index.Files.Count} file item(s).",
                    ConstructionPackageContextSchema.PackageIndexRelativePath);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
            {
                AddItem(
                    result,
                    "context.packageIndexInvalid",
                    "context",
                    ConstructionReadinessStatus.Error,
                    $"package-index.json could not be loaded: {ex.Message}",
                    ConstructionPackageContextSchema.PackageIndexRelativePath,
                    TaskFailureCategory.ValidationError);
            }
        }

        foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
        {
            var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
                projectRoot,
                definition.RelativePath,
                "context file");
            if (!File.Exists(fullPath))
            {
                AddMissingItem(
                    result,
                    mode,
                    $"context.file.{definition.Kind}",
                    "context",
                    definition.RelativePath);
                continue;
            }

            AddItem(
                result,
                $"context.file.{definition.Kind}",
                "context",
                ConstructionReadinessStatus.Ok,
                "Context file exists.",
                definition.RelativePath);

            var indexed = index?.Files.FirstOrDefault(file =>
                string.Equals(file.RelativePath, definition.RelativePath, StringComparison.OrdinalIgnoreCase));
            if (indexed is null)
            {
                AddItem(
                    result,
                    $"context.indexEntry.{definition.Kind}",
                    "context",
                    MissingStatus(mode),
                    "Context file is not represented in package-index.json.",
                    definition.RelativePath,
                    TaskFailureCategory.MissingInput);
            }
            else if (!string.Equals(definition.RelativePath, ConstructionPackageContextSchema.PackageIndexRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                var actualSha256 = ProjectPackagePathHelpers.ComputeSha256(fullPath);
                AddItem(
                    result,
                    $"context.hash.{definition.Kind}",
                    "context",
                    string.Equals(indexed.Sha256, actualSha256, StringComparison.OrdinalIgnoreCase)
                        ? ConstructionReadinessStatus.Ok
                        : ConstructionReadinessStatus.Error,
                    string.Equals(indexed.Sha256, actualSha256, StringComparison.OrdinalIgnoreCase)
                        ? "Context file hash matches package-index.json."
                        : "Context file hash does not match package-index.json.",
                    definition.RelativePath,
                    TaskFailureCategory.ValidationError);
            }
        }

        var constraintsPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionPackageContextSchema.ConstraintsRelativePath,
            "constraints context");
        if (File.Exists(constraintsPath))
        {
            var constraints = await File.ReadAllTextAsync(constraintsPath, cancellationToken);
            var lower = constraints.ToLowerInvariant();
            foreach (var requiredPhrase in new[] { "copy", "reference", ".git", "external api", "codex cli", "zip" })
            {
                AddItem(
                    result,
                    $"context.constraints.{requiredPhrase.Replace(" ", string.Empty, StringComparison.Ordinal)}",
                    "context",
                    lower.Contains(requiredPhrase, StringComparison.Ordinal)
                        ? ConstructionReadinessStatus.Ok
                        : ConstructionReadinessStatus.Warning,
                    lower.Contains(requiredPhrase, StringComparison.Ordinal)
                        ? $"constraints.md includes the '{requiredPhrase}' safety topic."
                        : $"constraints.md does not mention the '{requiredPhrase}' safety topic.",
                    ConstructionPackageContextSchema.ConstraintsRelativePath);
            }
        }

        CheckContextFreshness(projectRoot, result, mode);
    }

    private static async Task CheckTaskPackageAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        CodexTaskPackage package;
        try
        {
            package = await new CodexTaskPackageService().LoadAsync(projectRoot, cancellationToken);
            AddItem(
                result,
                "task.package",
                "taskPackage",
                ConstructionReadinessStatus.Ok,
                "task-package.json loaded.",
                CodexTaskPackageSchema.RelativePath);
        }
        catch (FileNotFoundException)
        {
            AddMissingItem(result, mode, "task.package", "taskPackage", CodexTaskPackageSchema.RelativePath);
            return;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            AddItem(
                result,
                "task.packageInvalid",
                "taskPackage",
                ConstructionReadinessStatus.Error,
                $"task-package.json could not be loaded: {ex.Message}",
                CodexTaskPackageSchema.RelativePath,
                TaskFailureCategory.ValidationError);
            return;
        }

        var allowed = package.Sandbox.AllowedWriteRoots
            .Select(NormalizeSlash)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var expected = ExpectedTaskAllowedRoots
            .Select(NormalizeSlash)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var allowedRootsAreExact = allowed.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase);
        AddItem(
            result,
            "task.allowedWriteRoots.exact",
            "taskPackage",
            allowedRootsAreExact ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Error,
            allowedRootsAreExact
                ? "Task package allowed write roots are limited to codex-workspace/ and output-site/current/."
                : "Task package allowed write roots must be exactly codex-workspace/ and output-site/current/.",
            CodexTaskPackageSchema.RelativePath,
            TaskFailureCategory.SandboxViolation);

        AddItem(
            result,
            "task.forbiddenRoots",
            "taskPackage",
            package.Sandbox.ForbiddenRoots.Count > 0 ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Error,
            package.Sandbox.ForbiddenRoots.Count > 0
                ? "Task package forbidden roots are present."
                : "Task package forbidden roots are missing.",
            CodexTaskPackageSchema.RelativePath,
            TaskFailureCategory.SandboxViolation);

        foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
        {
            var contains = package.InputFiles.Any(input =>
                string.Equals(input.RelativePath, definition.RelativePath, StringComparison.OrdinalIgnoreCase));
            AddItem(
                result,
                $"task.input.{definition.Kind}",
                "taskPackage",
                contains ? ConstructionReadinessStatus.Ok : MissingStatus(mode),
                contains ? "Task package references context file." : "Task package does not reference required context file.",
                definition.RelativePath,
                TaskFailureCategory.MissingInput);
        }

        var instructionsPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            CodexTaskPackageSchema.InstructionsRelativePath,
            "task instructions");
        if (!File.Exists(instructionsPath))
        {
            AddMissingItem(result, mode, "task.instructions", "taskPackage", CodexTaskPackageSchema.InstructionsRelativePath);
            return;
        }

        var instructions = await File.ReadAllTextAsync(instructionsPath, cancellationToken);
        foreach (var required in new[]
                 {
                     "Reading Order",
                     ConstructionPackageContextSchema.ProjectBriefRelativePath,
                     ConstructionPackageContextSchema.PackageIndexRelativePath,
                     "Do not call OpenAI APIs",
                     "not a clone"
                 })
        {
            AddItem(
                result,
                $"task.instructions.{required.Replace("/", ".").Replace(" ", string.Empty, StringComparison.Ordinal)}",
                "taskPackage",
                instructions.Contains(required, StringComparison.OrdinalIgnoreCase)
                    ? ConstructionReadinessStatus.Ok
                    : ConstructionReadinessStatus.Error,
                instructions.Contains(required, StringComparison.OrdinalIgnoreCase)
                    ? $"instructions.md includes '{required}'."
                    : $"instructions.md is missing '{required}'.",
                CodexTaskPackageSchema.InstructionsRelativePath,
                TaskFailureCategory.ValidationError);
        }
    }

    private async Task CheckSecretScanAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        CancellationToken cancellationToken)
    {
        var scan = await secretScanService.ScanProjectAsync(projectRoot, cancellationToken);
        if (scan.Findings.Count == 0)
        {
            AddItem(
                result,
                "secretScan.clean",
                "secretScan",
                ConstructionReadinessStatus.Ok,
                "No secret or local-path markers were found in scanned project files.");
            return;
        }

        foreach (var finding in scan.Findings)
        {
            var status = ToReadinessStatus(finding.Severity);
            AddItem(
                result,
                $"secretScan.{finding.Key}",
                "secretScan",
                status,
                finding.Message,
                finding.RelativePath,
                status == ConstructionReadinessStatus.Error
                    ? TaskFailureCategory.SecretDetected
                    : null);
        }
    }

    private async Task CheckExportIntegrityAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        CancellationToken cancellationToken)
    {
        var export = await exportIntegrityCheckService.CheckAsync(projectRoot, cancellationToken);
        foreach (var item in export.Items)
        {
            var status = ToReadinessStatus(item.Status);
            if (string.Equals(item.Key, "outputCurrent", StringComparison.OrdinalIgnoreCase)
                && status == ConstructionReadinessStatus.Error
                && mode == ConstructionReadinessMode.Draft)
            {
                status = ConstructionReadinessStatus.Warning;
            }

            AddItem(
                result,
                $"exportIntegrity.{item.Key}",
                "exportIntegrity",
                status,
                item.Message,
                item.RelativePath,
                status == ConstructionReadinessStatus.Error
                    ? MapFailureCategory(item.Key, item.Message)
                    : null);
        }
    }

    private static void CheckOutputSurface(
        string projectRoot,
        ConstructionReadinessResult result)
    {
        var outputRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ProjectDirectoryV2.OutputCurrent,
            "output surface");
        AddItem(
            result,
            "outputSurface.directory",
            "outputSurface",
            Directory.Exists(outputRoot) ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Error,
            Directory.Exists(outputRoot)
                ? "output-site/current exists."
                : "output-site/current is missing.",
            ProjectDirectoryV2.OutputCurrent,
            TaskFailureCategory.OutputMissing);

        var indexPath = Path.Combine(outputRoot, "index.html");
        AddItem(
            result,
            "outputSurface.indexOptional",
            "outputSurface",
            ConstructionReadinessStatus.Ok,
            File.Exists(indexPath)
                ? "Generated index.html exists, but the readiness gate does not require it."
                : "Generated index.html is not required for P1.5 readiness.",
            $"{ProjectDirectoryV2.OutputCurrent}/index.html");
    }

    private static void CheckSandboxPolicy(
        string projectRoot,
        ConstructionReadinessResult result)
    {
        foreach (var relativePath in new[]
                 {
                     $"{ProjectDirectoryV2.CodexWorkspace}/readiness-check.txt",
                     $"{ProjectDirectoryV2.OutputCurrent}/readiness-check.txt",
                     $"{ProjectDirectoryV2.Logs}/readiness-check.txt"
                 })
        {
            var fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var validation = SandboxPathPolicy.ValidateCodexWritePath(projectRoot, fullPath);
            AddItem(
                result,
                $"sandbox.allowed.{relativePath}",
                "sandbox",
                validation.IsAllowed ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Error,
                validation.Message,
                relativePath,
                validation.IsAllowed ? null : TaskFailureCategory.SandboxViolation);
        }

        var codexTaskValidation = SandboxPathPolicy.ValidateProjectPath(
            projectRoot,
            Path.Combine(projectRoot, ConstructionReadinessSchema.ReadinessRootRelativePath, "readiness-report.json"));
        AddItem(
            result,
            "sandbox.projectPath.codexTaskReadiness",
            "sandbox",
            codexTaskValidation.IsAllowed ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Error,
            codexTaskValidation.IsAllowed
                ? "codex-task/readiness is a safe project path for readiness reports."
                : codexTaskValidation.Message,
            ConstructionReadinessSchema.ReportJsonRelativePath,
            codexTaskValidation.IsAllowed ? null : TaskFailureCategory.SandboxViolation);

        foreach (var forbidden in new[] { ".git/config", "../outside.txt", ".ssh/id_rsa", ".codex/config", ".openai/config" })
        {
            var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, forbidden, "readiness forbidden path");
            AddItem(
                result,
                $"sandbox.forbidden.{forbidden.Replace("/", ".").Replace("..", "parent", StringComparison.Ordinal)}",
                "sandbox",
                validation.IsAllowed ? ConstructionReadinessStatus.Error : ConstructionReadinessStatus.Ok,
                validation.IsAllowed
                    ? "Forbidden path was unexpectedly allowed by sandbox policy."
                    : "Forbidden path is blocked by sandbox policy.",
                forbidden,
                validation.IsAllowed ? TaskFailureCategory.SandboxViolation : null);
        }
    }

    private async Task CheckRollbackProbeAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var probe = await snapshotService.CreateSnapshotAsync(projectRoot, "readiness-probe", cancellationToken);
            AddItem(
                result,
                "rollback.readinessProbeSnapshot",
                "rollback",
                probe.SnapshotId.StartsWith("readiness-probe-", StringComparison.OrdinalIgnoreCase)
                    ? ConstructionReadinessStatus.Ok
                    : ConstructionReadinessStatus.Error,
                "This snapshot was created only to verify rollback availability.",
                $"{ProjectSnapshotSchema.SnapshotRootRelativePath}/{probe.SnapshotId}/{ProjectSnapshotSchema.ManifestFileName}",
                TaskFailureCategory.InternalError);

            var listed = await snapshotRestoreService.ListSnapshotsAsync(projectRoot, cancellationToken);
            AddItem(
                result,
                "rollback.snapshotList",
                "rollback",
                listed.Any(snapshot => string.Equals(snapshot.SnapshotId, probe.SnapshotId, StringComparison.OrdinalIgnoreCase))
                    ? ConstructionReadinessStatus.Ok
                    : ConstructionReadinessStatus.Error,
                "Snapshot restore service can list the readiness-probe snapshot.",
                ProjectSnapshotSchema.SnapshotRootRelativePath,
                TaskFailureCategory.InternalError);

            var validation = await snapshotRestoreService.ValidateSnapshotAsync(projectRoot, probe.SnapshotId, cancellationToken);
            AddItem(
                result,
                "rollback.snapshotValidation",
                "rollback",
                validation.IsOk ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Error,
                validation.IsOk
                    ? "Readiness-probe snapshot validates."
                    : "Readiness-probe snapshot validation failed.",
                $"{ProjectSnapshotSchema.SnapshotRootRelativePath}/{probe.SnapshotId}/{ProjectSnapshotSchema.ManifestFileName}",
                validation.IsOk ? null : TaskFailureCategory.ValidationError);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            AddItem(
                result,
                "rollback.readinessProbeSnapshot",
                "rollback",
                ConstructionReadinessStatus.Error,
                $"Readiness-probe snapshot could not be created or validated: {ex.Message}",
                ProjectSnapshotSchema.SnapshotRootRelativePath,
                TaskFailureCategory.InternalError);
        }
    }

    private static async Task CheckEnvironmentAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        CancellationToken cancellationToken)
    {
        var environment = await new EnvironmentCheckService().CheckAsync(projectRoot, cancellationToken);
        foreach (var item in environment.Items)
        {
            var status = item.Status switch
            {
                EnvironmentCheckStatus.Ok => ConstructionReadinessStatus.Ok,
                EnvironmentCheckStatus.Error => ConstructionReadinessStatus.Error,
                EnvironmentCheckStatus.Skipped => ConstructionReadinessStatus.Skipped,
                _ => ConstructionReadinessStatus.Warning
            };

            if (string.Equals(item.Key, "codexCli", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Key, "codexLogin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Key, "webview2Runtime", StringComparison.OrdinalIgnoreCase))
            {
                status = ConstructionReadinessStatus.Skipped;
            }

            AddItem(
                result,
                $"environment.{item.Key}",
                "environment",
                status,
                $"{item.DisplayName}: {item.Message}",
                null,
                status == ConstructionReadinessStatus.Error
                    ? TaskFailureCategory.EnvironmentMissing
                    : null);
        }

        foreach (var key in new[] { "ollama", "lmStudio", "manualExportMode" })
        {
            AddItem(
                result,
                $"aiEngine.{key}",
                "aiEngine",
                key == "manualExportMode" ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Warning,
                key == "manualExportMode"
                    ? "Manual export mode remains available; no AI engine is required by P1.5."
                    : "AI engine status is not required by P1.5 and is recorded as non-blocking awareness only.");
        }
    }

    private static void CheckOptionalDesignAndReferenceAwareness(
        string projectRoot,
        ConstructionReadinessResult result)
    {
        foreach (var relativePath in OptionalDesignContextFiles)
        {
            var fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            AddItem(
                result,
                $"optionalAwareness.{relativePath.Replace("/", ".").Replace(".", "_", StringComparison.Ordinal)}",
                "optionalAwareness",
                File.Exists(fullPath) ? ConstructionReadinessStatus.Ok : ConstructionReadinessStatus.Warning,
                File.Exists(fullPath)
                    ? "Optional Design Context / Reference Portal file is present."
                    : "Optional Design Context / Reference Portal file is not present; P1.5 only records awareness.",
                relativePath);
        }
    }

    private static void CheckFoundationWorkflow(
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode)
    {
        var sourceRoot = FindSourceRoot();
        var parentRoot = string.IsNullOrWhiteSpace(sourceRoot)
            ? string.Empty
            : Directory.GetParent(sourceRoot)?.FullName ?? string.Empty;
        var workflowCandidates = new[]
        {
            Path.Combine(sourceRoot, ".github", "workflows", "webrebuildrecorder-foundation.yml"),
            Path.Combine(parentRoot, ".github", "workflows", "webrebuildrecorder-foundation.yml")
        };

        var workflowPath = workflowCandidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(workflowPath))
        {
            AddItem(
                result,
                "githubActions.workflow",
                "githubActions",
                MissingStatus(mode),
                "Foundation GitHub Actions workflow file was not found.",
                ".github/workflows/webrebuildrecorder-foundation.yml",
                TaskFailureCategory.EnvironmentMissing);
            return;
        }

        var workflow = File.ReadAllText(workflowPath);
        foreach (var required in new[]
                 {
                     "workflow_dispatch",
                     "windows-latest",
                     "timeout-minutes: 15",
                     "dotnet build WebRebuildRecorder.slnx",
                     "WebRebuildRecorder.FoundationSelfTest"
                 })
        {
            AddItem(
                result,
                $"githubActions.workflow.{required.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(".", "_", StringComparison.Ordinal)}",
                "githubActions",
                workflow.Contains(required, StringComparison.OrdinalIgnoreCase)
                    ? ConstructionReadinessStatus.Ok
                    : MissingStatus(mode),
                workflow.Contains(required, StringComparison.OrdinalIgnoreCase)
                    ? $"Workflow includes '{required}'."
                    : $"Workflow is missing '{required}'.",
                ".github/workflows/webrebuildrecorder-foundation.yml",
                TaskFailureCategory.EnvironmentMissing);
        }
    }

    private static void CheckContextFreshness(
        string projectRoot,
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode)
    {
        var existingSourceFiles = CoreManifestFiles
            .Where(relativePath => !string.Equals(relativePath, ConstructionPackageContextSchema.PackageIndexRelativePath, StringComparison.OrdinalIgnoreCase))
            .Select(relativePath => ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "freshness source"))
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .ToList();
        var existingContextFiles = ConstructionPackageContextSchema.ContextFiles
            .Select(definition => ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, definition.RelativePath, "freshness context"))
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .ToList();

        if (existingSourceFiles.Count == 0 || existingContextFiles.Count == 0)
        {
            AddItem(
                result,
                "context.freshness",
                "context",
                ConstructionReadinessStatus.Warning,
                "Context freshness could not be fully checked because source or context files are missing. This is a minimal P1.5 freshness check based on file timestamps; future rounds may upgrade it to source hash tracking.",
                ConstructionPackageContextSchema.ContextRootRelativePath);
            return;
        }

        var newestSource = existingSourceFiles.Max(info => info.LastWriteTimeUtc);
        var oldestContext = existingContextFiles.Min(info => info.LastWriteTimeUtc);
        var isFreshEnough = newestSource <= oldestContext.AddSeconds(2);
        AddItem(
            result,
            "context.freshness",
            "context",
            isFreshEnough
                ? ConstructionReadinessStatus.Ok
                : mode == ConstructionReadinessMode.PreCodexDryRun
                    ? ConstructionReadinessStatus.Error
                    : ConstructionReadinessStatus.Warning,
            isFreshEnough
                ? "Construction context files are not older than the core source manifests."
                : "Construction context files may be stale. This is a minimal P1.5 freshness check based on file timestamps; future rounds may upgrade it to source hash tracking.",
            ConstructionPackageContextSchema.ContextRootRelativePath,
            TaskFailureCategory.ValidationError);
    }

    private static void CheckWritableDirectory(
        string projectRoot,
        ConstructionReadinessResult result,
        string relativeDirectory)
    {
        try
        {
            var directory = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativeDirectory, "writable directory");
            if (!Directory.Exists(directory))
            {
                AddItem(
                    result,
                    $"directory.writable.{relativeDirectory}",
                    "directories",
                    ConstructionReadinessStatus.Error,
                    "Directory is missing and cannot be accepted as writable.",
                    relativeDirectory,
                    TaskFailureCategory.EnvironmentMissing);
                return;
            }

            var probe = Path.Combine(directory, ".readiness-write-check.tmp");
            File.WriteAllText(probe, "ok", Encoding.UTF8);
            File.Delete(probe);
            AddItem(
                result,
                $"directory.writable.{relativeDirectory}",
                "directories",
                ConstructionReadinessStatus.Ok,
                "Directory is writable.",
                relativeDirectory);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            AddItem(
                result,
                $"directory.writable.{relativeDirectory}",
                "directories",
                ConstructionReadinessStatus.Error,
                $"Directory write probe failed: {ex.Message}",
                relativeDirectory,
                TaskFailureCategory.EnvironmentMissing);
        }
    }

    private async Task WriteReadinessLogsAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        CancellationToken cancellationToken)
    {
        var level = result.IsReady ? ProjectLogLevel.Info : ProjectLogLevel.Warning;
        await logService.WriteAsync(
            projectRoot,
            "project",
            $"P1.5 readiness gate completed. mode={result.Mode}; ready={result.IsReady}; blockers={result.BlockingReasons.Count}",
            level,
            cancellationToken);
        await logService.WriteAsync(
            projectRoot,
            "security",
            $"P1.5 readiness security checks completed. ready={result.IsReady}; blockers={result.BlockingReasons.Count}",
            level,
            cancellationToken);
        await logService.WriteAsync(
            projectRoot,
            "codex-task",
            $"P1.5 readiness gate completed without executing Codex CLI. mode={result.Mode}; ready={result.IsReady}",
            level,
            cancellationToken);
    }

    private static async Task SaveReportsAsync(
        string projectRoot,
        ConstructionReadinessResult result,
        CancellationToken cancellationToken)
    {
        var jsonPath = GetReportJsonPath(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        await using (var stream = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(stream, result, WrbJsonOptions.Default, cancellationToken);
        }

        var markdownPath = GetReportMarkdownPath(projectRoot);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdownReport(result), Encoding.UTF8, cancellationToken);
    }

    private static string BuildMarkdownReport(ConstructionReadinessResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Construction Readiness Report");
        builder.AppendLine();
        builder.AppendLine($"- Schema version: `{Sanitize(result.SchemaVersion)}`");
        builder.AppendLine($"- Project id: `{Sanitize(result.ProjectId)}`");
        builder.AppendLine($"- Mode: `{result.Mode}`");
        builder.AppendLine($"- Ready: `{result.IsReady}`");
        builder.AppendLine($"- Checked at: `{result.CheckedAt:O}`");
        builder.AppendLine();
        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        if (result.BlockingReasons.Count == 0)
        {
            builder.AppendLine("- None.");
        }
        else
        {
            foreach (var reason in result.BlockingReasons)
            {
                builder.AppendLine($"- {Sanitize(reason)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Items");
        builder.AppendLine();
        builder.AppendLine("| Key | Category | Status | Blocks | Failure category | Path | Message |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in result.Items)
        {
            builder.AppendLine(
                $"| {EscapeTable(item.Key)} | {EscapeTable(item.Category)} | {item.Status} | {item.BlocksExecution} | {EscapeTable(item.FailureCategory?.ToString() ?? string.Empty)} | {EscapeTable(item.RelativePath ?? string.Empty)} | {EscapeTable(item.Message)} |");
        }

        return builder.ToString();
    }

    private static void FinalizeResult(ConstructionReadinessResult result)
    {
        foreach (var item in result.Items)
        {
            item.Key = Sanitize(item.Key);
            item.Category = Sanitize(item.Category);
            item.Message = Sanitize(item.Message);
            item.RelativePath = item.RelativePath is null ? null : Sanitize(ToProjectRelativeDisplayPath(item.RelativePath));
            if (item.Status == ConstructionReadinessStatus.Error)
            {
                item.BlocksExecution = true;
                item.FailureCategory ??= MapFailureCategory(item.Key, item.Message);
            }
        }

        result.IsReady = result.Items.All(item => !item.BlocksExecution);
        result.BlockingReasons = result.Items
            .Where(item => item.BlocksExecution)
            .Select(item => $"{item.Key}: {item.Message}")
            .Select(Sanitize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.Warnings = result.Items
            .Where(item => item.Status == ConstructionReadinessStatus.Warning)
            .Select(item => $"{item.Key}: {item.Message}")
            .Select(Sanitize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddMissingItem(
        ConstructionReadinessResult result,
        ConstructionReadinessMode mode,
        string key,
        string category,
        string relativePath)
    {
        AddItem(
            result,
            key,
            category,
            MissingStatus(mode),
            "Required file or directory is missing.",
            relativePath,
            TaskFailureCategory.MissingInput);
    }

    private static void AddItem(
        ConstructionReadinessResult result,
        string key,
        string category,
        ConstructionReadinessStatus status,
        string message,
        string? relativePath = null,
        TaskFailureCategory? failureCategory = null)
    {
        result.Items.Add(new ConstructionReadinessItem
        {
            Key = Sanitize(key),
            Category = Sanitize(category),
            Status = status,
            Message = Sanitize(message),
            RelativePath = relativePath is null ? null : Sanitize(ToProjectRelativeDisplayPath(relativePath)),
            BlocksExecution = status == ConstructionReadinessStatus.Error,
            FailureCategory = status == ConstructionReadinessStatus.Error
                ? failureCategory ?? MapFailureCategory(key, message)
                : null
        });
    }

    private static ConstructionReadinessStatus MissingStatus(ConstructionReadinessMode mode)
    {
        return mode == ConstructionReadinessMode.Draft
            ? ConstructionReadinessStatus.Warning
            : ConstructionReadinessStatus.Error;
    }

    private static ConstructionReadinessStatus ToReadinessStatus(string status)
    {
        return status.Equals("ok", StringComparison.OrdinalIgnoreCase)
            ? ConstructionReadinessStatus.Ok
            : status.Equals("skipped", StringComparison.OrdinalIgnoreCase)
                ? ConstructionReadinessStatus.Skipped
                : status.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? ConstructionReadinessStatus.Warning
                    : ConstructionReadinessStatus.Error;
    }

    private static TaskFailureCategory MapFailureCategory(string key, string message)
    {
        var text = $"{key} {message}";
        if (text.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || text.Contains("token", StringComparison.OrdinalIgnoreCase)
            || text.Contains("api key", StringComparison.OrdinalIgnoreCase)
            || text.Contains("OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.SecretDetected;
        }

        if (text.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            || text.Contains("absolute", StringComparison.OrdinalIgnoreCase)
            || text.Contains("outside", StringComparison.OrdinalIgnoreCase)
            || text.Contains("..", StringComparison.Ordinal)
            || text.Contains(".git", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".ssh", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".codex", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".openai", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.SandboxViolation;
        }

        if (text.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || text.Contains("required file", StringComparison.OrdinalIgnoreCase)
            || text.Contains("required directory", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.MissingInput;
        }

        if (text.Contains("output", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.OutputMissing;
        }

        if (text.Contains("environment", StringComparison.OrdinalIgnoreCase)
            || text.Contains("writable", StringComparison.OrdinalIgnoreCase))
        {
            return TaskFailureCategory.EnvironmentMissing;
        }

        return TaskFailureCategory.ValidationError;
    }

    private static bool ContainsSensitiveReportContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value,
            @"(?i)(sk-[A-Za-z0-9_\-]{3,}|OPENAI_API_KEY|api[_-]?key\s*[:=]|\b(token|secret|password)\b\s*[:=]|[A-Za-z]:\\+|[A-Za-z]:/+|/home/|\.ssh|\.codex|\.openai)");
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value.ReplaceLineEndings(" ").Trim();
        sanitized = Regex.Replace(sanitized, @"sk-[A-Za-z0-9_\-]{3,}", "<redacted-secret>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)(OPENAI_API_KEY|api[_-]?key|token|secret|password)\s*[:=]\s*[^,\s;]+", "$1=<redacted-secret>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:\\[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"(?i)[A-Za-z]:/[^\s\)""']+", "<redacted-local-path>");
        sanitized = Regex.Replace(sanitized, @"/home/[^\s\)""']+", "<redacted-local-path>", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)\.(ssh|codex|openai)(?=$|[\\/ .:;])", "<redacted-credential-dir>");
        return sanitized;
    }

    private static string EscapeTable(string value)
    {
        return Sanitize(value).Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string ToProjectRelativeDisplayPath(string path)
    {
        var normalized = NormalizeSlash(path);
        if (Path.IsPathRooted(normalized))
        {
            return "<redacted-local-path>";
        }

        return normalized;
    }

    private static string NormalizeSlash(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Replace('\\', '/')
            .Trim('/');
    }

    private static string FindSourceRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "WebRebuildRecorder.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return string.Empty;
    }
}
