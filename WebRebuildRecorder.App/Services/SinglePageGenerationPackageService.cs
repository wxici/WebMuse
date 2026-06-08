using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Core.ProjectSystem;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class SinglePageGenerationPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;

    private static readonly IReadOnlyList<string> RequiredEvidenceFiles =
    [
        "analysis/ai-reconstruction-brief.md",
        "analysis/reconstruction-evidence-graph.json",
        "analysis/section-map.json",
        "analysis/media-placement-map.json",
        "analysis/behavior-map.json",
        "analysis/css-rule-map.json",
        "analysis/js-behavior-reference-map.json",
        "rendered/first-screen.png"
    ];

    private static readonly IReadOnlyList<string> PackageFileNames =
    [
        SinglePageGenerationPackageSchema.ManifestFileName,
        SinglePageGenerationPackageSchema.ConstructionBriefFileName,
        SinglePageGenerationPackageSchema.PromptForCodexFileName,
        SinglePageGenerationPackageSchema.OutputContractFileName,
        SinglePageGenerationPackageSchema.ForbiddenContentFileName,
        SinglePageGenerationPackageSchema.AssetSlotPlanFileName,
        SinglePageGenerationPackageSchema.EvidenceIndexFileName,
        SinglePageGenerationPackageSchema.ReviewChecklistFileName
    ];

    private static readonly IReadOnlyList<string> FilteredOutItems =
    [
        "analytics scripts",
        "tracking scripts",
        "Cloudflare beacon",
        "Mindbox tracker",
        "recaptcha",
        "cookie consent UI",
        "hidden modal forms",
        "favourites system",
        "office backend forms",
        "server-side endpoints",
        "original logo assets as final delivery assets",
        "original images as final delivery assets",
        "original videos as final delivery assets",
        "original fonts as final delivery assets"
    ];

    private readonly AppLogger _logger;

    public SinglePageGenerationPackageService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<SinglePageGenerationPackage> CreateAsync(
        RebuildProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
        {
            throw new InvalidOperationException("Project directory is missing.");
        }

        var sourceSnapshotRoot = Path.Combine(project.ProjectDirectory, ProjectDirectoryV2.SourceSnapshot);
        var missingEvidence = RequiredEvidenceFiles
            .Where(relativePath => !File.Exists(Path.Combine(sourceSnapshotRoot, NormalizeSeparators(relativePath))))
            .ToList();

        if (missingEvidence.Count != 0)
        {
            throw new InvalidOperationException(
                "Source Snapshot reconstruction evidence is incomplete. Missing: "
                + string.Join(", ", missingEvidence));
        }

        var evidence = await LoadEvidenceAsync(sourceSnapshotRoot, cancellationToken);
        var packageId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var packageRoot = GetUniquePackageRoot(project.ProjectDirectory, packageId, out packageId);
        Directory.CreateDirectory(packageRoot);

        var package = new SinglePageGenerationPackage
        {
            PackageId = packageId,
            CreatedAt = DateTime.Now,
            ProjectId = project.ProjectId,
            ProjectName = project.ProjectName,
            ReferenceUrl = project.ReferenceUrl,
            SourceSnapshotRoot = ProjectDirectoryV2.SourceSnapshot,
            PackageRoot = packageRoot,
            InputEvidenceFiles = RequiredEvidenceFiles.Select(path => $"{ProjectDirectoryV2.SourceSnapshot}/{path}").ToList(),
            OutputFiles = PackageFileNames
                .Select(fileName => $"{SinglePageGenerationPackageSchema.RootRelativePath}/{packageId}/{fileName}")
                .ToList(),
            FilteredOutItems = FilteredOutItems.ToList(),
            IsReadyForCodexCli = true
        };

        package.Warnings.Add("This package is preparation only; it does not execute Codex CLI.");
        package.Warnings.Add("Original brand media assets must be replaced with user-owned or generated placeholders in later construction.");

        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.ConstructionBriefFileName),
            BuildConstructionBrief(project, evidence),
            cancellationToken);
        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.PromptForCodexFileName),
            BuildPromptForCodex(project, evidence),
            cancellationToken);
        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.OutputContractFileName),
            BuildOutputContract(),
            cancellationToken);
        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.ForbiddenContentFileName),
            BuildForbiddenContent(),
            cancellationToken);
        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.AssetSlotPlanFileName),
            BuildAssetSlotPlan(evidence),
            cancellationToken);
        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.EvidenceIndexFileName),
            BuildEvidenceIndex(package),
            cancellationToken);
        await WriteTextAsync(
            Path.Combine(packageRoot, SinglePageGenerationPackageSchema.ReviewChecklistFileName),
            BuildReviewChecklist(),
            cancellationToken);

        await using (var stream = File.Create(Path.Combine(packageRoot, SinglePageGenerationPackageSchema.ManifestFileName)))
        {
            await JsonSerializer.SerializeAsync(stream, package, JsonOptions, cancellationToken);
        }

        _logger.Info($"Single page generation package created: {packageRoot}");
        return package;
    }

    public async Task<SinglePageGenerationPackage?> LoadLatestAsync(
        RebuildProject project,
        CancellationToken cancellationToken = default)
    {
        var latestPath = await GetLatestPackagePathAsync(project, cancellationToken);
        if (string.IsNullOrWhiteSpace(latestPath))
        {
            return null;
        }

        var manifestPath = Path.Combine(latestPath, SinglePageGenerationPackageSchema.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<SinglePageGenerationPackage>(stream, JsonOptions, cancellationToken);
    }

    public Task<string?> GetLatestPackagePathAsync(
        RebuildProject project,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.Combine(
            project.ProjectDirectory,
            SinglePageGenerationPackageSchema.RootRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(root))
        {
            return Task.FromResult<string?>(null);
        }

        var latest = Directory
            .EnumerateDirectories(root)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return Task.FromResult(latest);
    }

    private static async Task<SinglePageGenerationEvidence> LoadEvidenceAsync(
        string sourceSnapshotRoot,
        CancellationToken cancellationToken)
    {
        var evidence = new SinglePageGenerationEvidence
        {
            AiBrief = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "ai-reconstruction-brief.md"),
                cancellationToken),
            ReconstructionGraphJson = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "reconstruction-evidence-graph.json"),
                cancellationToken),
            SectionMapJson = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "section-map.json"),
                cancellationToken),
            MediaPlacementMapJson = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "media-placement-map.json"),
                cancellationToken),
            BehaviorMapJson = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "behavior-map.json"),
                cancellationToken),
            CssRuleMapJson = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "css-rule-map.json"),
                cancellationToken),
            JsBehaviorReferenceMapJson = await File.ReadAllTextAsync(
                Path.Combine(sourceSnapshotRoot, "analysis", "js-behavior-reference-map.json"),
                cancellationToken)
        };

        evidence.SectionHeadings = ExtractStrings(evidence.SectionMapJson, ["heading", "visualRole", "role"], 14);
        evidence.MediaSlots = ExtractMediaSlots(evidence.MediaPlacementMapJson);
        evidence.BehaviorSignals = ExtractStrings(evidence.BehaviorMapJson, ["trigger", "plugin", "pattern", "animationName"], 18);
        evidence.CssSignals = ExtractStrings(evidence.CssRuleMapJson, ["selector", "sourceCssFile", "rebuildSuggestion"], 16);
        evidence.JsSignals = ExtractStrings(evidence.JsBehaviorReferenceMapJson, ["sourceJsFile", "notes"], 10);
        evidence.BriefHighlights = ExtractBriefHighlights(evidence.AiBrief);
        return evidence;
    }

    private static string BuildConstructionBrief(
        RebuildProject project,
        SinglePageGenerationEvidence evidence)
    {
        var sections = evidence.SectionHeadings.Count == 0
            ? "- Hero / project intro\n- Premium business-center positioning\n- Location and office-selection CTA\n- Supporting cards/sections from evidence"
            : string.Join(Environment.NewLine, evidence.SectionHeadings.Select(item => $"- {item}"));

        var behaviorSignals = evidence.BehaviorSignals.Count == 0
            ? "- Rebuild simple reveal, parallax, sticky, and contentAnimation intent with new static JavaScript."
            : string.Join(Environment.NewLine, evidence.BehaviorSignals.Select(item => $"- {item}"));

        var cssSignals = evidence.CssSignals.Count == 0
            ? "- Use a small static CSS architecture: reset, layout, hero, sections, cards, utilities, responsive rules."
            : string.Join(Environment.NewLine, evidence.CssSignals.Select(item => $"- {item}"));

        return $"""
# Single Page Construction Brief

## Goal

Build a static, desktop-first responsive single-page reconstruction inspired by `{project.ReferenceUrl}` using the P2A-1.2 Source Snapshot reconstruction evidence. The result must be a new implementation, not a copy of original source assets or backend behavior.

The target page type is a full-screen premium commercial real-estate first screen with a high-end business center narrative. Preserve the dynamic video feeling through gradients, slow zoom, simulated video background, or a user-provided video/image slot. Do not reuse the original Vimeo video, original images, original logo files, original fonts, trackers, backend forms, cookie banner, modal systems, or favourite logic.

## Source Evidence Summary

Evidence consumed:

- `source-snapshot/analysis/ai-reconstruction-brief.md`
- `source-snapshot/analysis/reconstruction-evidence-graph.json`
- `source-snapshot/analysis/section-map.json`
- `source-snapshot/analysis/media-placement-map.json`
- `source-snapshot/analysis/behavior-map.json`
- `source-snapshot/analysis/css-rule-map.json`
- `source-snapshot/analysis/js-behavior-reference-map.json`
- `source-snapshot/rendered/first-screen.png`

Key evidence highlights:

{FormatBullets(evidence.BriefHighlights)}

## Page Narrative

Open with a quiet, high-contrast, premium AIR/business-center hero: oversized typography, restrained black/white/gray palette, translucent controls, and a strong spatial background. Continue into concise sections that communicate architecture, class A offices, location, office selection, and investment/developer credibility.

## Required Sections

{sections}

## Hero Plan

- Full viewport hero.
- Large AIR-style letterforms or equivalent text treatment using new text, not copied logo assets.
- Simulated motion background or user asset slot replacing original Vimeo/image assets.
- Minimal top navigation and a primary office-selection CTA.
- Cookie consent, favourite buttons, tracking widgets, and backend form behavior are excluded.

## Media Replacement Plan

- Replace original Vimeo background with `hero-media-slot`.
- Replace original pictures and slider images with user-owned or generated placeholder assets.
- Use CSS gradients, masks, slow transforms, opacity layers, and neutral geometry when no user asset is provided.
- Do not embed original image, video, font, or logo binaries.

## Animation Plan

{behaviorSignals}

Recreate sticky / parallax / reveal / contentAnimation intent with small static JavaScript and CSS transitions. Keep effects graceful when JavaScript is disabled.

## CSS Architecture Plan

{cssSignals}

Use one static `assets/styles.css` file with clear sections: variables, base, layout, hero, cards/sections, motion, responsive, and utility rules.

## JavaScript Behavior Plan

{FormatBullets(evidence.JsSignals, "- Use small viewport/reveal/sticky helpers only.")}

No remote analytics, Cloudflare beacon, Mindbox tracker, recaptcha, backend form submit, cookie banner, favourite system, or paid external scripts.

## Asset Slot Plan

Use `ASSET_SLOT_PLAN.md`. Required slots include hero video/image slot, section background image slots, slider/card image slots, logo/textmark slot, and decorative map/geometry slots.

## Content Plan

Use concise English/Russian-neutral placeholder copy unless user supplies final copy. Keep section structure and hierarchy from evidence, but rewrite text as new content.

## Explicitly Ignore

{FormatBullets(FilteredOutItems)}

## Output Requirements

- Write `output-site/current/index.html`.
- Write `output-site/current/assets/styles.css`.
- Write `output-site/current/assets/app.js`.
- Write `output-site/current/site-manifest.json`.
- Also write `output-site/current/README_PREVIEW.md`.
- Static only.
- No backend.
- No package restore or build step.
- Works by opening `index.html` directly.
- Desktop-first responsive behavior.

## Acceptance Criteria

- Hero has a dynamic video-like feeling without original Vimeo/video/image assets.
- Required sections are present and readable.
- Sticky/parallax/reveal/contentAnimation intent exists at MVP quality.
- No original source assets are reused as final delivery assets.
- No tracker/backend/cookie/favourite logic exists.
- The page opens directly from `output-site/current/index.html`.
- Mobile layout is usable.
""";
    }

    private static string BuildPromptForCodex(
        RebuildProject project,
        SinglePageGenerationEvidence evidence)
    {
        return $"""
# Prompt For Codex

You are implementing a static single-page site from a local construction package. Use the evidence files and `CONSTRUCTION_BRIEF.md` as the source of truth.

Target reference URL for evidence context: `{project.ReferenceUrl}`.

Create these files exactly:

```text
output-site/current/index.html
output-site/current/assets/styles.css
output-site/current/assets/app.js
output-site/current/site-manifest.json
output-site/current/README_PREVIEW.md
```

Constraints:

- static only
- no backend
- no external tracking
- no original source assets
- no build step
- works by opening `index.html`
- desktop-first responsive
- no package restore
- no remote analytics
- no external paid scripts
- no server endpoint dependency

Use new placeholder/media-slot implementations for all original images, videos, logos, and fonts. Preserve the design intent: premium commercial real estate, large hero typography, black/white/gray restraint, translucent controls/cards, and slow motion-style visual depth.

Use these evidence highlights:

{FormatBullets(evidence.BriefHighlights)}

Before finishing, verify that no `Cloudflare`, `Mindbox`, `recaptcha`, cookie consent UI, favourite system, original asset URL, or backend form submission remains in the generated output.
""";
    }

    private static string BuildOutputContract()
    {
        return """
# Output Contract

## Required Files

```text
index.html
assets/styles.css
assets/app.js
site-manifest.json
README_PREVIEW.md
```

## Output Root

All files must be written under:

```text
output-site/current/
```

## Runtime Requirements

- Static HTML/CSS/JS only.
- Works by opening `index.html` directly.
- No package restore.
- No `node_modules`.
- No build step.
- No server endpoint dependency.

## Forbidden Output

- `node_modules`
- package restore
- remote analytics
- external paid scripts
- server endpoint dependency
- original source images/videos/fonts/logos as final delivery assets
- cookie consent UI
- recaptcha
- backend form submission
- favourite/account system
""";
    }

    private static string BuildForbiddenContent()
    {
        return $"""
# Forbidden Content

These items may appear in evidence, but must not be rebuilt as final page behavior or final delivery assets:

{FormatBullets(FilteredOutItems)}

## Implementation Rule

Use replacement slots, generated placeholders, CSS geometry, or user-provided assets instead. Do not preserve tracker, analytics, recaptcha, cookie consent, backend form, favourite, office backend, original logo, original image, original video, or original font implementations.
""";
    }

    private static string BuildAssetSlotPlan(SinglePageGenerationEvidence evidence)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Asset Slot Plan");
        builder.AppendLine();
        builder.AppendLine("All slots must use user-owned, generated, or placeholder assets. Original site binaries are evidence only.");
        builder.AppendLine();

        var slots = evidence.MediaSlots.Count == 0
            ? BuildFallbackSlots()
            : evidence.MediaSlots.Take(18).ToList();

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            builder.AppendLine($"## {slot.SlotId}");
            builder.AppendLine();
            builder.AppendLine($"- role: {slot.Role}");
            builder.AppendLine($"- recommended user asset type: {slot.RecommendedAssetType}");
            builder.AppendLine($"- desktop ratio: {slot.DesktopRatio}");
            builder.AppendLine($"- mobile ratio: {slot.MobileRatio}");
            builder.AppendLine($"- fallback behavior: {slot.FallbackBehavior}");
            if (!string.IsNullOrWhiteSpace(slot.SourceHint))
            {
                builder.AppendLine($"- evidence hint: {slot.SourceHint}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildEvidenceIndex(SinglePageGenerationPackage package)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Evidence Index");
        builder.AppendLine();
        builder.AppendLine("This package references P2A-1.2 Source Snapshot evidence. Evidence files stay in the project runtime directory and are not copied as final website assets.");
        builder.AppendLine();

        foreach (var file in package.InputEvidenceFiles)
        {
            builder.AppendLine($"- `{file}` - {DescribeEvidenceFile(file)}");
        }

        return builder.ToString();
    }

    private static string BuildReviewChecklist()
    {
        return """
# Review Checklist

- Does the hero have a dynamic video-like feeling?
- Are the required sections present and readable?
- Are sticky/parallax/reveal effects present at MVP quality?
- Is contentAnimation intent represented with new CSS/JS?
- Are original source assets absent from final output?
- Are tracker/backend/cookie/favourite systems absent?
- Is recaptcha absent?
- Is Cloudflare beacon absent?
- Is Mindbox tracker absent?
- Can the page be opened directly from `index.html`?
- Is the mobile layout usable?
- Is `site-manifest.json` present and accurate?
""";
    }

    private static string DescribeEvidenceFile(string file)
    {
        if (file.EndsWith("ai-reconstruction-brief.md", StringComparison.OrdinalIgnoreCase))
        {
            return "human-readable reconstruction intent and implementation guidance";
        }

        if (file.EndsWith("reconstruction-evidence-graph.json", StringComparison.OrdinalIgnoreCase))
        {
            return "joined section/media/behavior/CSS/JS reconstruction evidence";
        }

        if (file.EndsWith("section-map.json", StringComparison.OrdinalIgnoreCase))
        {
            return "section order, headings, CTAs, visual roles, and section-level signals";
        }

        if (file.EndsWith("media-placement-map.json", StringComparison.OrdinalIgnoreCase))
        {
            return "media placement and replacement slot evidence";
        }

        if (file.EndsWith("behavior-map.json", StringComparison.OrdinalIgnoreCase))
        {
            return "sticky, parallax, reveal, plugin, and animation declarations";
        }

        if (file.EndsWith("css-rule-map.json", StringComparison.OrdinalIgnoreCase))
        {
            return "CSS selector and class/rule evidence";
        }

        if (file.EndsWith("js-behavior-reference-map.json", StringComparison.OrdinalIgnoreCase))
        {
            return "JavaScript behavior owner/reference evidence";
        }

        if (file.EndsWith("first-screen.png", StringComparison.OrdinalIgnoreCase))
        {
            return "controlled desktop first-screen visual reference";
        }

        return "source evidence";
    }

    private static List<AssetSlotPlanItem> ExtractMediaSlots(string mediaJson)
    {
        try
        {
            using var document = JsonDocument.Parse(mediaJson);
            var elements = EnumerateArrayOrProperty(document.RootElement, "media").ToList();
            var slots = new List<AssetSlotPlanItem>();

            foreach (var element in elements.Take(24))
            {
                var role = GetString(element, "role");
                var selector = GetString(element, "selector");
                var aspectRatio = GetString(element, "aspectRatio");
                var widthHeight = GetString(element, "widthHeight");
                var tag = GetString(element, "tag");
                var sectionKey = GetString(element, "sectionKey");
                var url = GetString(element, "url");
                var normalizedRole = NormalizeMediaRole(role, selector, tag, url);

                slots.Add(new AssetSlotPlanItem
                {
                    SlotId = $"{normalizedRole}-slot-{slots.Count + 1:00}",
                    Role = normalizedRole,
                    RecommendedAssetType = RecommendedAssetType(normalizedRole),
                    DesktopRatio = string.IsNullOrWhiteSpace(aspectRatio) ? InferRatio(widthHeight, normalizedRole) : aspectRatio,
                    MobileRatio = normalizedRole.Contains("hero", StringComparison.OrdinalIgnoreCase) ? "9:16 or 4:5 crop" : "auto or 4:5 crop",
                    FallbackBehavior = FallbackBehavior(normalizedRole),
                    SourceHint = string.Join(" | ", new[] { sectionKey, selector, tag }.Where(value => !string.IsNullOrWhiteSpace(value)))
                });
            }

            return slots
                .GroupBy(slot => slot.SlotId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
        catch (JsonException)
        {
            return BuildFallbackSlots();
        }
    }

    private static List<AssetSlotPlanItem> BuildFallbackSlots()
    {
        return
        [
            new()
            {
                SlotId = "hero-media-slot-01",
                Role = "hero video/image slot",
                RecommendedAssetType = "user-owned video, generated motion still, or architectural hero image",
                DesktopRatio = "16:9 or full-bleed viewport",
                MobileRatio = "9:16 or 4:5 crop",
                FallbackBehavior = "CSS gradient, slow zoom, and geometric depth layers"
            },
            new()
            {
                SlotId = "section-background-slot-01",
                Role = "section background image slot",
                RecommendedAssetType = "user-owned architectural background image",
                DesktopRatio = "16:9",
                MobileRatio = "4:5 crop",
                FallbackBehavior = "neutral gray panel with soft depth gradient"
            },
            new()
            {
                SlotId = "slider-card-slot-01",
                Role = "slider/card image slot",
                RecommendedAssetType = "user-owned office, lobby, facade, or detail image",
                DesktopRatio = "4:3 or 16:10",
                MobileRatio = "1:1 or 4:5",
                FallbackBehavior = "static card with generated abstract architectural pattern"
            },
            new()
            {
                SlotId = "logo-textmark-slot-01",
                Role = "logo slot",
                RecommendedAssetType = "new textmark or user-provided logo",
                DesktopRatio = "text/SVG replacement",
                MobileRatio = "text/SVG replacement",
                FallbackBehavior = "use plain text brand mark"
            },
            new()
            {
                SlotId = "map-deco-slot-01",
                Role = "map/deco slot",
                RecommendedAssetType = "simple generated location graphic or neutral line-art map",
                DesktopRatio = "16:9",
                MobileRatio = "1:1",
                FallbackBehavior = "CSS linework and labels"
            }
        ];
    }

    private static List<string> ExtractBriefHighlights(string brief)
    {
        var lines = brief
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimStart('-', '*', '#', ' '))
            .Where(line => line.Length > 0)
            .Where(line =>
                line.Contains("Hero", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Vimeo", StringComparison.OrdinalIgnoreCase)
                || line.Contains("responsive", StringComparison.OrdinalIgnoreCase)
                || line.Contains("picture", StringComparison.OrdinalIgnoreCase)
                || line.Contains("parallax", StringComparison.OrdinalIgnoreCase)
                || line.Contains("contentAnimation", StringComparison.OrdinalIgnoreCase)
                || line.Contains("reveal", StringComparison.OrdinalIgnoreCase)
                || line.Contains("sticky", StringComparison.OrdinalIgnoreCase)
                || line.Contains("rebuild", StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();

        if (lines.Count != 0)
        {
            return lines;
        }

        return
        [
            "Hero evidence requires a premium full-screen opening.",
            "Vimeo-like background motion must be replaced with safe user-owned or generated assets.",
            "Responsive picture/source intent should be rebuilt with new media slots.",
            "Parallax, reveal, sticky, and contentAnimation intent should be recreated with new static CSS/JS."
        ];
    }

    private static List<string> ExtractStrings(
        string json,
        IReadOnlyList<string> propertyNames,
        int maxItems)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var values = new List<string>();
            foreach (var element in EnumerateAnyObjects(document.RootElement))
            {
                foreach (var propertyName in propertyNames)
                {
                    var value = GetString(element, propertyName);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }

                if (values.Count >= maxItems)
                {
                    break;
                }
            }

            return values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxItems)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<JsonElement> EnumerateArrayOrProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (root.ValueKind == JsonValueKind.Object
                 && root.TryGetProperty(propertyName, out var property)
                 && property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateAnyObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
            foreach (var property in root.EnumerateObject())
            {
                foreach (var nested in EnumerateAnyObjects(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var nested in EnumerateAnyObjects(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static string NormalizeMediaRole(string role, string selector, string tag, string url)
    {
        var joined = $"{role} {selector} {tag} {url}".ToLowerInvariant();
        if (joined.Contains("hero") || joined.Contains("vimeo") || joined.Contains("iframe") || joined.Contains("video"))
        {
            return "hero media";
        }

        if (joined.Contains("logo") || joined.Contains("icon"))
        {
            return "logo";
        }

        if (joined.Contains("slider") || joined.Contains("carousel") || joined.Contains("card"))
        {
            return "slider/card image";
        }

        if (joined.Contains("map"))
        {
            return "map/deco";
        }

        if (joined.Contains("background") || joined.Contains("bg"))
        {
            return "section background image";
        }

        return string.IsNullOrWhiteSpace(role) ? "section media" : role;
    }

    private static string RecommendedAssetType(string role)
    {
        if (role.Contains("hero", StringComparison.OrdinalIgnoreCase))
        {
            return "user-owned video, generated motion still, or architectural hero image";
        }

        if (role.Contains("logo", StringComparison.OrdinalIgnoreCase))
        {
            return "new textmark or user-provided logo";
        }

        if (role.Contains("map", StringComparison.OrdinalIgnoreCase))
        {
            return "simple generated map/decorative linework";
        }

        return "user-owned or generated architectural image";
    }

    private static string InferRatio(string widthHeight, string role)
    {
        if (!string.IsNullOrWhiteSpace(widthHeight))
        {
            return widthHeight;
        }

        return role.Contains("hero", StringComparison.OrdinalIgnoreCase)
            ? "16:9 or full-bleed viewport"
            : "16:9, 4:3, or evidence-driven crop";
    }

    private static string FallbackBehavior(string role)
    {
        if (role.Contains("hero", StringComparison.OrdinalIgnoreCase))
        {
            return "CSS gradient, slow zoom, and simulated video depth";
        }

        if (role.Contains("logo", StringComparison.OrdinalIgnoreCase))
        {
            return "plain text mark";
        }

        return "neutral placeholder with CSS geometric treatment";
    }

    private static string FormatBullets(IEnumerable<string> values, string fallback = "- Evidence did not include a precise item; use the surrounding brief and graph outputs.")
    {
        var lines = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(18)
            .Select(value => $"- {value}")
            .ToList();

        return lines.Count == 0 ? fallback : string.Join(Environment.NewLine, lines);
    }

    private static async Task WriteTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken);
    }

    private static string GetUniquePackageRoot(
        string projectDirectory,
        string initialPackageId,
        out string packageId)
    {
        var root = Path.Combine(
            projectDirectory,
            SinglePageGenerationPackageSchema.RootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(root);

        packageId = initialPackageId;
        var packageRoot = Path.Combine(root, packageId);
        var suffix = 2;
        while (Directory.Exists(packageRoot))
        {
            packageId = $"{initialPackageId}-{suffix:00}";
            packageRoot = Path.Combine(root, packageId);
            suffix++;
        }

        return packageRoot;
    }

    private static string NormalizeSeparators(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private sealed class SinglePageGenerationEvidence
    {
        public string AiBrief { get; set; } = string.Empty;
        public string ReconstructionGraphJson { get; set; } = string.Empty;
        public string SectionMapJson { get; set; } = string.Empty;
        public string MediaPlacementMapJson { get; set; } = string.Empty;
        public string BehaviorMapJson { get; set; } = string.Empty;
        public string CssRuleMapJson { get; set; } = string.Empty;
        public string JsBehaviorReferenceMapJson { get; set; } = string.Empty;
        public List<string> BriefHighlights { get; set; } = [];
        public List<string> SectionHeadings { get; set; } = [];
        public List<AssetSlotPlanItem> MediaSlots { get; set; } = [];
        public List<string> BehaviorSignals { get; set; } = [];
        public List<string> CssSignals { get; set; } = [];
        public List<string> JsSignals { get; set; } = [];
    }

    private sealed class AssetSlotPlanItem
    {
        public string SlotId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string RecommendedAssetType { get; set; } = string.Empty;
        public string DesktopRatio { get; set; } = string.Empty;
        public string MobileRatio { get; set; } = string.Empty;
        public string FallbackBehavior { get; set; } = string.Empty;
        public string SourceHint { get; set; } = string.Empty;
    }
}
