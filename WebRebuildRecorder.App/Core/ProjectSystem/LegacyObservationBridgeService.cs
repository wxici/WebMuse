using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class LegacyObservationBridgeService
{
    private static readonly string[] DefaultLegacyRelativePaths =
    [
        "observation/observation.md",
        "observation/action-log.json",
        "observation/frame-index.json",
        "observation/screenshots/frame-index.json"
    ];

    private static readonly Regex MarkdownHeadingRegex = new(
        @"^(#{1,6})\s+(.+?)\s*$",
        RegexOptions.Compiled);

    public Task<LegacyObservationBridgeResult> BuildAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        return BuildAsync(projectRoot, DefaultLegacyRelativePaths, cancellationToken);
    }

    public async Task<LegacyObservationBridgeResult> BuildAsync(
        string projectRoot,
        IEnumerable<string> legacyRelativePaths,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var result = new LegacyObservationBridgeResult();
        var observationService = new ObservationPackageService();
        if (!File.Exists(ObservationPackageService.GetManifestPath(root)))
        {
            var emptyManifest = await observationService.CreateNewAsync(root, cancellationToken: cancellationToken);
            await observationService.SaveAsync(root, emptyManifest, cancellationToken);
        }

        foreach (var sourcePath in legacyRelativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = ProjectPackagePathHelpers.NormalizeProjectRelativePath(root, sourcePath, "legacy observation source");
            var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(root, relativePath, "legacy observation source");
            if (!File.Exists(fullPath))
            {
                var warning = $"Legacy observation source was not found: {relativePath}";
                result.Warnings.Add(warning);
                result.Items.Add(new LegacyObservationBridgeItem
                {
                    Key = $"missing.{relativePath}",
                    SourceRelativePath = relativePath,
                    TargetKind = "warning",
                    Status = "warning",
                    Message = warning
                });
                continue;
            }

            if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                await BridgeObservationMarkdownAsync(root, relativePath, fullPath, observationService, result, cancellationToken);
            }
            else if (relativePath.EndsWith("action-log.json", StringComparison.OrdinalIgnoreCase))
            {
                await BridgeActionLogAsync(root, relativePath, fullPath, observationService, result, cancellationToken);
            }
            else if (relativePath.EndsWith("frame-index.json", StringComparison.OrdinalIgnoreCase))
            {
                await BridgeFrameIndexAsync(root, relativePath, fullPath, observationService, result, cancellationToken);
            }
            else
            {
                result.Warnings.Add($"Unsupported legacy observation source was skipped: {relativePath}");
            }
        }

        result.IsOk = result.Items.All(item => !string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase));
        result.UpdatedAt = DateTimeOffset.UtcNow;
        await WriteReportAsync(root, result, cancellationToken);
        return result;
    }

    public static string GetReportPath(string projectRoot)
    {
        return ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            LegacyObservationBridgeSchema.ReportRelativePath,
            "legacy observation bridge report");
    }

    private static async Task BridgeObservationMarkdownAsync(
        string projectRoot,
        string relativePath,
        string fullPath,
        ObservationPackageService observationService,
        LegacyObservationBridgeResult result,
        CancellationToken cancellationToken)
    {
        await observationService.AddArtifactAsync(
            projectRoot,
            new ObservationArtifactItem
            {
                ArtifactId = "legacy-observation-md",
                Kind = "markdown",
                RelativePath = relativePath,
                Note = "Legacy observation markdown registered by bridge."
            },
            cancellationToken);

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var sections = ExtractMarkdownSections(content);
        if (sections.Count == 0)
        {
            var summary = Truncate(FirstMeaningfulText(content), 240);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                await observationService.AddFindingAsync(
                    projectRoot,
                    new ObservationFindingItem
                    {
                        FindingId = "legacy-observation-summary",
                        Category = "legacyMarkdown",
                        Summary = summary,
                        Detail = "Extracted from legacy observation markdown with no heading structure.",
                        Confidence = "low"
                    },
                    cancellationToken);
            }
        }

        var index = 0;
        foreach (var section in sections)
        {
            index++;
            var sectionId = $"legacy-md-section-{index:00}";
            await observationService.AddSectionAsync(
                projectRoot,
                new ObservationSectionItem
                {
                    SectionId = sectionId,
                    DisplayName = section.Title,
                    SectionType = $"markdown-h{section.Level}",
                    VisualIntent = section.Title,
                    RelatedArtifactIds = ["legacy-observation-md"]
                },
                cancellationToken);

            var summary = Truncate(FirstMeaningfulText(section.Body), 240);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                await observationService.AddFindingAsync(
                    projectRoot,
                    new ObservationFindingItem
                    {
                        FindingId = $"legacy-md-finding-{index:00}",
                        Category = "legacyMarkdown",
                        Summary = summary,
                        Detail = $"Extracted under markdown heading '{section.Title}'.",
                        Confidence = "low"
                    },
                    cancellationToken);
            }
        }

        result.Items.Add(new LegacyObservationBridgeItem
        {
            Key = "observationMarkdown",
            SourceRelativePath = relativePath,
            TargetKind = "section/finding",
            Status = "bridged",
            Message = $"Created {sections.Count} section item(s) from legacy markdown."
        });
    }

    private static async Task BridgeActionLogAsync(
        string projectRoot,
        string relativePath,
        string fullPath,
        ObservationPackageService observationService,
        LegacyObservationBridgeResult result,
        CancellationToken cancellationToken)
    {
        await observationService.AddArtifactAsync(
            projectRoot,
            new ObservationArtifactItem
            {
                ArtifactId = "legacy-action-log",
                Kind = "json",
                RelativePath = relativePath,
                Note = "Legacy action log registered by bridge."
            },
            cancellationToken);

        try
        {
            await using var stream = File.OpenRead(fullPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var interactions = ExtractInteractions(document.RootElement).Take(100).ToList();
            var index = 0;
            foreach (var interaction in interactions)
            {
                index++;
                interaction.InteractionId = $"legacy-action-{index:00}";
                await observationService.AddInteractionAsync(projectRoot, interaction, cancellationToken);
            }

            result.Items.Add(new LegacyObservationBridgeItem
            {
                Key = "actionLog",
                SourceRelativePath = relativePath,
                TargetKind = "interaction",
                Status = "bridged",
                Message = $"Created {interactions.Count} interaction item(s) from legacy action log."
            });
        }
        catch (JsonException ex)
        {
            var warning = $"Legacy action log JSON could not be parsed: {relativePath}. {ex.Message}";
            result.Warnings.Add(warning);
            result.Items.Add(new LegacyObservationBridgeItem
            {
                Key = "actionLog.parse",
                SourceRelativePath = relativePath,
                TargetKind = "interaction",
                Status = "warning",
                Message = warning
            });
        }
    }

    private static async Task BridgeFrameIndexAsync(
        string projectRoot,
        string relativePath,
        string fullPath,
        ObservationPackageService observationService,
        LegacyObservationBridgeResult result,
        CancellationToken cancellationToken)
    {
        var artifactId = relativePath.Contains("/screenshots/", StringComparison.OrdinalIgnoreCase)
            ? "legacy-screenshots-frame-index"
            : "legacy-frame-index";
        var note = "Legacy frame index registered by bridge.";
        var frameCount = 0;
        try
        {
            await using var stream = File.OpenRead(fullPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            frameCount = CountFrameEntries(document.RootElement);
            if (frameCount > 0)
            {
                note = $"Legacy frame index registered by bridge; parsed {frameCount} frame entry item(s).";
            }
        }
        catch (JsonException ex)
        {
            var warning = $"Legacy frame index JSON could not be parsed: {relativePath}. {ex.Message}";
            result.Warnings.Add(warning);
        }

        await observationService.AddArtifactAsync(
            projectRoot,
            new ObservationArtifactItem
            {
                ArtifactId = artifactId,
                Kind = "json",
                RelativePath = relativePath,
                Note = note
            },
            cancellationToken);

        if (frameCount > 0)
        {
            await observationService.AddFindingAsync(
                projectRoot,
                new ObservationFindingItem
                {
                    FindingId = $"{artifactId}-summary",
                    Category = "frameIndex",
                    Summary = $"Frame index contains {frameCount} frame entry item(s).",
                    Detail = "Extracted from legacy frame-index JSON metadata only; no image content was analyzed.",
                    Confidence = "medium"
                },
                cancellationToken);
        }

        result.Items.Add(new LegacyObservationBridgeItem
        {
            Key = artifactId,
            SourceRelativePath = relativePath,
            TargetKind = "artifact/finding",
            Status = "bridged",
            Message = frameCount > 0
                ? $"Registered frame index and summarized {frameCount} frame entry item(s)."
                : "Registered frame index artifact."
        });
    }

    private static async Task WriteReportAsync(
        string projectRoot,
        LegacyObservationBridgeResult result,
        CancellationToken cancellationToken)
    {
        var reportPath = GetReportPath(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await using var stream = File.Create(reportPath);
        await JsonSerializer.SerializeAsync(stream, result, WrbJsonOptions.Default, cancellationToken);
    }

    private static List<MarkdownSection> ExtractMarkdownSections(string content)
    {
        var sections = new List<MarkdownSection>();
        MarkdownSection? current = null;
        foreach (var rawLine in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var match = MarkdownHeadingRegex.Match(rawLine);
            if (match.Success)
            {
                if (current is not null)
                {
                    sections.Add(current);
                }

                current = new MarkdownSection(match.Groups[2].Value.Trim(), match.Groups[1].Value.Length);
                continue;
            }

            current?.BodyLines.Add(rawLine);
        }

        if (current is not null)
        {
            sections.Add(current);
        }

        return sections;
    }

    private static IEnumerable<ObservationInteractionItem> ExtractInteractions(JsonElement element)
    {
        foreach (var entry in EnumerateObjects(element))
        {
            var triggerText = FirstString(entry, "trigger", "type", "action", "event", "kind", "name", "message", "note", "description");
            var trigger = DetectTrigger(triggerText);
            if (string.IsNullOrWhiteSpace(trigger))
            {
                continue;
            }

            yield return new ObservationInteractionItem
            {
                Trigger = trigger,
                TargetHint = FirstString(entry, "targetHint", "target", "selector", "element", "text", "path", "url"),
                ObservedEffect = FirstString(entry, "observedEffect", "effect", "note", "message", "description", "value"),
                Confidence = "low"
            };
        }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in EnumerateObjects(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in EnumerateObjects(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static int CountFrameEntries(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.GetArrayLength();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in new[] { "frames", "items", "screenshots", "captures" })
        {
            if (element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                return array.GetArrayLength();
            }
        }

        return EnumerateObjects(element).Count(entry =>
            !string.IsNullOrWhiteSpace(FirstString(entry, "relativePath", "path", "file", "filePath", "imagePath")));
    }

    private static string FirstString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var name in names)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString()?.Trim() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static string DetectTrigger(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.Contains("scroll", StringComparison.OrdinalIgnoreCase))
        {
            return "scroll";
        }

        if (normalized.Contains("click", StringComparison.OrdinalIgnoreCase))
        {
            return "click";
        }

        if (normalized.Contains("hover", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("mouseover", StringComparison.OrdinalIgnoreCase))
        {
            return "hover";
        }

        if (normalized.Contains("key", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("keyboard", StringComparison.OrdinalIgnoreCase))
        {
            return "key";
        }

        if (normalized.Contains("note", StringComparison.OrdinalIgnoreCase))
        {
            return "note";
        }

        return string.Empty;
    }

    private static string FirstMeaningfulText(string content)
    {
        var lines = content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !MarkdownHeadingRegex.IsMatch(line));
        return string.Join(" ", lines);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";
    }

    private sealed class MarkdownSection(string title, int level)
    {
        public string Title { get; } = title;
        public int Level { get; } = level;
        public List<string> BodyLines { get; } = [];
        public string Body => string.Join(Environment.NewLine, BodyLines);
    }
}
