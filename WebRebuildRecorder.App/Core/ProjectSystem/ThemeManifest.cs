namespace WebRebuildRecorder.App.Core.ProjectSystem;

public static class ThemeManifestSchema
{
    public const string CurrentSchemaVersion = "0.1.0-p1-data";
    public const string RelativePath = $"{ProjectDirectoryV2.Theme}/theme.json";
}

public sealed class ThemeManifest
{
    public string SchemaVersion { get; set; } = ThemeManifestSchema.CurrentSchemaVersion;
    public string ProjectId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ColorPalette CurrentPalette { get; set; } = new();
    public List<ColorPalette> CandidatePalettes { get; set; } = [];
    public TypographyScale Typography { get; set; } = new();
    public SpacingScale Spacing { get; set; } = new();
    public VisualTone VisualTone { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

public sealed class ColorPalette
{
    public string PaletteId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public List<ThemeColor> Colors { get; set; } = [];
}

public sealed class ThemeColor
{
    public string Role { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed class TypographyScale
{
    public string DisplayFont { get; set; } = string.Empty;
    public string BodyFont { get; set; } = string.Empty;
    public double BaseFontSize { get; set; } = 16;
    public double HeadingScale { get; set; } = 1.25;
}

public sealed class SpacingScale
{
    public double SectionGap { get; set; } = 120;
    public double ContentMaxWidth { get; set; } = 1200;
    public double BorderRadius { get; set; } = 16;
}

public sealed class VisualTone
{
    public double Brightness { get; set; } = 1.0;
    public double Contrast { get; set; } = 1.0;
    public double Saturation { get; set; } = 1.0;
    public string Mood { get; set; } = string.Empty;
}
