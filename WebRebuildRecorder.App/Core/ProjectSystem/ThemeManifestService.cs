using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public interface IThemeManifestService
{
    Task<ThemeManifest> CreateDefaultAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default);

    Task<ThemeManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string projectRoot,
        ThemeManifest manifest,
        CancellationToken cancellationToken = default);
}

public sealed class ThemeManifestService : IThemeManifestService
{
    private static readonly Regex HexColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public Task<ThemeManifest> CreateDefaultAsync(
        string projectRoot,
        string projectId = "",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateProjectRoot(projectRoot);
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new ThemeManifest
        {
            SchemaVersion = ThemeManifestSchema.CurrentSchemaVersion,
            ProjectId = projectId.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CurrentPalette = new ColorPalette
            {
                PaletteId = "default-neutral",
                Name = "Default neutral",
                Source = "systemDefault",
                Colors =
                [
                    new ThemeColor { Role = "background", Hex = "#FFFFFF", Note = "Default page background." },
                    new ThemeColor { Role = "text", Hex = "#111111", Note = "Default body text." },
                    new ThemeColor { Role = "muted", Hex = "#666666", Note = "Default secondary text." },
                    new ThemeColor { Role = "accent", Hex = "#2563EB", Note = "Default accent." }
                ]
            },
            Typography = new TypographyScale
            {
                DisplayFont = "system-ui",
                BodyFont = "system-ui",
                BaseFontSize = 16,
                HeadingScale = 1.25
            },
            Spacing = new SpacingScale(),
            VisualTone = new VisualTone { Mood = "neutral" },
            Notes = "Default scaffold theme; no UI selection has been performed."
        });
    }

    public async Task<ThemeManifest> LoadAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var path = GetManifestPath(projectRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Theme manifest was not found: {path}", path);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<ThemeManifest>(
                stream,
                WrbJsonOptions.Default,
                cancellationToken);

            if (manifest is null)
            {
                throw new InvalidDataException($"Theme manifest is empty or invalid: {path}");
            }

            ValidateSchema(manifest, path);
            NormalizeAndValidate(manifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Theme manifest JSON is invalid: {path}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to read theme manifest: {path}. {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(
        string projectRoot,
        ThemeManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var path = GetManifestPath(projectRoot);

        try
        {
            NormalizeAndValidate(manifest);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, manifest, WrbJsonOptions.Default, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to serialize theme manifest: {path}. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to save theme manifest: {path}. {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"No permission to save theme manifest: {path}. {ex.Message}", ex);
        }
    }

    public static string GetManifestPath(string projectRoot)
    {
        return ValidateRelativeFilePath(projectRoot, ThemeManifestSchema.RelativePath, "theme manifest");
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

    private static string ValidateRelativeFilePath(string projectRoot, string relativePath, string fieldName)
    {
        var validation = SandboxPathPolicy.ValidateManifestRelativePath(projectRoot, relativePath, fieldName);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.NormalizedTargetPath;
    }

    private static void ValidateSchema(ThemeManifest manifest, string path)
    {
        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            throw new InvalidDataException($"Theme manifest has no schemaVersion: {path}");
        }

        if (!string.Equals(
                manifest.SchemaVersion,
                ThemeManifestSchema.CurrentSchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Theme manifest schemaVersion '{manifest.SchemaVersion}' is not supported. Expected '{ThemeManifestSchema.CurrentSchemaVersion}'. File: {path}");
        }
    }

    private static void NormalizeAndValidate(ThemeManifest manifest)
    {
        var now = DateTimeOffset.UtcNow;
        manifest.SchemaVersion = ThemeManifestSchema.CurrentSchemaVersion;
        manifest.ProjectId = manifest.ProjectId?.Trim() ?? string.Empty;
        manifest.CreatedAt = manifest.CreatedAt == default ? now : manifest.CreatedAt;
        manifest.UpdatedAt = now;
        manifest.CurrentPalette ??= new ColorPalette();
        manifest.CandidatePalettes ??= [];
        manifest.Typography ??= new TypographyScale();
        manifest.Spacing ??= new SpacingScale();
        manifest.VisualTone ??= new VisualTone();
        manifest.Notes = manifest.Notes?.Trim() ?? string.Empty;

        NormalizePalette(manifest.CurrentPalette, nameof(manifest.CurrentPalette));
        foreach (var palette in manifest.CandidatePalettes)
        {
            NormalizePalette(palette, nameof(manifest.CandidatePalettes));
        }
    }

    private static void NormalizePalette(ColorPalette palette, string fieldName)
    {
        palette.PaletteId = palette.PaletteId?.Trim() ?? string.Empty;
        palette.Name = palette.Name?.Trim() ?? string.Empty;
        palette.Source = palette.Source?.Trim() ?? string.Empty;
        palette.Colors ??= [];
        foreach (var color in palette.Colors)
        {
            color.Role = color.Role?.Trim() ?? string.Empty;
            color.Hex = color.Hex?.Trim().ToUpperInvariant() ?? string.Empty;
            color.Note = color.Note?.Trim() ?? string.Empty;
            if (!HexColorRegex.IsMatch(color.Hex))
            {
                throw new InvalidOperationException(
                    $"Theme color '{fieldName}.{color.Role}' has invalid hex value '{color.Hex}'. Expected #RRGGBB.");
            }
        }
    }
}
