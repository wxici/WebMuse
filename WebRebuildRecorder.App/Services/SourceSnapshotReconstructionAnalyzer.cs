using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class SourceSnapshotReconstructionAnalyzer
{
    private static readonly Regex TagRegex = new(
        """<(?<tag>[a-zA-Z][\w:-]*)\b(?<attrs>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex AttributeRegex = new(
        """(?<name>[\w:-]+)\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<bare>[^\s"'=<>`]+))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex StripTagsRegex = new(
        """<[^>]+>""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex CollapseWhitespaceRegex = new(
        """\s+""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    public SourceSnapshotReconstructionBundle Analyze(
        Uri referenceUri,
        string rawHtml,
        SourceSnapshotRenderedEvidence renderedEvidence,
        SourceSnapshotResourceManifest resourceManifest,
        IReadOnlyList<SourceSnapshotTextResource> textResources,
        IReadOnlyDictionary<string, string> textResourceContents)
    {
        var rawTags = ReadTags(rawHtml);
        var sections = BuildSectionMap(renderedEvidence, rawTags);
        var media = BuildMediaPlacementMap(referenceUri, renderedEvidence, rawTags);
        var responsive = BuildResponsiveMediaMap(referenceUri, rawTags, media);
        var behaviors = BuildBehaviorMap(renderedEvidence, rawTags, textResourceContents);
        var cssRules = BuildCssRuleMap(sections, renderedEvidence, textResourceContents);
        var jsReferences = BuildJsBehaviorReferenceMap(rawTags, behaviors, textResourceContents);
        var dependencyGraph = BuildDependencyGraph(resourceManifest, textResources, media, sections);
        LinkEvidence(sections, media, responsive, behaviors);
        var graph = BuildEvidenceGraph(sections, media, responsive, behaviors, cssRules, jsReferences, renderedEvidence);

        return new SourceSnapshotReconstructionBundle
        {
            DependencyGraph = dependencyGraph,
            SectionMap = sections,
            MediaPlacementMap = media,
            ResponsiveMediaMap = responsive,
            BehaviorMap = behaviors,
            AnimationSignalMap = behaviors
                .Where(item => item.Trigger is "reveal" or "sticky" or "scroll" or "carousel" or "load"
                    || item.Plugin.Contains("parallax", StringComparison.OrdinalIgnoreCase)
                    || item.Plugin.Contains("content", StringComparison.OrdinalIgnoreCase)
                    || item.RawDeclaration.Contains("animation", StringComparison.OrdinalIgnoreCase))
                .Take(200)
                .ToList(),
            CssRuleMap = cssRules,
            JsBehaviorReferenceMap = jsReferences,
            ReconstructionEvidenceGraph = graph,
            AiReconstructionBriefMarkdown = BuildAiReconstructionBrief(
                referenceUri,
                renderedEvidence,
                sections,
                media,
                responsive,
                behaviors,
                cssRules,
                jsReferences,
                graph)
        };
    }

    private static List<SourceSnapshotSectionMapItem> BuildSectionMap(
        SourceSnapshotRenderedEvidence renderedEvidence,
        IReadOnlyList<RawTag> rawTags)
    {
        var sectionElements = renderedEvidence.Elements
            .Where(item => IsSectionLike(item))
            .Take(80)
            .ToList();

        var result = new List<SourceSnapshotSectionMapItem>();
        var index = 1;
        foreach (var element in sectionElements)
        {
            var classes = NormalizeClasses(element.CssClasses.Count > 0
                ? element.CssClasses
                : SplitClasses(element.ClassName));
            var heading = FindHeadingForSection(element, renderedEvidence.Elements);
            var role = InferSectionRole(element, classes, heading);
            result.Add(new SourceSnapshotSectionMapItem
            {
                SectionKey = $"section-{index:00}-{role}",
                Selector = BuildSelector(element),
                Role = role,
                Heading = heading,
                BodyPreview = CleanText(element.Text, 500),
                CssClasses = classes,
                CtaTexts = FindCtasNear(element, renderedEvidence.Elements),
                MediaRefs = [],
                BehaviorRefs = [],
                ResponsiveFlags = [],
                VisualRole = InferVisualRole(classes, element.Text)
            });
            index++;
        }

        if (result.Count == 0)
        {
            var rawSections = rawTags
                .Where(tag => tag.Name.Equals("section", StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();
            foreach (var tag in rawSections)
            {
                var classes = SplitClasses(tag.Attributes.GetValueOrDefault("class"));
                result.Add(new SourceSnapshotSectionMapItem
                {
                    SectionKey = $"section-{index:00}-{InferSectionRole(null, classes, string.Empty)}",
                    Selector = SelectorFromRawTag(tag),
                    Role = InferSectionRole(null, classes, string.Empty),
                    CssClasses = classes,
                    VisualRole = InferVisualRole(classes, string.Empty)
                });
                index++;
            }
        }

        return result;
    }

    private static List<SourceSnapshotMediaPlacementItem> BuildMediaPlacementMap(
        Uri referenceUri,
        SourceSnapshotRenderedEvidence renderedEvidence,
        IReadOnlyList<RawTag> rawTags)
    {
        var result = new List<SourceSnapshotMediaPlacementItem>();

        foreach (var element in renderedEvidence.Elements.Where(item =>
                     item.Tag is "img" or "video" or "iframe" or "picture" or "source" or "svg" or "use"))
        {
            var url = FirstNonEmpty(element.Src, element.Href);
            result.Add(new SourceSnapshotMediaPlacementItem
            {
                SectionKey = FindNearestSectionKey(element, renderedEvidence.Elements),
                Selector = BuildSelector(element),
                Role = InferMediaRole(element.Tag, element.ClassName, url),
                Url = url,
                Tag = element.Tag,
                AspectRatio = Ratio(element.Width, element.Height),
                WidthHeight = $"{Math.Round(element.Width)}x{Math.Round(element.Height)}",
                CssClasses = NormalizeClasses(element.CssClasses.Count > 0
                    ? element.CssClasses
                    : SplitClasses(element.ClassName)),
                Behaviors = ExtractBehaviorNames(element.DataAttributes),
                ReplacementAdvice = ReplacementAdvice(url, element.Tag)
            });
        }

        foreach (var tag in rawTags.Where(tag => tag.Name is "iframe" or "video" or "img" or "source" or "svg" or "use"))
        {
            var url = ResolveFirstUrl(referenceUri, tag.Attributes);
            if (string.IsNullOrWhiteSpace(url)
                && tag.Name.Equals("svg", StringComparison.OrdinalIgnoreCase)
                && tag.Attributes.TryGetValue("class", out var svgClass)
                && svgClass.Contains("air", StringComparison.OrdinalIgnoreCase))
            {
                url = "inline-svg:air";
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            result.Add(new SourceSnapshotMediaPlacementItem
            {
                SectionKey = InferSectionFromClasses(tag.Attributes.GetValueOrDefault("class")),
                Selector = SelectorFromRawTag(tag),
                Role = InferMediaRole(tag.Name, tag.Attributes.GetValueOrDefault("class") ?? string.Empty, url),
                Url = url,
                Tag = tag.Name.ToLowerInvariant(),
                AspectRatio = tag.Attributes.GetValueOrDefault("style") ?? string.Empty,
                WidthHeight = BuildWidthHeight(tag.Attributes),
                ResponsiveVariants = SplitSrcSet(referenceUri, tag.Attributes.GetValueOrDefault("srcset")).ToList(),
                CssClasses = SplitClasses(tag.Attributes.GetValueOrDefault("class")),
                Behaviors = ExtractBehaviorNames(tag.Attributes),
                ReplacementAdvice = ReplacementAdvice(url, tag.Name)
            });
        }

        return result
            .GroupBy(item => $"{item.Tag}|{item.Url}|{item.Selector}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(250)
            .ToList();
    }

    private static List<SourceSnapshotMediaPlacementItem> BuildResponsiveMediaMap(
        Uri referenceUri,
        IReadOnlyList<RawTag> rawTags,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media)
    {
        var result = new List<SourceSnapshotMediaPlacementItem>();

        foreach (var tag in rawTags.Where(item => item.Name is "picture" or "source" or "img"))
        {
            var variants = SplitSrcSet(referenceUri, tag.Attributes.GetValueOrDefault("srcset")).ToList();
            var src = ResolveFirstUrl(referenceUri, tag.Attributes);
            if (variants.Count == 0 && string.IsNullOrWhiteSpace(src))
            {
                continue;
            }

            var mediaQuery = tag.Attributes.GetValueOrDefault("media") ?? string.Empty;
            result.Add(new SourceSnapshotMediaPlacementItem
            {
                SectionKey = InferSectionFromClasses(tag.Attributes.GetValueOrDefault("class")),
                Selector = SelectorFromRawTag(tag),
                Role = mediaQuery.Length > 0 ? $"responsive-source {mediaQuery}" : "responsive-media",
                Url = src,
                Tag = tag.Name.ToLowerInvariant(),
                AspectRatio = tag.Attributes.GetValueOrDefault("style") ?? string.Empty,
                WidthHeight = BuildWidthHeight(tag.Attributes),
                ResponsiveVariants = variants,
                CssClasses = SplitClasses(tag.Attributes.GetValueOrDefault("class")),
                ReplacementAdvice = "Map breakpoints and replace protected source media with licensed equivalents."
            });
        }

        foreach (var item in media.Where(item =>
                     item.Url.Contains("@xs", StringComparison.OrdinalIgnoreCase)
                     || item.Url.Contains("@lg", StringComparison.OrdinalIgnoreCase)
                     || item.Url.Contains("_xs", StringComparison.OrdinalIgnoreCase)
                     || item.Url.Contains("_lg", StringComparison.OrdinalIgnoreCase)
                     || item.Url.Contains("srcset", StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(item);
        }

        return result
            .GroupBy(item => $"{item.Tag}|{item.Url}|{item.Selector}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(180)
            .ToList();
    }

    private static List<SourceSnapshotBehaviorItem> BuildBehaviorMap(
        SourceSnapshotRenderedEvidence renderedEvidence,
        IReadOnlyList<RawTag> rawTags,
        IReadOnlyDictionary<string, string> textResourceContents)
    {
        var result = new List<SourceSnapshotBehaviorItem>();

        foreach (var element in renderedEvidence.Elements.Where(item => item.DataAttributes.Count > 0))
        {
            foreach (var behavior in BuildBehaviorsFromAttributes(
                         BuildSelector(element),
                         FindNearestSectionKey(element, renderedEvidence.Elements),
                         element.DataAttributes,
                         textResourceContents))
            {
                result.Add(behavior);
            }
        }

        foreach (var tag in rawTags.Where(tag => tag.Attributes.Keys.Any(IsBehaviorAttribute)))
        {
            foreach (var behavior in BuildBehaviorsFromAttributes(
                         SelectorFromRawTag(tag),
                         InferSectionFromClasses(tag.Attributes.GetValueOrDefault("class")),
                         tag.Attributes,
                         textResourceContents))
            {
                result.Add(behavior);
            }
        }

        foreach (var tag in rawTags.Where(tag => tag.Name.Equals("iframe", StringComparison.OrdinalIgnoreCase)))
        {
            var src = tag.Attributes.GetValueOrDefault("src") ?? string.Empty;
            if (src.Contains("player.vimeo.com", StringComparison.OrdinalIgnoreCase)
                || src.Contains("youtube", StringComparison.OrdinalIgnoreCase)
                || src.Contains("autoplay", StringComparison.OrdinalIgnoreCase)
                || src.Contains("background", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new SourceSnapshotBehaviorItem
                {
                    SectionKey = InferSectionFromClasses(tag.Attributes.GetValueOrDefault("class")),
                    Selector = SelectorFromRawTag(tag),
                    Trigger = "load",
                    Plugin = "iframe-video-background",
                    Pattern = src.Contains("vimeo", StringComparison.OrdinalIgnoreCase) ? "Vimeo background" : "embedded video background",
                    RawDeclaration = src,
                    LikelyJsOwner = FindLikelyJsOwner("iframe", textResourceContents),
                    EffectSummary = "Hero or section video background is declared through an iframe embed.",
                    RebuildSuggestion = "Replace protected video with an approved video or generated motion placeholder while preserving autoplay/muted/background behavior."
                });
            }
        }

        return result
            .GroupBy(item => $"{item.Selector}|{item.Trigger}|{item.RawDeclaration}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(250)
            .ToList();
    }

    private static List<SourceSnapshotCssRuleMapItem> BuildCssRuleMap(
        IReadOnlyList<SourceSnapshotSectionMapItem> sections,
        SourceSnapshotRenderedEvidence renderedEvidence,
        IReadOnlyDictionary<string, string> textResourceContents)
    {
        var classes = sections.SelectMany(item => item.CssClasses)
            .Concat(renderedEvidence.Elements.SelectMany(item => item.CssClasses.Count > 0
                ? item.CssClasses
                : SplitClasses(item.ClassName)))
            .Where(value => value.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(220)
            .ToList();

        var cssFiles = textResourceContents
            .Where(pair => pair.Key.EndsWith(".css.txt", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Contains(".css", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new List<SourceSnapshotCssRuleMapItem>();
        foreach (var css in cssFiles)
        {
            foreach (var className in classes)
            {
                var selector = "." + className;
                if (!css.Value.Contains(selector, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new SourceSnapshotCssRuleMapItem
                {
                    Selector = selector,
                    SourceCssFile = css.Key,
                    MatchedClasses = [className],
                    ImportantProperties = ExtractImportantProperties(css.Value, selector),
                    RebuildSuggestion = SuggestCssRebuild(className)
                });
            }
        }

        return result
            .GroupBy(item => $"{item.SourceCssFile}|{item.Selector}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(200)
            .ToList();
    }

    private static List<SourceSnapshotJsBehaviorReferenceItem> BuildJsBehaviorReferenceMap(
        IReadOnlyList<RawTag> rawTags,
        IReadOnlyList<SourceSnapshotBehaviorItem> behaviors,
        IReadOnlyDictionary<string, string> textResourceContents)
    {
        var htmlDataNames = rawTags
            .SelectMany(tag => tag.Attributes.Keys)
            .Where(name => name.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<SourceSnapshotJsBehaviorReferenceItem>();
        foreach (var js in textResourceContents.Where(pair =>
                     pair.Key.EndsWith(".js.txt", StringComparison.OrdinalIgnoreCase)
                     || pair.Key.Contains(".js", StringComparison.OrdinalIgnoreCase)))
        {
            var dataNames = htmlDataNames
                .Where(name => js.Value.Contains(name, StringComparison.OrdinalIgnoreCase)
                    || js.Value.Contains(ToCamelCase(name[5..]), StringComparison.OrdinalIgnoreCase))
                .Take(80)
                .ToList();
            var pluginNames = ExtractKeywordHits(js.Value, [
                "parallax",
                "contentAnimation",
                "reveal",
                "sticky",
                "scrollSnap",
                "carousel",
                "swiper",
                "iframeSize",
                "preloader",
                "Vimeo"
            ]);
            var matched = behaviors
                .Where(item => dataNames.Any(name => item.RawDeclaration.Contains(name, StringComparison.OrdinalIgnoreCase))
                    || pluginNames.Any(name => item.RawDeclaration.Contains(name, StringComparison.OrdinalIgnoreCase)
                        || item.Plugin.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .Select(item => Limit(item.RawDeclaration, 260))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList();

            result.Add(new SourceSnapshotJsBehaviorReferenceItem
            {
                SourceJsFile = js.Key,
                MinifiedLikely = IsLikelyMinified(js.Value),
                PluginNamesFound = pluginNames,
                AnimationNamesFound = ExtractKeywordHits(js.Value, [
                    "landingIntroMove",
                    "landingIntroFade",
                    "changeShow",
                    "changeHide",
                    "textOut",
                    "text",
                    "clip",
                    "fade"
                ]),
                DataAttributeNamesFound = dataNames,
                MatchedHtmlDeclarations = matched,
                Notes = pluginNames.Count > 0 || dataNames.Count > 0
                    ? "JavaScript contains names that correspond to HTML behavior declarations."
                    : "No direct data-attribute match found in this bounded JS sample."
            });
        }

        return result.Take(80).ToList();
    }

    private static SourceSnapshotDependencyGraph BuildDependencyGraph(
        SourceSnapshotResourceManifest resourceManifest,
        IReadOnlyList<SourceSnapshotTextResource> textResources,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media,
        IReadOnlyList<SourceSnapshotSectionMapItem> sections)
    {
        var textByUrl = textResources.ToDictionary(item => item.Url, StringComparer.OrdinalIgnoreCase);
        var nodes = resourceManifest.Css
            .Concat(resourceManifest.JavaScript)
            .Concat(resourceManifest.Images)
            .Concat(resourceManifest.Fonts)
            .Concat(resourceManifest.Videos)
            .Concat(resourceManifest.Other)
            .Select(item =>
            {
                textByUrl.TryGetValue(item.Url, out var text);
                return new SourceSnapshotDependencyNode
                {
                    Url = item.Url,
                    Type = item.Kind,
                    SourceTag = item.SourceTag,
                    SameOrigin = item.IsSameOrigin,
                    TextContentFetched = text?.Fetched == true,
                    BinaryOnlyListed = item.Kind is "image" or "font" or "video" && text is null,
                    ReferencedBySection = FindMediaSection(item.Url, media, sections),
                    LocalRelativePath = text?.LocalRelativePath ?? string.Empty
                };
            })
            .GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SourceSnapshotDependencyGraph { Nodes = nodes };
    }

    private static SourceSnapshotReconstructionEvidenceGraph BuildEvidenceGraph(
        IReadOnlyList<SourceSnapshotSectionMapItem> sections,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> responsive,
        IReadOnlyList<SourceSnapshotBehaviorItem> behaviors,
        IReadOnlyList<SourceSnapshotCssRuleMapItem> cssRules,
        IReadOnlyList<SourceSnapshotJsBehaviorReferenceItem> jsReferences,
        SourceSnapshotRenderedEvidence renderedEvidence)
    {
        var warnings = new List<string>();
        if (renderedEvidence.Viewport.Width < 1000)
        {
            warnings.Add($"Rendered viewport is narrow: {renderedEvidence.Viewport.Width}x{renderedEvidence.Viewport.Height}.");
        }

        if (!media.Any(item => item.Url.Contains("vimeo", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("No Vimeo iframe/video background detected in bounded evidence.");
        }

        if (cssRules.Count == 0)
        {
            warnings.Add("No CSS rule to DOM class mapping was produced.");
        }

        if (jsReferences.Count == 0)
        {
            warnings.Add("No JS behavior reference mapping was produced.");
        }

        return new SourceSnapshotReconstructionEvidenceGraph
        {
            Summary =
            [
                $"Controlled rendered viewport evidence: {renderedEvidence.Viewport.Width}x{renderedEvidence.Viewport.Height}.",
                $"Sections mapped: {sections.Count}.",
                $"Media placements mapped: {media.Count}.",
                $"Responsive media signals mapped: {responsive.Count}.",
                $"Behavior declarations mapped: {behaviors.Count}.",
                $"CSS class/rule evidence rows: {cssRules.Count}.",
                $"JS behavior reference rows: {jsReferences.Count}."
            ],
            Sections = sections.ToList(),
            Media = media.Concat(responsive).DistinctByKey(item => $"{item.Selector}|{item.Url}|{item.Role}").Take(250).ToList(),
            Behaviors = behaviors.ToList(),
            CssRules = cssRules.ToList(),
            JsReferences = jsReferences.ToList(),
            RebuildSuggestions = BuildRebuildSuggestions(media, responsive, behaviors, cssRules, jsReferences),
            Warnings = warnings
        };
    }

    private static string BuildAiReconstructionBrief(
        Uri referenceUri,
        SourceSnapshotRenderedEvidence renderedEvidence,
        IReadOnlyList<SourceSnapshotSectionMapItem> sections,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> responsive,
        IReadOnlyList<SourceSnapshotBehaviorItem> behaviors,
        IReadOnlyList<SourceSnapshotCssRuleMapItem> cssRules,
        IReadOnlyList<SourceSnapshotJsBehaviorReferenceItem> jsReferences,
        SourceSnapshotReconstructionEvidenceGraph graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AI Reconstruction Brief");
        builder.AppendLine();
        builder.AppendLine($"Target: {referenceUri.AbsoluteUri}");
        builder.AppendLine($"Captured viewport: {renderedEvidence.Viewport.Width}x{renderedEvidence.Viewport.Height}.");
        builder.AppendLine();
        builder.AppendLine("## Rebuild Goal");
        builder.AppendLine();
        builder.AppendLine("Rebuild a single-page approximation using local, licensed, or generated replacement assets. Preserve structure, section rhythm, viewport composition, motion intent, and content hierarchy without copying protected media.");
        builder.AppendLine();
        builder.AppendLine("## Hero Evidence");
        builder.AppendLine();
        AppendItems(builder, media
            .Where(item => item.Role.Contains("hero", StringComparison.OrdinalIgnoreCase)
                || item.Url.Contains("vimeo", StringComparison.OrdinalIgnoreCase)
                || item.Url.Contains("intro", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"- {item.Role}: {item.Tag} {item.Url} {string.Join(", ", item.Behaviors)}")
            .DefaultIfEmpty("- No hero media signal found."));
        builder.AppendLine();
        builder.AppendLine("## Vimeo / Video Evidence");
        builder.AppendLine();
        AppendItems(builder, media
            .Where(item => item.Url.Contains("vimeo", StringComparison.OrdinalIgnoreCase)
                || item.Url.Contains("youtube", StringComparison.OrdinalIgnoreCase)
                || item.Role.Contains("video", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"- Vimeo/video candidate: {item.Url} ({item.Selector})")
            .DefaultIfEmpty("- No Vimeo/video candidate found."));
        builder.AppendLine();
        builder.AppendLine("## Responsive Media Evidence");
        builder.AppendLine();
        AppendItems(builder, responsive
            .Take(30)
            .Select(item => $"- responsive {item.Tag}: {item.Role}; {item.Url}; variants: {string.Join(" | ", item.ResponsiveVariants.Take(4))}")
            .DefaultIfEmpty("- No responsive picture/source evidence found."));
        builder.AppendLine();
        builder.AppendLine("## Section Map");
        builder.AppendLine();
        AppendItems(builder, sections
            .Take(20)
            .Select(item => $"- {item.SectionKey}: {item.Role}; heading: {item.Heading}; classes: {string.Join(" ", item.CssClasses.Take(8))}"));
        builder.AppendLine();
        builder.AppendLine("## Behavior And Animation Evidence");
        builder.AppendLine();
        AppendItems(builder, behaviors
            .Take(40)
            .Select(item => $"- {item.Trigger}/{item.Plugin}: {item.Pattern}; effect: {item.EffectSummary}; rebuild: {item.RebuildSuggestion}")
            .DefaultIfEmpty("- No parallax, reveal, sticky, contentAnimation, or carousel behavior found."));
        builder.AppendLine();
        builder.AppendLine("## CSS Rule Evidence");
        builder.AppendLine();
        AppendItems(builder, cssRules
            .Take(40)
            .Select(item => $"- {item.SourceCssFile} {item.Selector}: {string.Join("; ", item.ImportantProperties.Take(6))}")
            .DefaultIfEmpty("- No CSS rule evidence found."));
        builder.AppendLine();
        builder.AppendLine("## JS Behavior References");
        builder.AppendLine();
        AppendItems(builder, jsReferences
            .Take(20)
            .Select(item => $"- {item.SourceJsFile}: plugins={string.Join(", ", item.PluginNamesFound)} data={string.Join(", ", item.DataAttributeNamesFound.Take(8))}; {item.Notes}")
            .DefaultIfEmpty("- No JS behavior reference evidence found."));
        builder.AppendLine();
        builder.AppendLine("## Reconstruction Suggestions");
        builder.AppendLine();
        AppendItems(builder, graph.RebuildSuggestions.Select(item => $"- {item}"));
        builder.AppendLine();
        builder.AppendLine("## Boundaries");
        builder.AppendLine();
        builder.AppendLine("- This evidence is for local reconstruction planning only.");
        builder.AppendLine("- Do not reuse protected brand media directly.");
        builder.AppendLine("- Replace original image, video, and font assets with licensed or generated equivalents.");
        builder.AppendLine("- Do not execute Codex CLI or generate a website from this brief until the runtime package is reviewed.");
        return builder.ToString();
    }

    private static void LinkEvidence(
        IReadOnlyList<SourceSnapshotSectionMapItem> sections,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> responsive,
        IReadOnlyList<SourceSnapshotBehaviorItem> behaviors)
    {
        foreach (var section in sections)
        {
            section.MediaRefs.AddRange(media
                .Where(item => SameSection(section.SectionKey, item.SectionKey)
                    || section.CssClasses.Any(cls => item.Selector.Contains(cls, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Url.Length > 0 ? item.Url : item.Selector)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20));
            section.MediaRefs.AddRange(responsive
                .Where(item => SameSection(section.SectionKey, item.SectionKey))
                .SelectMany(item => item.ResponsiveVariants.Prepend(item.Url))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12));
            section.BehaviorRefs.AddRange(behaviors
                .Where(item => SameSection(section.SectionKey, item.SectionKey)
                    || section.CssClasses.Any(cls => item.Selector.Contains(cls, StringComparison.OrdinalIgnoreCase)))
                .Select(item => $"{item.Trigger}:{item.Plugin}:{item.Pattern}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20));
            if (responsive.Any(item => SameSection(section.SectionKey, item.SectionKey))
                || section.MediaRefs.Any(value => value.Contains("@xs", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("@lg", StringComparison.OrdinalIgnoreCase)))
            {
                section.ResponsiveFlags.Add("responsive media variants detected");
            }
        }
    }

    private static IEnumerable<SourceSnapshotBehaviorItem> BuildBehaviorsFromAttributes(
        string selector,
        string sectionKey,
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyDictionary<string, string> textResourceContents)
    {
        foreach (var attribute in attributes.Where(item => IsBehaviorAttribute(item.Key)))
        {
            var name = attribute.Key.ToLowerInvariant();
            var value = WebUtility.HtmlDecode(attribute.Value ?? string.Empty);
            var trigger = name switch
            {
                "data-scroll-sticky" => "sticky",
                "data-scroll-snap-point" => "scroll",
                "data-scroll-section" => "scroll",
                "data-reveal" => "reveal",
                "data-content-animation-animations" => "reveal",
                "data-carousel-web-gl-images" => "carousel",
                "data-iframe-size" => "load",
                _ when value.Contains("parallax", StringComparison.OrdinalIgnoreCase) => "scroll",
                _ => "load"
            };
            var plugin = name == "data-plugin" ? value : name[5..];
            yield return new SourceSnapshotBehaviorItem
            {
                SectionKey = sectionKey,
                Selector = selector,
                Trigger = trigger,
                Plugin = Limit(plugin, 120),
                Pattern = Limit(value, 240),
                AnimationName = ExtractAnimationName(value),
                RawDeclaration = $"{attribute.Key}={Limit(value, 600)}",
                LikelyJsOwner = FindLikelyJsOwner(attribute.Key, textResourceContents),
                EffectSummary = SummarizeEffect(attribute.Key, value),
                RebuildSuggestion = SuggestBehaviorRebuild(attribute.Key, value)
            };
        }
    }

    private static IReadOnlyList<RawTag> ReadTags(string html)
    {
        return TagRegex.Matches(html ?? string.Empty)
            .Select(match => new RawTag(
                match.Groups["tag"].Value.ToLowerInvariant(),
                ParseAttributes(match.Groups["attrs"].Value)))
            .ToList();
    }

    private static Dictionary<string, string> ParseAttributes(string attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attributes ?? string.Empty))
        {
            var value = match.Groups["double"].Success
                ? match.Groups["double"].Value
                : match.Groups["single"].Success
                    ? match.Groups["single"].Value
                    : match.Groups["bare"].Value;
            result[match.Groups["name"].Value] = value;
        }

        return result;
    }

    private static bool IsSectionLike(SourceSnapshotElementItem item)
    {
        return item.Tag is "header" or "nav" or "main" or "section" or "article" or "footer"
            || item.CssClasses.Any(cls => cls.Contains("section", StringComparison.OrdinalIgnoreCase)
                || cls.Contains("hero", StringComparison.OrdinalIgnoreCase)
                || cls.Contains("intro", StringComparison.OrdinalIgnoreCase))
            || item.ClassName.Contains("section", StringComparison.OrdinalIgnoreCase)
            || item.ClassName.Contains("hero", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindHeadingForSection(SourceSnapshotElementItem section, IReadOnlyList<SourceSnapshotElementItem> elements)
    {
        var heading = elements
            .Where(item => (item.Tag is "h1" or "h2" or "h3")
                && item.Y >= section.Y - 20
                && item.Y <= section.Y + Math.Max(section.Height, 700))
            .OrderBy(item => item.Y)
            .Select(item => CleanText(item.Text, 180))
            .FirstOrDefault(value => value.Length > 0);
        if (!string.IsNullOrWhiteSpace(heading))
        {
            return heading;
        }

        return CleanText(section.Text, 180);
    }

    private static List<string> FindCtasNear(SourceSnapshotElementItem section, IReadOnlyList<SourceSnapshotElementItem> elements)
    {
        return elements
            .Where(item => item.Tag is "a" or "button"
                && item.Y >= section.Y - 20
                && item.Y <= section.Y + Math.Max(section.Height, 800))
            .Select(item => CleanText(item.Text, 160))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();
    }

    private static string InferSectionRole(SourceSnapshotElementItem? element, IReadOnlyList<string> classes, string heading)
    {
        var text = string.Join(" ", classes.Concat([heading, element?.Text ?? string.Empty]));
        if (text.Contains("intro", StringComparison.OrdinalIgnoreCase)
            || text.Contains("hero", StringComparison.OrdinalIgnoreCase)
            || text.Contains("architecture of new success", StringComparison.OrdinalIgnoreCase))
        {
            return "hero";
        }

        if (text.Contains("impulse", StringComparison.OrdinalIgnoreCase)) return "momentum";
        if (text.Contains("format", StringComparison.OrdinalIgnoreCase)) return "format";
        if (text.Contains("harmony", StringComparison.OrdinalIgnoreCase)) return "harmony";
        if (text.Contains("life", StringComparison.OrdinalIgnoreCase) || text.Contains("location", StringComparison.OrdinalIgnoreCase)) return "location";
        if (text.Contains("people", StringComparison.OrdinalIgnoreCase)) return "people";
        if (text.Contains("status", StringComparison.OrdinalIgnoreCase)) return "status";
        if (element?.Tag == "footer" || text.Contains("footer", StringComparison.OrdinalIgnoreCase)) return "footer";
        return element?.Tag ?? "section";
    }

    private static string InferVisualRole(IReadOnlyList<string> classes, string text)
    {
        var value = string.Join(" ", classes) + " " + text;
        if (value.Contains("full-height", StringComparison.OrdinalIgnoreCase)) return "full-screen section";
        if (value.Contains("sticky", StringComparison.OrdinalIgnoreCase)) return "sticky narrative section";
        if (value.Contains("slider", StringComparison.OrdinalIgnoreCase)) return "slider/gallery section";
        if (value.Contains("background", StringComparison.OrdinalIgnoreCase)) return "image background section";
        if (value.Contains("modal", StringComparison.OrdinalIgnoreCase)) return "modal/off-canvas surface";
        return "content section";
    }

    private static string InferMediaRole(string tag, string className, string url)
    {
        var value = $"{tag} {className} {url}";
        if (value.Contains("vimeo", StringComparison.OrdinalIgnoreCase)
            || value.Contains("youtube", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("iframe", StringComparison.OrdinalIgnoreCase))
        {
            return value.Contains("background", StringComparison.OrdinalIgnoreCase)
                ? "hero video background"
                : "iframe video";
        }

        if (tag.Equals("svg", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("use", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return value.Contains("logo", StringComparison.OrdinalIgnoreCase) || value.Contains("air", StringComparison.OrdinalIgnoreCase)
                ? "logo/svg/preloader"
                : "svg/icon";
        }

        if (value.Contains("slider", StringComparison.OrdinalIgnoreCase)) return "slider image";
        if (value.Contains("placeholder", StringComparison.OrdinalIgnoreCase)) return "placeholder/background";
        if (value.Contains("background", StringComparison.OrdinalIgnoreCase)) return "background";
        if (value.Contains("hero", StringComparison.OrdinalIgnoreCase) || value.Contains("intro", StringComparison.OrdinalIgnoreCase)) return "hero media";
        return tag.Equals("img", StringComparison.OrdinalIgnoreCase) ? "content image" : tag.ToLowerInvariant();
    }

    private static string ResolveFirstUrl(Uri referenceUri, IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in new[] { "src", "href", "xlink:href", "poster" })
        {
            if (attributes.TryGetValue(key, out var value)
                && TryResolve(referenceUri, value, out var resolved))
            {
                return resolved;
            }
        }

        return SplitSrcSet(referenceUri, attributes.GetValueOrDefault("srcset")).FirstOrDefault() ?? string.Empty;
    }

    private static IEnumerable<string> SplitSrcSet(Uri referenceUri, string? srcset)
    {
        if (string.IsNullOrWhiteSpace(srcset))
        {
            yield break;
        }

        foreach (var part in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var url = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (url is not null && TryResolve(referenceUri, url, out var resolved))
            {
                yield return resolved;
            }
        }
    }

    private static bool TryResolve(Uri referenceUri, string value, out string resolved)
    {
        var candidate = WebUtility.HtmlDecode(value ?? string.Empty).Trim();
        var hash = candidate.IndexOf('#');
        if (hash == 0)
        {
            resolved = candidate;
            return true;
        }

        if (candidate.StartsWith("//", StringComparison.Ordinal))
        {
            candidate = $"{referenceUri.Scheme}:{candidate}";
        }

        if (Uri.TryCreate(referenceUri, candidate, out var uri)
            && uri.Scheme is "http" or "https")
        {
            resolved = uri.AbsoluteUri;
            return true;
        }

        resolved = string.Empty;
        return false;
    }

    private static List<string> ExtractBehaviorNames(IReadOnlyDictionary<string, string> attributes)
    {
        return attributes
            .Where(item => IsBehaviorAttribute(item.Key))
            .Select(item => $"{item.Key}={Limit(WebUtility.HtmlDecode(item.Value ?? string.Empty), 120)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static bool IsBehaviorAttribute(string name)
    {
        return name.StartsWith("data-", StringComparison.OrdinalIgnoreCase)
            && (name.Equals("data-plugin", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-parallax-pattern", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-content-animation-animations", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-reveal", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-scroll-section", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-scroll-sticky", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-scroll-snap-point", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-carousel-web-gl-images", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data-iframe-size", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindLikelyJsOwner(string token, IReadOnlyDictionary<string, string> textResourceContents)
    {
        var normalized = token.StartsWith("data-", StringComparison.OrdinalIgnoreCase)
            ? token[5..]
            : token;
        var camel = ToCamelCase(normalized);
        var hit = textResourceContents.FirstOrDefault(pair =>
            pair.Key.Contains(".js", StringComparison.OrdinalIgnoreCase)
            && (pair.Value.Contains(token, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Contains(camel, StringComparison.OrdinalIgnoreCase)));
        return string.IsNullOrWhiteSpace(hit.Key) ? string.Empty : hit.Key;
    }

    private static string ExtractAnimationName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = Regex.Match(value, "\"name\"\\s*:\\s*\"(?<name>[^\"]+)\"", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups["name"].Value : string.Empty;
    }

    private static string SummarizeEffect(string key, string value)
    {
        if (key.Contains("parallax", StringComparison.OrdinalIgnoreCase) || value.Contains("parallax", StringComparison.OrdinalIgnoreCase))
        {
            return "Scroll-linked parallax or section movement.";
        }

        if (key.Contains("sticky", StringComparison.OrdinalIgnoreCase))
        {
            return "Sticky section or pinned scroll behavior.";
        }

        if (key.Contains("content-animation", StringComparison.OrdinalIgnoreCase))
        {
            return "Text/content reveal and hide animation sequence.";
        }

        if (key.Contains("reveal", StringComparison.OrdinalIgnoreCase))
        {
            return "Reveal-on-scroll behavior.";
        }

        if (key.Contains("carousel", StringComparison.OrdinalIgnoreCase))
        {
            return "Carousel or image clip slider behavior.";
        }

        if (key.Contains("iframe-size", StringComparison.OrdinalIgnoreCase))
        {
            return "Responsive iframe sizing behavior.";
        }

        return "Frontend behavior declared through data attributes.";
    }

    private static string SuggestBehaviorRebuild(string key, string value)
    {
        if (key.Contains("parallax", StringComparison.OrdinalIgnoreCase))
        {
            return "Use CSS transforms and IntersectionObserver or scroll timeline for a simpler parallax approximation.";
        }

        if (key.Contains("content-animation", StringComparison.OrdinalIgnoreCase))
        {
            return "Implement staged text reveal/hide transitions with CSS classes and bounded JS.";
        }

        if (key.Contains("carousel", StringComparison.OrdinalIgnoreCase))
        {
            return "Rebuild as a controlled image slider using replacement images and simple state transitions.";
        }

        if (value.Contains("iframe", StringComparison.OrdinalIgnoreCase))
        {
            return "Preserve responsive iframe ratio but replace protected media content.";
        }

        return "Rebuild as progressive enhancement; page must remain readable without exact original JS.";
    }

    private static List<string> ExtractImportantProperties(string css, string selector)
    {
        var escaped = Regex.Escape(selector);
        var regex = new Regex($@"{escaped}[^\{{]*\{{(?<body>.*?)\}}", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        var match = regex.Match(css);
        if (!match.Success)
        {
            return [];
        }

        var body = match.Groups["body"].Value;
        var properties = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(':')
                && (line.Contains("display", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("position", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("grid", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("flex", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("transform", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("transition", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("animation", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("height", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("width", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("font", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("color", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("background", StringComparison.OrdinalIgnoreCase)))
            .Select(line => Limit(line.Trim(), 180))
            .Take(12)
            .ToList();

        return properties.Count > 0 ? properties : [Limit(body.Trim(), 240)];
    }

    private static string SuggestCssRebuild(string className)
    {
        if (className.Contains("sticky", StringComparison.OrdinalIgnoreCase)) return "Preserve sticky/pinned section behavior with CSS sticky and small JS fallbacks.";
        if (className.Contains("hero", StringComparison.OrdinalIgnoreCase) || className.Contains("intro", StringComparison.OrdinalIgnoreCase)) return "Use this class as primary hero layout evidence.";
        if (className.Contains("slider", StringComparison.OrdinalIgnoreCase)) return "Use this class as carousel/slider layout evidence.";
        if (className.Contains("background", StringComparison.OrdinalIgnoreCase)) return "Use replacement background media and preserve scale/crop behavior.";
        return "Use rule as class-level reconstruction evidence, not as a direct copy contract.";
    }

    private static List<string> ExtractKeywordHits(string content, IReadOnlyList<string> keywords)
    {
        return keywords
            .Where(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLikelyMinified(string content)
    {
        if (content.Length < 1000)
        {
            return false;
        }

        var newlineCount = content.Count(ch => ch is '\n');
        return newlineCount < content.Length / 400;
    }

    private static List<string> BuildRebuildSuggestions(
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> responsive,
        IReadOnlyList<SourceSnapshotBehaviorItem> behaviors,
        IReadOnlyList<SourceSnapshotCssRuleMapItem> cssRules,
        IReadOnlyList<SourceSnapshotJsBehaviorReferenceItem> jsReferences)
    {
        var suggestions = new List<string>
        {
            "Use the controlled desktop viewport and first-screen screenshot as the primary layout reference.",
            "Replace original brand images, fonts, and videos with licensed or generated equivalents.",
            "Preserve content hierarchy, section order, calls to action, and responsive behavior intent."
        };

        if (media.Any(item => item.Url.Contains("vimeo", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add("Hero Vimeo background detected: rebuild with approved replacement video or motion background.");
        }

        if (responsive.Count > 0)
        {
            suggestions.Add("Responsive picture/source rules detected: define desktop, tablet, and mobile media slots explicitly.");
        }

        if (behaviors.Any(item => item.Plugin.Contains("parallax", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add("Parallax declarations detected: approximate with lightweight scroll transforms.");
        }

        if (behaviors.Any(item => item.RawDeclaration.Contains("content-animation", StringComparison.OrdinalIgnoreCase)
                || item.Plugin.Contains("content", StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add("contentAnimation declarations detected: preserve staged text reveal rhythm.");
        }

        if (cssRules.Count > 0 && jsReferences.Count > 0)
        {
            suggestions.Add("DOM classes, CSS rules, and JS behavior names have MVP cross-reference evidence.");
        }

        return suggestions;
    }

    private static string ReplacementAdvice(string url, string tag)
    {
        if (url.Contains("vimeo", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            return "Do not reuse the original video; replace with approved motion media while preserving placement and behavior.";
        }

        if (tag.Equals("img", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".webp", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "Use as evidence for slot size/crop only; replace protected imagery.";
        }

        if (url.Contains(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return "Use as icon/logo/preloader evidence only; recreate public-safe vector equivalents.";
        }

        return "Use as structural evidence, not as direct reuse permission.";
    }

    private static string FindNearestSectionKey(SourceSnapshotElementItem element, IReadOnlyList<SourceSnapshotElementItem> elements)
    {
        var section = elements
            .Where(IsSectionLike)
            .Where(item => item.Y <= element.Y + 2)
            .OrderByDescending(item => item.Y)
            .FirstOrDefault();
        if (section is null)
        {
            return string.Empty;
        }

        return InferSectionRole(section, SplitClasses(section.ClassName), CleanText(section.Text, 120));
    }

    private static string InferSectionFromClasses(string? className)
    {
        return InferSectionRole(null, SplitClasses(className), string.Empty);
    }

    private static string FindMediaSection(
        string url,
        IReadOnlyList<SourceSnapshotMediaPlacementItem> media,
        IReadOnlyList<SourceSnapshotSectionMapItem> sections)
    {
        var hit = media.FirstOrDefault(item => item.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        if (hit is null)
        {
            return string.Empty;
        }

        var section = sections.FirstOrDefault(item => SameSection(item.SectionKey, hit.SectionKey));
        return section?.SectionKey ?? hit.SectionKey;
    }

    private static bool SameSection(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return left.Contains(right, StringComparison.OrdinalIgnoreCase)
            || right.Contains(left, StringComparison.OrdinalIgnoreCase)
            || left.Split('-').LastOrDefault()?.Equals(right.Split('-').LastOrDefault(), StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string BuildSelector(SourceSnapshotElementItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Selector))
        {
            return item.Selector;
        }

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            return $"#{item.Id}";
        }

        var classes = item.CssClasses.Count > 0 ? item.CssClasses : SplitClasses(item.ClassName);
        return classes.Count > 0
            ? $"{item.Tag}.{string.Join('.', classes.Take(3))}"
            : item.Tag;
    }

    private static string SelectorFromRawTag(RawTag tag)
    {
        if (tag.Attributes.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            return $"#{id}";
        }

        var classes = SplitClasses(tag.Attributes.GetValueOrDefault("class"));
        return classes.Count > 0
            ? $"{tag.Name}.{string.Join('.', classes.Take(3))}"
            : tag.Name;
    }

    private static string Ratio(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return string.Empty;
        }

        return $"{width / height:0.###}:1";
    }

    private static string BuildWidthHeight(IReadOnlyDictionary<string, string> attributes)
    {
        var width = attributes.GetValueOrDefault("width");
        var height = attributes.GetValueOrDefault("height");
        return string.IsNullOrWhiteSpace(width) && string.IsNullOrWhiteSpace(height)
            ? string.Empty
            : $"{width}x{height}";
    }

    private static List<string> SplitClasses(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return [];
        }

        return className.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();
    }

    private static List<string> NormalizeClasses(IEnumerable<string> classes)
    {
        return classes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();
    }

    private static string CleanText(string value, int maxLength)
    {
        var withoutTags = StripTagsRegex.Replace(value ?? string.Empty, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Limit(CollapseWhitespaceRegex.Replace(decoded, " ").Trim(), maxLength);
    }

    private static string Limit(string? value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string ToCamelCase(string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return value;
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static void AppendItems(StringBuilder builder, IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            builder.AppendLine(item);
        }
    }

    private sealed record RawTag(string Name, Dictionary<string, string> Attributes);
}

internal static class SourceSnapshotEnumerableExtensions
{
    public static IEnumerable<T> DistinctByKey<T>(
        this IEnumerable<T> source,
        Func<T, string> keySelector)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            if (seen.Add(keySelector(item)))
            {
                yield return item;
            }
        }
    }
}
