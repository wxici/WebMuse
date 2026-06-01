using System.Text.Json;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class PackageValidationService
{
    private static readonly IReadOnlyList<string> StrictReadinessFiles =
    [
        WrbProjectSchema.FileName,
        ObservationPackageSchema.RelativePath,
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath,
        ConstructionPackageSchema.RelativePath,
        CodexTaskPackageSchema.RelativePath,
        CodexTaskPackageSchema.InstructionsRelativePath,
        ConstructionPackageContextSchema.PackageIndexRelativePath
    ];

    public Task<PackageValidationResult> ValidateObservationPackageAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        return ValidateObservationPackageAsync(projectRoot, PackageValidationMode.Draft, cancellationToken);
    }

    public async Task<PackageValidationResult> ValidateObservationPackageAsync(
        string projectRoot,
        PackageValidationMode mode = PackageValidationMode.Draft,
        CancellationToken cancellationToken = default)
    {
        var result = new PackageValidationResult();
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        AddRequiredFileCheck(
            result,
            root,
            ObservationPackageSchema.RelativePath,
            "observation.manifest",
            MissingSeverity(mode, draftSeverity: "warning"));

        ObservationPackageManifest? manifest = null;
        try
        {
            manifest = await new ObservationPackageService().LoadAsync(root, cancellationToken);
            AddSchemaCheck(result, "observation.schemaVersion", manifest.SchemaVersion, ObservationPackageSchema.CurrentSchemaVersion, ObservationPackageSchema.RelativePath);
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            result.Items.Add(Error("observation.manifestInvalid", ex.Message, ObservationPackageSchema.RelativePath));
        }

        if (manifest is not null)
        {
            foreach (var artifact in manifest.Artifacts)
            {
                AddRelativePathCheck(result, root, artifact.RelativePath, $"observation.artifact.{artifact.ArtifactId}");
            }
        }

        await AddSecretScanFindingsAsync(result, root, cancellationToken);
        FinalizeResult(result);
        return result;
    }

    public Task<PackageValidationResult> ValidateConstructionPackageAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        return ValidateConstructionPackageAsync(projectRoot, PackageValidationMode.Draft, cancellationToken);
    }

    public async Task<PackageValidationResult> ValidateConstructionPackageAsync(
        string projectRoot,
        PackageValidationMode mode = PackageValidationMode.Draft,
        CancellationToken cancellationToken = default)
    {
        var result = new PackageValidationResult();
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        AddManifestExists(result, root, ConstructionPackageSchema.RelativePath, "construction.manifest");

        ConstructionPackageManifest? manifest = null;
        try
        {
            manifest = await new ConstructionPackageService().LoadAsync(root, cancellationToken);
            AddSchemaCheck(result, "construction.schemaVersion", manifest.SchemaVersion, ConstructionPackageSchema.CurrentSchemaVersion, ConstructionPackageSchema.RelativePath);
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            result.Items.Add(Error("construction.manifestInvalid", ex.Message, ConstructionPackageSchema.RelativePath));
        }

        if (manifest is not null)
        {
            foreach (var requiredFile in manifest.RequiredProjectFiles)
            {
                AddRequiredFileCheck(result, root, requiredFile, $"construction.required.{requiredFile}", MissingSeverity(mode));
            }

            foreach (var input in manifest.Inputs)
            {
                AddRelativePathCheck(result, root, input.RelativePath, $"construction.input.{input.InputId}");
            }

            foreach (var deliverable in manifest.Deliverables)
            {
                AddRelativePathCheck(result, root, deliverable.TargetRelativePath, $"construction.deliverable.{deliverable.DeliverableId}");
            }
        }

        AddContextReadinessChecks(result, root, mode);
        await AddContentMapTuneIdCheckAsync(result, root, mode, cancellationToken);
        await AddSecretScanFindingsAsync(result, root, cancellationToken);
        FinalizeResult(result);
        return result;
    }

    public Task<PackageValidationResult> ValidateCodexTaskPackageAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        return ValidateCodexTaskPackageAsync(projectRoot, PackageValidationMode.Draft, cancellationToken);
    }

    public async Task<PackageValidationResult> ValidateCodexTaskPackageAsync(
        string projectRoot,
        PackageValidationMode mode = PackageValidationMode.Draft,
        CancellationToken cancellationToken = default)
    {
        var result = new PackageValidationResult();
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        AddManifestExists(result, root, CodexTaskPackageSchema.RelativePath, "task.manifest");
        AddManifestExists(result, root, CodexTaskPackageSchema.InstructionsRelativePath, "task.instructions");
        AddReadinessFileChecks(result, root, mode);

        CodexTaskPackage? package = null;
        try
        {
            package = await new CodexTaskPackageService().LoadAsync(root, cancellationToken);
            AddSchemaCheck(result, "task.schemaVersion", package.SchemaVersion, CodexTaskPackageSchema.CurrentSchemaVersion, CodexTaskPackageSchema.RelativePath);
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            result.Items.Add(Error("task.manifestInvalid", ex.Message, CodexTaskPackageSchema.RelativePath));
        }

        if (package is not null)
        {
            if (package.Sandbox.AllowedWriteRoots.Count == 0)
            {
                result.Items.Add(Error("task.allowedWriteRoots", "Task package must define at least one allowed write root.", CodexTaskPackageSchema.RelativePath));
            }

            if (package.Sandbox.ForbiddenRoots.Count == 0)
            {
                result.Items.Add(Error("task.forbiddenRoots", "Task package must define forbidden roots.", CodexTaskPackageSchema.RelativePath));
            }

            AddRelativePathCheck(result, root, package.Sandbox.WorkspaceRelativePath, "task.workspace");
            foreach (var allowedRoot in package.Sandbox.AllowedWriteRoots)
            {
                AddRelativePathCheck(result, root, allowedRoot, $"task.allowedWriteRoot.{allowedRoot}");
            }

            foreach (var input in package.InputFiles)
            {
                AddRelativePathCheck(result, root, input.RelativePath, $"task.input.{input.Role}");
                if (input.Required)
                {
                    AddRequiredFileCheck(result, root, input.RelativePath, $"task.requiredInput.{input.Role}", MissingSeverity(mode));
                }
            }

            foreach (var expected in package.ExpectedOutputs)
            {
                AddRelativePathCheck(result, root, expected.RelativePath, $"task.expectedOutput.{expected.RelativePath}");
            }
        }

        await AddContentMapTuneIdCheckAsync(result, root, mode, cancellationToken);
        await AddSecretScanFindingsAsync(result, root, cancellationToken);
        FinalizeResult(result);
        return result;
    }

    private static void AddManifestExists(
        PackageValidationResult result,
        string projectRoot,
        string relativePath,
        string key)
    {
        AddRequiredFileCheck(result, projectRoot, relativePath, key, missingSeverity: "error");
    }

    private static void AddReadinessFileChecks(
        PackageValidationResult result,
        string projectRoot,
        PackageValidationMode mode)
    {
        foreach (var relativePath in StrictReadinessFiles)
        {
            AddRequiredFileCheck(
                result,
                projectRoot,
                relativePath,
                $"strict.required.{relativePath}",
                MissingSeverity(mode));
        }
    }

    private static void AddContextReadinessChecks(
        PackageValidationResult result,
        string projectRoot,
        PackageValidationMode mode)
    {
        foreach (var definition in ConstructionPackageContextSchema.ContextFiles)
        {
            AddRequiredFileCheck(
                result,
                projectRoot,
                definition.RelativePath,
                $"construction.context.{definition.Kind}",
                MissingSeverity(mode));
        }
    }

    private static void AddRequiredFileCheck(
        PackageValidationResult result,
        string projectRoot,
        string relativePath,
        string key,
        string missingSeverity)
    {
        try
        {
            var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, key);
            var exists = File.Exists(fullPath);
            result.Items.Add(new PackageValidationItem
            {
                Key = key,
                Severity = exists ? "ok" : missingSeverity,
                Message = exists ? "File exists." : "Required file is missing.",
                RelativePath = relativePath
            });
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error(key, ex.Message, relativePath));
        }
    }

    private static void AddRelativePathCheck(
        PackageValidationResult result,
        string projectRoot,
        string relativePath,
        string key)
    {
        try
        {
            var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, key);
            result.Items.Add(new PackageValidationItem
            {
                Key = key,
                Severity = "ok",
                Message = "Path is a safe project-relative path.",
                RelativePath = normalized
            });
        }
        catch (InvalidOperationException ex)
        {
            result.Items.Add(Error(key, ex.Message, relativePath));
        }
    }

    private static void AddSchemaCheck(
        PackageValidationResult result,
        string key,
        string actual,
        string expected,
        string relativePath)
    {
        result.Items.Add(new PackageValidationItem
        {
            Key = key,
            Severity = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ? "ok" : "error",
            Message = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? "schemaVersion is supported."
                : $"schemaVersion '{actual}' is not supported. Expected '{expected}'.",
            RelativePath = relativePath
        });
    }

    private static async Task AddContentMapTuneIdCheckAsync(
        PackageValidationResult result,
        string projectRoot,
        PackageValidationMode mode,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(ContentMapService.GetContentMapPath(projectRoot)))
        {
            result.Items.Add(new PackageValidationItem
            {
                Key = "contentMap.dataTuneId",
                Severity = MissingSeverity(mode),
                Message = "content-map.json is missing; DataTuneId availability could not be checked.",
                RelativePath = ContentMapSchema.RelativePath
            });
            return;
        }

        try
        {
            var contentMap = await new ContentMapService().LoadAsync(projectRoot, cancellationToken);
            var tuneIds = contentMap.Pages
                .SelectMany(page => page.Sections)
                .Select(section => section.DataTuneId)
                .Concat(contentMap.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Elements).Select(element => element.DataTuneId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            result.Items.Add(tuneIds.Count > 0
                ? Ok("contentMap.dataTuneId", $"Found {tuneIds.Count} DataTuneId values.", ContentMapSchema.RelativePath)
                : Error("contentMap.dataTuneId", "No DataTuneId values were found.", ContentMapSchema.RelativePath));
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            result.Items.Add(Error("contentMap.dataTuneId", ex.Message, ContentMapSchema.RelativePath));
        }
    }

    private static async Task AddSecretScanFindingsAsync(
        PackageValidationResult result,
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var scan = await new SecretAndLocalPathScanService().ScanProjectAsync(projectRoot, cancellationToken);
        foreach (var finding in scan.Findings)
        {
            result.Items.Add(new PackageValidationItem
            {
                Key = $"secretScan.{finding.Key}",
                Severity = string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase) ? "error" : "warning",
                Message = finding.Message,
                RelativePath = finding.RelativePath
            });
        }
    }

    private static string MissingSeverity(
        PackageValidationMode mode,
        string draftSeverity = "warning")
    {
        return mode == PackageValidationMode.Strict ? "error" : draftSeverity;
    }

    private static PackageValidationItem Ok(string key, string message, string? relativePath = null)
    {
        return new PackageValidationItem
        {
            Key = key,
            Severity = "ok",
            Message = message,
            RelativePath = relativePath
        };
    }

    private static PackageValidationItem Error(string key, string message, string? relativePath = null)
    {
        return new PackageValidationItem
        {
            Key = key,
            Severity = "error",
            Message = message,
            RelativePath = relativePath
        };
    }

    private static void FinalizeResult(PackageValidationResult result)
    {
        result.IsOk = result.Items.All(item =>
            !string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase));
    }
}

public enum PackageValidationMode
{
    Draft,
    Strict
}

public sealed class PackageValidationResult
{
    public bool IsOk { get; set; }
    public List<PackageValidationItem> Items { get; set; } = [];
}

public sealed class PackageValidationItem
{
    public string Key { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
}
