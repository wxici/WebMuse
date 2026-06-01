using System.Text.Json;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IContentMapService
{
    Task<ContentMap> CreateDefaultAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default);

    Task<ContentMap> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        ContentMap contentMap,
        CancellationToken cancellationToken = default);
}

public sealed class ContentMapService : IContentMapService
{
    public Task<ContentMap> CreateDefaultAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateProjectRoot(projectRoot);
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new ContentMap
        {
            SchemaVersion = ContentMapSchema.CurrentSchemaVersion,
            ProjectId = projectId.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            Pages =
            [
                new PageMap
                {
                    PageId = "home",
                    Route = "/",
                    Title = "Home",
                    Sections =
                    [
                        new SectionMap
                        {
                            SectionId = "hero",
                            SectionType = "hero",
                            DisplayName = "Hero",
                            DataTuneId = "home.hero",
                            Elements =
                            [
                                new ElementMap
                                {
                                    ElementId = "hero-title",
                                    ElementType = "heading",
                                    DataTuneId = "home.hero.title",
                                    SourceField = "project.projectName",
                                    IsUserEditableInBasicMode = true
                                }
                            ]
                        }
                    ]
                }
            ]
        });
    }

    public async Task<ContentMap> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetContentMapPath(projectRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Content map was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var contentMap = await JsonSerializer.DeserializeAsync<ContentMap>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (contentMap is null)
            {
                throw new InvalidDataException($"Content map is empty or invalid: {path}");
            }

            ValidateSchema(contentMap, path);
            NormalizeAndValidate(contentMap);
            return contentMap;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Content map JSON is invalid: {path}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to read content map: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        ContentMap contentMap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentMap);
        var path = GetContentMapPath(projectRoot);

        try
        {
            NormalizeAndValidate(contentMap);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, contentMap, WrbJsonOptions.Default, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to serialize content map: {path}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save content map: {path}. {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"No permission to save content map: {path}. {ex.Message}", ex);
        }
    }

    public static string GetContentMapPath(string projectRoot)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(
            projectRoot,
            ContentMapSchema.RelativePath,
            "content map");
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
    }

    private static void ValidateProjectRoot(string projectRoot)
    {
        var validation = SandboxPathPolicy.ValidateProjectRoot(projectRoot);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }
    }

    private static void ValidateSchema(ContentMap contentMap, string path)
    {
        if (string.IsNullOrWhiteSpace(contentMap.SchemaVersion))
        {
            throw new InvalidDataException($"Content map has no schemaVersion: {path}");
        }

        if (!string.Equals(
                contentMap.SchemaVersion,
                ContentMapSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Content map schemaVersion '{contentMap.SchemaVersion}' is not supported. Expected '{ContentMapSchema.CurrentSchemaVersion}'. File: {path}");
        }
    }

    private static void NormalizeAndValidate(ContentMap contentMap)
    {
        var now = DateTimeOffset.UtcNow;
        contentMap.SchemaVersion = ContentMapSchema.CurrentSchemaVersion;
        contentMap.ProjectId = contentMap.ProjectId?.Trim() ?? string.Empty;
        contentMap.CreatedAt = contentMap.CreatedAt == default ? now : contentMap.CreatedAt;
        contentMap.UpdatedAt = now;
        contentMap.Pages ??= [];

        var tuneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in contentMap.Pages)
        {
            page.PageId = Required(page.PageId, "page.pageId");
            page.Route = Required(page.Route, $"page[{page.PageId}].route");
            page.Title = page.Title?.Trim() ?? string.Empty;
            page.Sections ??= [];

            foreach (var section in page.Sections)
            {
                section.SectionId = Required(section.SectionId, $"page[{page.PageId}].sectionId");
                section.SectionType = Required(section.SectionType, $"section[{section.SectionId}].sectionType");
                section.DisplayName = section.DisplayName?.Trim() ?? string.Empty;
                section.DataTuneId = RequiredUniqueTuneId(section.DataTuneId, $"section[{section.SectionId}].dataTuneId", tuneIds);
                section.Elements ??= [];

                foreach (var element in section.Elements)
                {
                    element.ElementId = Required(element.ElementId, $"section[{section.SectionId}].elementId");
                    element.ElementType = Required(element.ElementType, $"element[{element.ElementId}].elementType");
                    element.DataTuneId = RequiredUniqueTuneId(element.DataTuneId, $"element[{element.ElementId}].dataTuneId", tuneIds);
                    element.SourceField = element.SourceField?.Trim() ?? string.Empty;
                    element.AssetId = string.IsNullOrWhiteSpace(element.AssetId) ? null : element.AssetId.Trim();
                }
            }
        }
    }

    private static string Required(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Content map requires {fieldName}.");
        }

        return value.Trim();
    }

    private static string RequiredUniqueTuneId(
        string value,
        string fieldName,
        HashSet<string> tuneIds)
    {
        var normalized = Required(value, fieldName);
        if (!tuneIds.Add(normalized))
        {
            throw new InvalidOperationException($"Content map has duplicate DataTuneId '{normalized}' at {fieldName}.");
        }

        return normalized;
    }
}
