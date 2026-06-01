using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IConstructionPackageService
{
    Task<ConstructionPackageManifest> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        string brandName = "",
        string goalSummary = "",
        CancellationToken cancellationToken = default);

    Task<ConstructionPackageManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        ConstructionPackageManifest manifest,
        CancellationToken cancellationToken = default);
}

public sealed class ConstructionPackageService : IConstructionPackageService
{
    public static readonly IReadOnlyList<string> RequiredProjectFiles =
    [
        WrbProjectSchema.FileName,
        ObservationPackageSchema.RelativePath,
        AssetsManifestSchema.RelativePath,
        ThemeManifestSchema.RelativePath,
        ContentMapSchema.RelativePath
    ];

    public async Task<ConstructionPackageManifest> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        string brandName = "",
        string goalSummary = "",
        CancellationToken cancellationToken = default)
    {
        ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(projectRoot, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var manifest = new ConstructionPackageManifest
        {
            SchemaVersion = ConstructionPackageSchema.CurrentSchemaVersion,
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? identity.ProjectId : projectId.Trim(),
            ConstructionPackageId = CreatePackageId("construction", now),
            CreatedAt = now,
            UpdatedAt = now,
            ReferenceUrl = identity.ReferenceUrl,
            BrandName = brandName.Trim(),
            GoalSummary = string.IsNullOrWhiteSpace(goalSummary)
                ? "Scaffold a static branded website reconstruction package from project manifests."
                : goalSummary.Trim(),
            RequiredProjectFiles = RequiredProjectFiles.ToList(),
            Inputs = CreateDefaultInputs(),
            Constraints = CreateDefaultConstraints(),
            Deliverables = CreateDefaultDeliverables()
        };

        AddMissingRequiredFileWarnings(projectRoot, manifest);
        return manifest;
    }

    public async Task<ConstructionPackageManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetManifestPath(projectRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Construction package manifest was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<ConstructionPackageManifest>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (manifest is null)
            {
                throw new InvalidDataException($"Construction package manifest is empty or invalid: {path}");
            }

            ValidateSchema(manifest, path);
            NormalizeAndValidate(projectRoot, manifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Construction package JSON is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        ConstructionPackageManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var path = GetManifestPath(projectRoot);
        NormalizeAndValidate(projectRoot, manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default, cancellationToken);
    }

    public static string GetManifestPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionPackageSchema.RelativePath,
            "construction package");
    }

    private static List<ConstructionInputItem> CreateDefaultInputs()
    {
        return RequiredProjectFiles
            .Select((path, index) => new ConstructionInputItem
            {
                InputId = $"input-{index + 1:00}",
                Kind = Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "project",
                RelativePath = path,
                Description = $"Required project input: {path}",
                Required = true
            })
            .ToList();
    }

    private static List<ConstructionConstraintItem> CreateDefaultConstraints()
    {
        return
        [
            new()
            {
                ConstraintId = "constraint-static-site",
                Category = "scope",
                Rule = "Generate only static single-page or static multi-section branded website output.",
                Severity = "required"
            },
            new()
            {
                ConstraintId = "constraint-no-copying-reference",
                Category = "copyright",
                Rule = "Do not copy reference-site proprietary assets, logos, copy, or distinctive graphics by default.",
                Severity = "required"
            },
            new()
            {
                ConstraintId = "constraint-sandbox",
                Category = "sandbox",
                Rule = "Write only inside codex-workspace/ and output-site/current/ when future execution is enabled.",
                Severity = "required"
            }
        ];
    }

    private static List<ConstructionDeliverableItem> CreateDefaultDeliverables()
    {
        return
        [
            new()
            {
                DeliverableId = "deliverable-output-site",
                Kind = "static-site",
                TargetRelativePath = $"{ProjectDirectoryV2.OutputCurrent}/",
                Description = "Generated static website output root."
            },
            new()
            {
                DeliverableId = "deliverable-index",
                Kind = "html",
                TargetRelativePath = $"{ProjectDirectoryV2.OutputCurrent}/index.html",
                Description = "Primary static website entry point."
            }
        ];
    }

    private static void AddMissingRequiredFileWarnings(string projectRoot, ConstructionPackageManifest manifest)
    {
        foreach (var relativePath in RequiredProjectFiles)
        {
            var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "construction required file");
            if (!File.Exists(fullPath))
            {
                manifest.Warnings.Add($"Required project file is missing at scaffold time: {relativePath}");
            }
        }
    }

    private static void ValidateSchema(ConstructionPackageManifest manifest, string path)
    {
        if (!string.Equals(
                manifest.SchemaVersion,
                ConstructionPackageSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Construction package schemaVersion '{manifest.SchemaVersion}' is not supported. Expected '{ConstructionPackageSchema.CurrentSchemaVersion}'. File: {path}");
        }
    }

    private static void NormalizeAndValidate(string projectRoot, ConstructionPackageManifest manifest)
    {
        var now = DateTimeOffset.UtcNow;
        manifest.SchemaVersion = ConstructionPackageSchema.CurrentSchemaVersion;
        manifest.ProjectId = manifest.ProjectId?.Trim() ?? string.Empty;
        manifest.ConstructionPackageId = string.IsNullOrWhiteSpace(manifest.ConstructionPackageId)
            ? CreatePackageId("construction", now)
            : manifest.ConstructionPackageId.Trim();
        manifest.CreatedAt = manifest.CreatedAt == default ? now : manifest.CreatedAt;
        manifest.UpdatedAt = now;
        manifest.ReferenceUrl = manifest.ReferenceUrl?.Trim() ?? string.Empty;
        manifest.BrandName = manifest.BrandName?.Trim() ?? string.Empty;
        manifest.GoalSummary = manifest.GoalSummary?.Trim() ?? string.Empty;
        manifest.Inputs ??= [];
        manifest.Constraints ??= [];
        manifest.Deliverables ??= [];
        manifest.RequiredProjectFiles = NormalizeRequiredFiles(projectRoot, manifest.RequiredProjectFiles);
        manifest.Warnings = NormalizeStringList(manifest.Warnings);

        foreach (var input in manifest.Inputs)
        {
            input.InputId = RequiredOrGenerated(input.InputId, "input");
            input.Kind = NormalizeText(input.Kind, "unknown");
            input.RelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, input.RelativePath, "construction input path");
            input.Description = input.Description?.Trim() ?? string.Empty;
        }

        foreach (var constraint in manifest.Constraints)
        {
            constraint.ConstraintId = RequiredOrGenerated(constraint.ConstraintId, "constraint");
            constraint.Category = NormalizeText(constraint.Category, "unknown");
            constraint.Rule = constraint.Rule?.Trim() ?? string.Empty;
            constraint.Severity = NormalizeText(constraint.Severity, "required");
        }

        foreach (var deliverable in manifest.Deliverables)
        {
            deliverable.DeliverableId = RequiredOrGenerated(deliverable.DeliverableId, "deliverable");
            deliverable.Kind = NormalizeText(deliverable.Kind, "unknown");
            deliverable.TargetRelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, deliverable.TargetRelativePath, "construction deliverable path");
            deliverable.Description = deliverable.Description?.Trim() ?? string.Empty;
        }
    }

    private static List<string> NormalizeRequiredFiles(string projectRoot, List<string>? paths)
    {
        var normalized = paths is { Count: > 0 }
            ? paths
            : RequiredProjectFiles.ToList();

        return normalized
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, path, "required project file"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RequiredOrGenerated(string value, string prefix)
    {
        return string.IsNullOrWhiteSpace(value) ? $"{prefix}-{Guid.NewGuid():N}" : value.Trim();
    }

    private static string NormalizeText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static List<string> NormalizeStringList(List<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string CreatePackageId(string prefix, DateTimeOffset now)
    {
        var raw = $"{prefix}-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(48, raw.Length)];
    }
}
