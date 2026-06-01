using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IObservationPackageService
{
    Task<ObservationPackageManifest> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        string referenceUrl = "",
        CancellationToken cancellationToken = default);

    Task<ObservationPackageManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        ObservationPackageManifest manifest,
        CancellationToken cancellationToken = default);

    Task<ObservationPackageManifest> AddArtifactAsync(
        string projectRoot,
        ObservationArtifactItem item,
        CancellationToken cancellationToken = default);

    Task<ObservationPackageManifest> AddSectionAsync(
        string projectRoot,
        ObservationSectionItem item,
        CancellationToken cancellationToken = default);

    Task<ObservationPackageManifest> AddInteractionAsync(
        string projectRoot,
        ObservationInteractionItem item,
        CancellationToken cancellationToken = default);

    Task<ObservationPackageManifest> AddFindingAsync(
        string projectRoot,
        ObservationFindingItem item,
        CancellationToken cancellationToken = default);
}

public sealed class ObservationPackageService : IObservationPackageService
{
    private static readonly string[] LegacyObservationArtifacts =
    [
        "observation/observation.md",
        "observation/action-log.json",
        "observation/frame-index.json",
        "observation/screenshots/frame-index.json"
    ];

    public async Task<ObservationPackageManifest> CreateNewAsync(
        string projectRoot,
        string projectId = "",
        string referenceUrl = "",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var identity = await ProjectPackagePathHelpers.TryReadProjectIdentityAsync(projectRoot, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var manifest = new ObservationPackageManifest
        {
            SchemaVersion = ObservationPackageSchema.CurrentSchemaVersion,
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? identity.ProjectId : projectId.Trim(),
            ObservationPackageId = CreatePackageId("observation", now),
            CreatedAt = now,
            UpdatedAt = now,
            ReferenceUrl = string.IsNullOrWhiteSpace(referenceUrl) ? identity.ReferenceUrl : referenceUrl.Trim(),
            CaptureMode = "scaffold"
        };

        AddLegacyArtifacts(projectRoot, manifest);
        if (manifest.Artifacts.Count == 0)
        {
            manifest.Warnings.Add("No legacy observation artifacts were found; created an empty observation package scaffold.");
        }

        return manifest;
    }

    public async Task<ObservationPackageManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetManifestPath(projectRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Observation package manifest was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<ObservationPackageManifest>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (manifest is null)
            {
                throw new InvalidDataException($"Observation package manifest is empty or invalid: {path}");
            }

            ValidateSchema(manifest, path);
            NormalizeAndValidate(projectRoot, manifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Observation package JSON is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        ObservationPackageManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var path = GetManifestPath(projectRoot);
        NormalizeAndValidate(projectRoot, manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default, cancellationToken);
    }

    public async Task<ObservationPackageManifest> AddArtifactAsync(
        string projectRoot,
        ObservationArtifactItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var manifest = await LoadOrCreateAsync(projectRoot, cancellationToken);
        NormalizeArtifact(projectRoot, item);
        Upsert(manifest.Artifacts, item, existing => existing.ArtifactId, item.ArtifactId);
        await SaveAsync(projectRoot, manifest, cancellationToken);
        return manifest;
    }

    public async Task<ObservationPackageManifest> AddSectionAsync(
        string projectRoot,
        ObservationSectionItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var manifest = await LoadOrCreateAsync(projectRoot, cancellationToken);
        NormalizeSection(item);
        Upsert(manifest.Sections, item, existing => existing.SectionId, item.SectionId);
        await SaveAsync(projectRoot, manifest, cancellationToken);
        return manifest;
    }

    public async Task<ObservationPackageManifest> AddInteractionAsync(
        string projectRoot,
        ObservationInteractionItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var manifest = await LoadOrCreateAsync(projectRoot, cancellationToken);
        NormalizeInteraction(item);
        Upsert(manifest.Interactions, item, existing => existing.InteractionId, item.InteractionId);
        await SaveAsync(projectRoot, manifest, cancellationToken);
        return manifest;
    }

    public async Task<ObservationPackageManifest> AddFindingAsync(
        string projectRoot,
        ObservationFindingItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var manifest = await LoadOrCreateAsync(projectRoot, cancellationToken);
        NormalizeFinding(item);
        Upsert(manifest.Findings, item, existing => existing.FindingId, item.FindingId);
        await SaveAsync(projectRoot, manifest, cancellationToken);
        return manifest;
    }

    public static string GetManifestPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ObservationPackageSchema.RelativePath,
            "observation package");
    }

    private async Task<ObservationPackageManifest> LoadOrCreateAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        return File.Exists(GetManifestPath(projectRoot))
            ? await LoadAsync(projectRoot, cancellationToken)
            : await CreateNewAsync(projectRoot, cancellationToken: cancellationToken);
    }

    private static void AddLegacyArtifacts(string projectRoot, ObservationPackageManifest manifest)
    {
        foreach (var relativePath in LegacyObservationArtifacts)
        {
            var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "legacy observation artifact");
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var item = new ObservationArtifactItem
            {
                ArtifactId = $"artifact-{Path.GetFileNameWithoutExtension(relativePath).Replace('.', '-')}",
                Kind = Path.GetExtension(relativePath).Equals(".md", StringComparison.OrdinalIgnoreCase) ? "markdown" : "json",
                RelativePath = relativePath,
                Note = "Imported from an existing observation artifact."
            };
            NormalizeArtifact(projectRoot, item);
            manifest.Artifacts.Add(item);
        }
    }

    private static void ValidateSchema(ObservationPackageManifest manifest, string path)
    {
        if (!string.Equals(
                manifest.SchemaVersion,
                ObservationPackageSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Observation package schemaVersion '{manifest.SchemaVersion}' is not supported. Expected '{ObservationPackageSchema.CurrentSchemaVersion}'. File: {path}");
        }
    }

    private static void NormalizeAndValidate(string projectRoot, ObservationPackageManifest manifest)
    {
        var now = DateTimeOffset.UtcNow;
        manifest.SchemaVersion = ObservationPackageSchema.CurrentSchemaVersion;
        manifest.ProjectId = manifest.ProjectId?.Trim() ?? string.Empty;
        manifest.ObservationPackageId = string.IsNullOrWhiteSpace(manifest.ObservationPackageId)
            ? CreatePackageId("observation", now)
            : manifest.ObservationPackageId.Trim();
        manifest.CreatedAt = manifest.CreatedAt == default ? now : manifest.CreatedAt;
        manifest.UpdatedAt = now;
        manifest.ReferenceUrl = manifest.ReferenceUrl?.Trim() ?? string.Empty;
        manifest.CaptureMode = string.IsNullOrWhiteSpace(manifest.CaptureMode) ? "scaffold" : manifest.CaptureMode.Trim();
        manifest.Artifacts ??= [];
        manifest.Sections ??= [];
        manifest.Interactions ??= [];
        manifest.Findings ??= [];
        manifest.Warnings = NormalizeStringList(manifest.Warnings);

        foreach (var artifact in manifest.Artifacts)
        {
            NormalizeArtifact(projectRoot, artifact);
        }

        foreach (var section in manifest.Sections)
        {
            NormalizeSection(section);
        }

        foreach (var interaction in manifest.Interactions)
        {
            NormalizeInteraction(interaction);
        }

        foreach (var finding in manifest.Findings)
        {
            NormalizeFinding(finding);
        }
    }

    private static void NormalizeArtifact(string projectRoot, ObservationArtifactItem item)
    {
        item.ArtifactId = RequiredOrGenerated(item.ArtifactId, "artifact");
        item.Kind = NormalizeText(item.Kind, "unknown");
        item.RelativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, item.RelativePath, "observation artifact path");
        item.Note = item.Note?.Trim() ?? string.Empty;

        if (ProjectPackagePathHelpers.ContainsSecretOrLocalPath(item.Note))
        {
            throw new InvalidOperationException("Observation artifact note contains a secret marker or local path.");
        }

        var fullPath = Path.Combine(ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot), item.RelativePath);
        if (File.Exists(fullPath))
        {
            var info = new FileInfo(fullPath);
            item.SizeBytes ??= info.Length;
            item.Sha256 ??= ProjectPackagePathHelpers.ComputeSha256(fullPath);
        }
    }

    private static void NormalizeSection(ObservationSectionItem item)
    {
        item.SectionId = RequiredOrGenerated(item.SectionId, "section");
        item.DisplayName = item.DisplayName?.Trim() ?? string.Empty;
        item.SectionType = NormalizeText(item.SectionType, "unknown");
        item.VisualIntent = item.VisualIntent?.Trim() ?? string.Empty;
        item.RelatedArtifactIds = NormalizeStringList(item.RelatedArtifactIds);
    }

    private static void NormalizeInteraction(ObservationInteractionItem item)
    {
        item.InteractionId = RequiredOrGenerated(item.InteractionId, "interaction");
        item.TargetHint = item.TargetHint?.Trim() ?? string.Empty;
        item.Trigger = NormalizeText(item.Trigger, "unknown");
        item.ObservedEffect = item.ObservedEffect?.Trim() ?? string.Empty;
        item.Confidence = NormalizeText(item.Confidence, "unknown");
    }

    private static void NormalizeFinding(ObservationFindingItem item)
    {
        item.FindingId = RequiredOrGenerated(item.FindingId, "finding");
        item.Category = NormalizeText(item.Category, "unknown");
        item.Summary = item.Summary?.Trim() ?? string.Empty;
        item.Detail = item.Detail?.Trim() ?? string.Empty;
        item.Confidence = NormalizeText(item.Confidence, "unknown");
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

    private static void Upsert<T>(
        List<T> items,
        T item,
        Func<T, string> keySelector,
        string key)
    {
        var index = items.FindIndex(existing =>
            string.Equals(keySelector(existing), key, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            items[index] = item;
        }
        else
        {
            items.Add(item);
        }
    }

    private static string CreatePackageId(string prefix, DateTimeOffset now)
    {
        var raw = $"{prefix}-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return raw[..Math.Min(48, raw.Length)];
    }
}
