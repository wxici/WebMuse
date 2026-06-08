using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.ProjectSystem;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class SourceSnapshotService
{
    private const int MaxRawHtmlBytes = 4 * 1024 * 1024;
    private const int MaxTextResourceBytes = 1024 * 1024;
    private const int MaxTotalTextResourceBytes = 8 * 1024 * 1024;
    private const int MaxTextResourceCount = 40;
    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;
    private static readonly Regex TagRegex = new(
        """<(?<tag>link|script|img|source|video|iframe|svg|use)\b(?<attrs>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex AttributeRegex = new(
        """(?<name>[\w:-]+)\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<bare>[^\s"'=<>`]+))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex TitleRegex = new(
        """<title\b[^>]*>(?<value>.*?)</title>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex MetaRegex = new(
        """<meta\b(?<attrs>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex AnchorRegex = new(
        """<a\b(?<attrs>[^>]*)>(?<value>.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex StripTagsRegex = new(
        """<[^>]+>""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex CollapseWhitespaceRegex = new(
        """\s+""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private readonly AppLogger _logger;
    private readonly SourceSnapshotReconstructionAnalyzer _reconstructionAnalyzer = new();

    public SourceSnapshotService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<SourceSnapshotResult> CaptureAsync(
        RebuildProject project,
        string referenceUrl,
        SourceSnapshotRenderedEvidence? renderedEvidence,
        CancellationToken cancellationToken = default)
    {
        return await CaptureAsync(
            project,
            referenceUrl,
            renderedEvidence,
            options: new SourceSnapshotCaptureOptions { KnownTextResources = [] },
            cancellationToken);
    }

    public async Task<SourceSnapshotResult> CaptureAsync(
        RebuildProject project,
        string referenceUrl,
        SourceSnapshotRenderedEvidence? renderedEvidence,
        SourceSnapshotCaptureOptions? options,
        CancellationToken cancellationToken = default)
    {
        var referenceUri = ValidateReferenceUri(referenceUrl);
        var warnings = new List<string>();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawHtml = string.Empty;
        var httpSucceeded = false;
        int? statusCode = null;
        var contentType = string.Empty;

        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 WebRebuildRecorder SourceSnapshot/0.1");

            using var response = await client.GetAsync(
                referenceUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            statusCode = (int)response.StatusCode;
            contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
            httpSucceeded = response.IsSuccessStatusCode;
            CopySafeHeaders(response.Headers, headers);
            CopySafeHeaders(response.Content.Headers, headers);

            var boundedContent = await ReadBoundedContentAsync(
                response.Content,
                cancellationToken);
            rawHtml = boundedContent.Text;
            if (boundedContent.Truncated)
            {
                warnings.Add("Raw HTML truncated at 4MB.");
            }

            if (!httpSucceeded)
            {
                warnings.Add($"HTTP request returned status {statusCode} ({response.ReasonPhrase}).");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            warnings.Add($"HTTP capture failed: {ex.Message}");
            _logger.Warn($"Source Snapshot HTTP capture failed: {ex.Message}");
        }

        return await CaptureCoreAsync(
            project,
            referenceUri,
            rawHtml,
            headers,
            renderedEvidence,
            httpSucceeded,
            statusCode,
            contentType,
            warnings,
            options,
            cancellationToken);
    }

    public Task<SourceSnapshotResult> CaptureFromKnownHtmlAsync(
        RebuildProject project,
        string referenceUrl,
        string rawHtml,
        IReadOnlyDictionary<string, string>? headers,
        SourceSnapshotRenderedEvidence? renderedEvidence,
        CancellationToken cancellationToken = default)
    {
        return CaptureFromKnownHtmlAsync(
            project,
            referenceUrl,
            rawHtml,
            headers,
            renderedEvidence,
            options: null,
            cancellationToken);
    }

    public Task<SourceSnapshotResult> CaptureFromKnownHtmlAsync(
        RebuildProject project,
        string referenceUrl,
        string rawHtml,
        IReadOnlyDictionary<string, string>? headers,
        SourceSnapshotRenderedEvidence? renderedEvidence,
        SourceSnapshotCaptureOptions? options,
        CancellationToken cancellationToken = default)
    {
        var referenceUri = ValidateReferenceUri(referenceUrl);
        var safeHeaders = SanitizeHeaders(headers);
        var contentType = safeHeaders.TryGetValue("content-type", out var value)
            ? value
            : "text/html";
        var warnings = new List<string>();
        var boundedHtml = LimitKnownHtml(rawHtml ?? string.Empty, warnings);

        return CaptureCoreAsync(
            project,
            referenceUri,
            boundedHtml,
            safeHeaders,
            renderedEvidence,
            httpSucceeded: true,
            statusCode: 200,
            contentType,
            warnings,
            options,
            cancellationToken);
    }

    private async Task<SourceSnapshotResult> CaptureCoreAsync(
        RebuildProject project,
        Uri referenceUri,
        string rawHtml,
        IReadOnlyDictionary<string, string> headers,
        SourceSnapshotRenderedEvidence? renderedEvidence,
        bool httpSucceeded,
        int? statusCode,
        string contentType,
        List<string> warnings,
        SourceSnapshotCaptureOptions? options,
        CancellationToken cancellationToken)
    {
        ValidateProject(project);
        cancellationToken.ThrowIfCancellationRequested();

        var diskPaths = GetDiskPaths(project.ProjectDirectory);
        EnsureDirectories(diskPaths);

        var evidence = renderedEvidence ?? new SourceSnapshotRenderedEvidence
        {
            RenderSucceeded = false,
            RenderError = "Rendered evidence was not provided."
        };
        if (!evidence.RenderSucceeded)
        {
            warnings.Add(string.IsNullOrWhiteSpace(evidence.RenderError)
                ? "WebView2 rendered evidence was not available."
                : $"Rendered evidence failed: {evidence.RenderError}");
        }

        var captureOptions = options ?? new SourceSnapshotCaptureOptions();
        var resourceManifest = ExtractResources(referenceUri, rawHtml, evidence, warnings);
        var analysis = BuildAnalysis(referenceUri, rawHtml, evidence, warnings);
        var textCapture = await CaptureTextResourcesAsync(
            project.ProjectDirectory,
            referenceUri,
            resourceManifest,
            captureOptions,
            warnings,
            cancellationToken);
        var reconstructionBundle = _reconstructionAnalyzer.Analyze(
            referenceUri,
            rawHtml,
            evidence,
            resourceManifest,
            textCapture.Resources,
            textCapture.Contents);
        var result = new SourceSnapshotResult
        {
            SnapshotId = $"snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..42],
            ReferenceUrl = referenceUri.AbsoluteUri,
            CreatedAt = DateTime.Now,
            HttpSucceeded = httpSucceeded,
            StatusCode = statusCode,
            ContentType = contentType,
            RenderSucceeded = evidence.RenderSucceeded,
            RenderError = evidence.RenderError,
            Paths = CreateRelativePaths(),
            ResourceManifest = resourceManifest,
            Analysis = analysis,
            CaptureViewport = captureOptions.Viewport,
            TextResources = textCapture.Resources,
            DependencyGraph = reconstructionBundle.DependencyGraph,
            ReconstructionGraph = reconstructionBundle.ReconstructionEvidenceGraph
        };

        await WriteTextAsync(Path.Combine(diskPaths.Raw, "index.html"), rawHtml, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Raw, "response-headers.json"), headers, cancellationToken);
        await WriteTextAsync(Path.Combine(diskPaths.Rendered, "dom.html"), evidence.DomHtml, cancellationToken);
        await WriteTextAsync(Path.Combine(diskPaths.Rendered, "visible-text.txt"), evidence.VisibleText, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Rendered, "viewport.json"), evidence.Viewport, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Rendered, "element-map.json"), evidence.Elements, cancellationToken);
        if (captureOptions.FirstScreenPngBytes is { Length: > 0 })
        {
            await File.WriteAllBytesAsync(
                Path.Combine(diskPaths.Rendered, "first-screen.png"),
                captureOptions.FirstScreenPngBytes,
                cancellationToken);
        }
        else
        {
            warnings.Add("First-screen screenshot was not provided.");
            analysis.Warnings = warnings.Distinct(StringComparer.Ordinal).ToList();
        }

        await WriteJsonAsync(Path.Combine(diskPaths.Resources, "resource-manifest.json"), resourceManifest, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Resources, "css-links.json"), resourceManifest.Css, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Resources, "js-links.json"), resourceManifest.JavaScript, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Resources, "image-links.json"), resourceManifest.Images, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Resources, "font-links.json"), resourceManifest.Fonts, cancellationToken);
        await WriteJsonAsync(
            Path.Combine(diskPaths.Resources, "text-resource-manifest.json"),
            textCapture.Resources,
            cancellationToken);

        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "layout-signals.json"), analysis.LayoutSignals, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "color-signals.json"), analysis.ColorSignals, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "typography-signals.json"), analysis.TypographySignals, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "asset-slot-candidates.json"), analysis.AssetSlotCandidates, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "dependency-graph.json"), reconstructionBundle.DependencyGraph, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "section-map.json"), reconstructionBundle.SectionMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "media-placement-map.json"), reconstructionBundle.MediaPlacementMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "responsive-media-map.json"), reconstructionBundle.ResponsiveMediaMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "behavior-map.json"), reconstructionBundle.BehaviorMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "animation-signal-map.json"), reconstructionBundle.AnimationSignalMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "css-rule-map.json"), reconstructionBundle.CssRuleMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "js-behavior-reference-map.json"), reconstructionBundle.JsBehaviorReferenceMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "reconstruction-evidence-graph.json"), reconstructionBundle.ReconstructionEvidenceGraph, cancellationToken);
        await WriteTextAsync(Path.Combine(diskPaths.Analysis, "ai-reconstruction-brief.md"), reconstructionBundle.AiReconstructionBriefMarkdown, cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Analysis, "source-snapshot-report.json"), result, cancellationToken);
        await WriteTextAsync(
            Path.Combine(diskPaths.Analysis, "source-snapshot-report.md"),
            BuildMarkdownReport(result, evidence.Elements.Count),
            cancellationToken);
        await WriteJsonAsync(Path.Combine(diskPaths.Root, "snapshot-manifest.json"), result, cancellationToken);

        _logger.Info(
            $"Source Snapshot persisted: {result.SnapshotId}, HTTP={result.HttpSucceeded}, Render={result.RenderSucceeded}");
        return result;
    }

    private static SourceSnapshotResourceManifest ExtractResources(
        Uri baseUri,
        string html,
        SourceSnapshotRenderedEvidence evidence,
        List<string> warnings)
    {
        var manifest = new SourceSnapshotResourceManifest();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TagRegex.Matches(html))
        {
            var tag = match.Groups["tag"].Value.ToLowerInvariant();
            var attributes = ParseAttributes(match.Groups["attrs"].Value);

            foreach (var rawUrl in EnumerateResourceUrls(tag, attributes))
            {
                AddResource(manifest, seen, baseUri, rawUrl, tag, attributes, warnings);
            }
        }

        foreach (var element in evidence.Elements)
        {
            if (!string.IsNullOrWhiteSpace(element.Src))
            {
                AddResource(
                    manifest,
                    seen,
                    baseUri,
                    element.Src,
                    $"rendered:{element.Tag}",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    warnings);
            }
        }

        SortResources(manifest);
        return manifest;
    }

    private static IEnumerable<string> EnumerateResourceUrls(
        string tag,
        IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var attribute in new[] { "href", "src", "xlink:href", "poster" })
        {
            if (attributes.TryGetValue(attribute, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }

        if (attributes.TryGetValue("srcset", out var srcset))
        {
            foreach (var url in SplitSrcSetUrls(srcset))
            {
                yield return url;
            }
        }

        if (tag.Equals("use", StringComparison.OrdinalIgnoreCase)
            && attributes.TryGetValue("href", out var useHref))
        {
            yield return useHref;
        }
    }

    private static IEnumerable<string> SplitSrcSetUrls(string srcset)
    {
        foreach (var part in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var url = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(url))
            {
                yield return url;
            }
        }
    }

    private static void AddResource(
        SourceSnapshotResourceManifest manifest,
        HashSet<string> seen,
        Uri baseUri,
        string rawUrl,
        string sourceTag,
        IReadOnlyDictionary<string, string> attributes,
        List<string> warnings)
    {
        var candidate = WebUtility.HtmlDecode(rawUrl).Trim();
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith('#'))
        {
            return;
        }

        if (ContainsSensitiveUrlData(baseUri, candidate))
        {
            warnings.Add("Resource URL with sensitive query data was skipped.");
            return;
        }

        if (!TryResolveResourceUri(baseUri, candidate, out var resourceUri))
        {
            warnings.Add($"Resource URL could not be normalized: {Limit(candidate, 240)}");
            return;
        }

        if (!seen.Add(resourceUri.AbsoluteUri))
        {
            return;
        }

        var kind = ClassifyResource(resourceUri, sourceTag, attributes);
        var item = new SourceSnapshotResourceItem
        {
            Url = resourceUri.AbsoluteUri,
            Kind = kind,
            SourceTag = sourceTag,
            IsSameOrigin = HasSameOrigin(baseUri, resourceUri)
        };

        GetResourceList(manifest, kind).Add(item);
    }

    private static SourceSnapshotAnalysis BuildAnalysis(
        Uri referenceUri,
        string html,
        SourceSnapshotRenderedEvidence evidence,
        List<string> warnings)
    {
        var analysis = new SourceSnapshotAnalysis
        {
            Title = ExtractTitle(html),
            Description = ExtractDescription(html),
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList()
        };

        analysis.Headings = evidence.Elements
            .Where(item => item.Tag is "h1" or "h2" or "h3")
            .Select(item => CleanText(item.Text, 300))
            .Where(value => value.Length > 0)
            .Concat(ExtractElementTexts(html, "h1"))
            .Concat(ExtractElementTexts(html, "h2"))
            .Concat(ExtractElementTexts(html, "h3"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        analysis.Buttons = evidence.Elements
            .Where(item => item.Tag == "button"
                || item.ClassName.Contains("button", StringComparison.OrdinalIgnoreCase)
                || item.ClassName.Contains("btn", StringComparison.OrdinalIgnoreCase))
            .Select(item => CleanText(item.Text, 200))
            .Where(value => value.Length > 0)
            .Concat(ExtractElementTexts(html, "button"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        analysis.Links = ExtractLinks(referenceUri, html, evidence)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();

        analysis.LayoutSignals = BuildLayoutSignals(evidence);
        analysis.ColorSignals = evidence.StyleSamples
            .SelectMany(sample => new[] { sample.Color, sample.BackgroundColor })
            .Select(value => value.Trim())
            .Where(value => value.Length > 0
                && !value.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("rgba(0, 0, 0, 0)", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
        analysis.TypographySignals = evidence.StyleSamples
            .Where(sample => !string.IsNullOrWhiteSpace(sample.FontFamily)
                || !string.IsNullOrWhiteSpace(sample.FontSize)
                || !string.IsNullOrWhiteSpace(sample.FontWeight))
            .Select(sample =>
                $"{sample.FontFamily.Trim()} | {sample.FontSize.Trim()} | weight {sample.FontWeight.Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
        analysis.AssetSlotCandidates = evidence.Elements
            .Where(item => item.Tag is "img" or "video"
                || item.ClassName.Contains("hero", StringComparison.OrdinalIgnoreCase))
            .Select(item =>
            {
                var source = string.IsNullOrWhiteSpace(item.Src) ? item.Text : item.Src;
                return $"{item.Tag} {Math.Round(item.Width)}x{Math.Round(item.Height)}: {Limit(source, 300)}";
            })
            .Where(value => !value.EndsWith(": ", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        return analysis;
    }

    private static List<string> BuildLayoutSignals(SourceSnapshotRenderedEvidence evidence)
    {
        var signals = new List<string>
        {
            $"Viewport {evidence.Viewport.Width}x{evidence.Viewport.Height} at DPR {evidence.Viewport.DevicePixelRatio:0.##}.",
            $"Document scroll height {evidence.Viewport.ScrollHeight}px.",
            $"Captured {evidence.Elements.Count} rendered semantic elements."
        };

        foreach (var tag in new[] { "header", "nav", "main", "section", "article", "footer" })
        {
            var count = evidence.Elements.Count(item => item.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
            {
                signals.Add($"{tag}: {count} rendered element(s).");
            }
        }

        var largeElements = evidence.Elements.Count(item =>
            item.Width >= Math.Max(600, evidence.Viewport.Width * 0.7)
            && item.Height >= 180);
        if (largeElements > 0)
        {
            signals.Add($"Large viewport-spanning regions: {largeElements}.");
        }

        return signals;
    }

    private static IEnumerable<string> ExtractLinks(
        Uri baseUri,
        string html,
        SourceSnapshotRenderedEvidence evidence)
    {
        foreach (var element in evidence.Elements.Where(item => !string.IsNullOrWhiteSpace(item.Href)))
        {
            if (TryResolveResourceUri(baseUri, element.Href, out var resolved))
            {
                yield return resolved.AbsoluteUri;
            }
        }

        foreach (Match match in AnchorRegex.Matches(html))
        {
            var attributes = ParseAttributes(match.Groups["attrs"].Value);
            if (attributes.TryGetValue("href", out var href)
                && TryResolveResourceUri(baseUri, href, out var resolved))
            {
                yield return resolved.AbsoluteUri;
            }
        }
    }

    private static string ExtractTitle(string html)
    {
        var match = TitleRegex.Match(html);
        return match.Success ? CleanText(match.Groups["value"].Value, 300) : string.Empty;
    }

    private static string ExtractDescription(string html)
    {
        foreach (Match match in MetaRegex.Matches(html))
        {
            var attributes = ParseAttributes(match.Groups["attrs"].Value);
            var name = attributes.GetValueOrDefault("name");
            var property = attributes.GetValueOrDefault("property");
            if ((name?.Equals("description", StringComparison.OrdinalIgnoreCase) == true
                    || property?.Equals("og:description", StringComparison.OrdinalIgnoreCase) == true)
                && attributes.TryGetValue("content", out var content))
            {
                return CleanText(content, 500);
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> ExtractElementTexts(string html, string tag)
    {
        var regex = new Regex(
            $"""<{tag}\b[^>]*>(?<value>.*?)</{tag}>""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(2));
        return regex.Matches(html)
            .Select(match => CleanText(match.Groups["value"].Value, 300))
            .Where(value => value.Length > 0)
            .Take(40);
    }

    private static Dictionary<string, string> ParseAttributes(string attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attributes))
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

    private static string ClassifyResource(
        Uri resourceUri,
        string sourceTag,
        IReadOnlyDictionary<string, string> attributes)
    {
        var extension = Path.GetExtension(resourceUri.AbsolutePath).ToLowerInvariant();
        if (extension == ".css"
            || sourceTag.Equals("link", StringComparison.OrdinalIgnoreCase)
                && attributes.GetValueOrDefault("rel")?.Contains("stylesheet", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "css";
        }

        if (extension == ".js"
            || sourceTag.Equals("script", StringComparison.OrdinalIgnoreCase))
        {
            return "javascript";
        }

        if (sourceTag.Equals("iframe", StringComparison.OrdinalIgnoreCase)
            && (resourceUri.Host.Contains("vimeo", StringComparison.OrdinalIgnoreCase)
                || resourceUri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase)
                || resourceUri.AbsoluteUri.Contains("background", StringComparison.OrdinalIgnoreCase)
                || resourceUri.AbsoluteUri.Contains("autoplay", StringComparison.OrdinalIgnoreCase)))
        {
            return "video";
        }

        if (extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".svg" or ".avif"
            || sourceTag.EndsWith(":img", StringComparison.OrdinalIgnoreCase)
            || sourceTag.Equals("img", StringComparison.OrdinalIgnoreCase)
            || sourceTag.Equals("source", StringComparison.OrdinalIgnoreCase)
                && attributes.GetValueOrDefault("type")?.Contains("image", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "image";
        }

        if (extension is ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot"
            || attributes.GetValueOrDefault("as")?.Equals("font", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "font";
        }

        if (extension is ".mp4" or ".webm" or ".mov" or ".m4v"
            || sourceTag.EndsWith(":video", StringComparison.OrdinalIgnoreCase)
            || sourceTag.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        return "other";
    }

    private static List<SourceSnapshotResourceItem> GetResourceList(
        SourceSnapshotResourceManifest manifest,
        string kind)
    {
        return kind switch
        {
            "css" => manifest.Css,
            "javascript" => manifest.JavaScript,
            "image" => manifest.Images,
            "font" => manifest.Fonts,
            "video" => manifest.Videos,
            _ => manifest.Other
        };
    }

    private static void SortResources(SourceSnapshotResourceManifest manifest)
    {
        foreach (var list in new[]
                 {
                     manifest.Css,
                     manifest.JavaScript,
                     manifest.Images,
                     manifest.Fonts,
                     manifest.Videos,
                     manifest.Other
                 })
        {
            list.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Url, right.Url));
        }
    }

    private static bool TryResolveResourceUri(Uri baseUri, string value, out Uri resourceUri)
    {
        var decoded = WebUtility.HtmlDecode(value).Trim();
        if (decoded.StartsWith("//", StringComparison.Ordinal))
        {
            decoded = $"{baseUri.Scheme}:{decoded}";
        }

        if (!Uri.TryCreate(baseUri, decoded, out var resolved)
            || resolved.Scheme is not ("http" or "https")
            || HasSensitiveQuery(resolved))
        {
            resourceUri = null!;
            return false;
        }

        resourceUri = resolved;
        return true;
    }

    private static bool HasSameOrigin(Uri left, Uri right)
    {
        return left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase)
            && left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port;
    }

    private static Uri ValidateReferenceUri(string referenceUrl)
    {
        if (!Uri.TryCreate(referenceUrl?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Source Snapshot requires an absolute HTTP or HTTPS reference URL.");
        }

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new InvalidOperationException("Source Snapshot does not accept URLs containing credentials.");
        }

        if (HasSensitiveQuery(uri))
        {
            throw new InvalidOperationException(
                "Source Snapshot does not accept reference URLs containing token, key, secret, auth, cookie, or session query parameters.");
        }

        return uri;
    }

    private static void ValidateProject(RebuildProject project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
        {
            throw new InvalidOperationException("Source Snapshot requires a project directory.");
        }

        Directory.CreateDirectory(project.ProjectDirectory);
    }

    private static SourceSnapshotDiskPaths GetDiskPaths(string projectDirectory)
    {
        var root = Path.Combine(projectDirectory, ProjectDirectoryV2.SourceSnapshot);
        return new SourceSnapshotDiskPaths
        {
            Root = root,
            Raw = Path.Combine(projectDirectory, ProjectDirectoryV2.SourceSnapshotRaw),
            Rendered = Path.Combine(projectDirectory, ProjectDirectoryV2.SourceSnapshotRendered),
            Resources = Path.Combine(projectDirectory, ProjectDirectoryV2.SourceSnapshotResources),
            Analysis = Path.Combine(projectDirectory, ProjectDirectoryV2.SourceSnapshotAnalysis)
        };
    }

    private static void EnsureDirectories(SourceSnapshotDiskPaths paths)
    {
        Directory.CreateDirectory(paths.Root);
        Directory.CreateDirectory(paths.Raw);
        Directory.CreateDirectory(paths.Rendered);
        Directory.CreateDirectory(paths.Resources);
        Directory.CreateDirectory(Path.Combine(paths.Resources, "text"));
        Directory.CreateDirectory(paths.Analysis);
    }

    private static SourceSnapshotPaths CreateRelativePaths()
    {
        return new SourceSnapshotPaths
        {
            Root = ProjectDirectoryV2.SourceSnapshot,
            RawHtml = Relative(ProjectDirectoryV2.SourceSnapshotRaw, "index.html"),
            ResponseHeaders = Relative(ProjectDirectoryV2.SourceSnapshotRaw, "response-headers.json"),
            RenderedDom = Relative(ProjectDirectoryV2.SourceSnapshotRendered, "dom.html"),
            VisibleText = Relative(ProjectDirectoryV2.SourceSnapshotRendered, "visible-text.txt"),
            Viewport = Relative(ProjectDirectoryV2.SourceSnapshotRendered, "viewport.json"),
            ElementMap = Relative(ProjectDirectoryV2.SourceSnapshotRendered, "element-map.json"),
            FirstScreenPng = Relative(ProjectDirectoryV2.SourceSnapshotRendered, "first-screen.png"),
            ResourceManifest = Relative(ProjectDirectoryV2.SourceSnapshotResources, "resource-manifest.json"),
            TextResourceManifest = Relative(ProjectDirectoryV2.SourceSnapshotResources, "text-resource-manifest.json"),
            DependencyGraph = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "dependency-graph.json"),
            SectionMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "section-map.json"),
            MediaPlacementMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "media-placement-map.json"),
            ResponsiveMediaMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "responsive-media-map.json"),
            BehaviorMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "behavior-map.json"),
            AnimationSignalMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "animation-signal-map.json"),
            CssRuleMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "css-rule-map.json"),
            JsBehaviorReferenceMap = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "js-behavior-reference-map.json"),
            ReconstructionEvidenceGraph = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "reconstruction-evidence-graph.json"),
            AiReconstructionBrief = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "ai-reconstruction-brief.md"),
            ReportMarkdown = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "source-snapshot-report.md"),
            ReportJson = Relative(ProjectDirectoryV2.SourceSnapshotAnalysis, "source-snapshot-report.json")
        };
    }

    private static string Relative(string directory, string fileName)
    {
        return $"{directory.Replace('\\', '/')}/{fileName}";
    }

    private static async Task<BoundedContent> ReadBoundedContentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(MaxRawHtmlBytes);
        var chunk = new byte[81920];
        var total = 0;
        var truncated = false;

        while (total < MaxRawHtmlBytes)
        {
            var requested = Math.Min(chunk.Length, MaxRawHtmlBytes - total);
            var read = await stream.ReadAsync(chunk.AsMemory(0, requested), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            total += read;
        }

        if (total == MaxRawHtmlBytes)
        {
            truncated = await stream.ReadAsync(chunk.AsMemory(0, 1), cancellationToken) > 0;
        }

        var encoding = ResolveEncoding(content.Headers.ContentType);
        return new BoundedContent(encoding.GetString(buffer.ToArray()), truncated);
    }

    private static Encoding ResolveEncoding(MediaTypeHeaderValue? contentType)
    {
        var charset = contentType?.CharSet?.Trim('"');
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
            }
        }

        return Encoding.UTF8;
    }

    private static string LimitKnownHtml(string html, List<string> warnings)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        if (bytes.Length <= MaxRawHtmlBytes)
        {
            return html;
        }

        warnings.Add("Raw HTML truncated at 4MB.");
        return Encoding.UTF8.GetString(bytes, 0, MaxRawHtmlBytes);
    }

    private static void CopySafeHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> source,
        IDictionary<string, string> destination)
    {
        foreach (var header in source)
        {
            var value = string.Join(", ", header.Value);
            if (!IsSensitiveHeader(header.Key) && !ContainsSensitiveValue(value))
            {
                destination[header.Key.ToLowerInvariant()] = value;
            }
        }
    }

    private static Dictionary<string, string> SanitizeHeaders(
        IReadOnlyDictionary<string, string>? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return result;
        }

        foreach (var header in headers)
        {
            if (!IsSensitiveHeader(header.Key) && !ContainsSensitiveValue(header.Value))
            {
                result[header.Key.ToLowerInvariant()] = header.Value;
            }
        }

        return result;
    }

    private static bool IsSensitiveHeader(string name)
    {
        return name.Contains("cookie", StringComparison.OrdinalIgnoreCase)
            || name.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("key", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("session", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSensitiveValue(string value)
    {
        return value.Contains("token=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("key=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("auth=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("cookie=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("session=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("signature=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("credential=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSensitiveUrlData(Uri baseUri, string value)
    {
        var decoded = WebUtility.HtmlDecode(value).Trim();
        if (decoded.StartsWith("//", StringComparison.Ordinal))
        {
            decoded = $"{baseUri.Scheme}:{decoded}";
        }

        return Uri.TryCreate(baseUri, decoded, out var uri) && HasSensitiveQuery(uri);
    }

    private static bool HasSensitiveQuery(Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return false;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var rawName = separator >= 0 ? pair[..separator] : pair;
            var name = Uri.UnescapeDataString(rawName.Replace('+', ' '));
            if (IsSensitiveHeader(name)
                || name.Contains("signature", StringComparison.OrdinalIgnoreCase)
                || name.Contains("credential", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildMarkdownReport(SourceSnapshotResult result, int elementCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Source Snapshot Report");
        builder.AppendLine();
        builder.AppendLine($"- Snapshot ID: {result.SnapshotId}");
        builder.AppendLine($"- Reference URL: {result.ReferenceUrl}");
        builder.AppendLine($"- Created At: {result.CreatedAt:O}");
        builder.AppendLine($"- HTTP succeeded: {result.HttpSucceeded}");
        builder.AppendLine($"- Status code: {result.StatusCode?.ToString() ?? "n/a"}");
        builder.AppendLine($"- Content type: {result.ContentType}");
        builder.AppendLine($"- Render succeeded: {result.RenderSucceeded}");
        builder.AppendLine($"- Render error: {result.RenderError}");
        builder.AppendLine($"- Title: {result.Analysis.Title}");
        builder.AppendLine($"- Description: {result.Analysis.Description}");
        builder.AppendLine();
        builder.AppendLine("## Counts");
        builder.AppendLine();
        builder.AppendLine($"- CSS links: {result.ResourceManifest.Css.Count}");
        builder.AppendLine($"- JS links: {result.ResourceManifest.JavaScript.Count}");
        builder.AppendLine($"- Images: {result.ResourceManifest.Images.Count}");
        builder.AppendLine($"- Fonts: {result.ResourceManifest.Fonts.Count}");
        builder.AppendLine($"- Videos: {result.ResourceManifest.Videos.Count}");
        builder.AppendLine($"- Elements: {elementCount}");
        AppendSection(builder, "Headings", result.Analysis.Headings);
        AppendSection(builder, "Buttons", result.Analysis.Buttons);
        AppendSection(builder, "Layout signals", result.Analysis.LayoutSignals);
        AppendSection(builder, "Color signals", result.Analysis.ColorSignals);
        AppendSection(builder, "Typography signals", result.Analysis.TypographySignals);
        AppendSection(builder, "Asset slot candidates", result.Analysis.AssetSlotCandidates);
        AppendSection(builder, "Warnings", result.Analysis.Warnings);
        builder.AppendLine();
        builder.AppendLine("## AI handoff note");
        builder.AppendLine();
        builder.AppendLine("This snapshot is structured evidence for AI generation. It is not permission to copy protected brand assets.");
        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string title, IReadOnlyCollection<string> values)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        if (values.Count == 0)
        {
            builder.AppendLine("- None.");
            return;
        }

        foreach (var value in values)
        {
            builder.AppendLine($"- {value.Replace("\r", " ").Replace("\n", " ")}");
        }
    }

    private static string CleanText(string value, int maxLength)
    {
        var withoutTags = StripTagsRegex.Replace(value ?? string.Empty, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Limit(CollapseWhitespaceRegex.Replace(decoded, " ").Trim(), maxLength);
    }

    private static string Limit(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static Task WriteTextAsync(
        string path,
        string? content,
        CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(path, content ?? string.Empty, Encoding.UTF8, cancellationToken);
    }

    private static Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(value, JsonOptions),
            Encoding.UTF8,
            cancellationToken);
    }

    private async Task<TextResourceCaptureResult> CaptureTextResourcesAsync(
        string projectDirectory,
        Uri referenceUri,
        SourceSnapshotResourceManifest manifest,
        SourceSnapshotCaptureOptions options,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var resources = new List<SourceSnapshotTextResource>();
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var textDirectory = Path.Combine(projectDirectory, ProjectDirectoryV2.SourceSnapshotResources, "text");
        Directory.CreateDirectory(textDirectory);

        if (options.KnownTextResources is not null)
        {
            var knownIndex = 1;
            foreach (var known in options.KnownTextResources.Take(MaxTextResourceCount))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = known.ContentPreview ?? string.Empty;
                var localRelativePath = string.IsNullOrWhiteSpace(known.LocalRelativePath)
                    ? Relative($"{ProjectDirectoryV2.SourceSnapshotResources}/text", $"{knownIndex:000}_{SafeTextResourceFileName(known.Url, known.Kind)}.txt")
                    : known.LocalRelativePath.Replace('\\', '/');
                var diskPath = Path.Combine(projectDirectory, localRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
                await File.WriteAllTextAsync(diskPath, content, Encoding.UTF8, cancellationToken);
                resources.Add(new SourceSnapshotTextResource
                {
                    Url = known.Url,
                    Kind = string.IsNullOrWhiteSpace(known.Kind) ? InferTextResourceKind(known.Url, known.ContentType) : known.Kind,
                    LocalRelativePath = localRelativePath,
                    Fetched = known.Fetched,
                    StatusCode = known.StatusCode == 0 ? 200 : known.StatusCode,
                    ContentType = string.IsNullOrWhiteSpace(known.ContentType) ? "text/plain" : known.ContentType,
                    ByteLength = Encoding.UTF8.GetByteCount(content),
                    Truncated = known.Truncated,
                    Error = known.Error,
                    ContentPreview = Limit(content, 2000)
                });
                contents[localRelativePath] = content;
                knownIndex++;
            }

            return new TextResourceCaptureResult(resources, contents);
        }

        var candidates = GetTextResourceCandidates(manifest)
            .Where(item => !HasSensitiveQuery(new Uri(item.Url)))
            .Take(MaxTextResourceCount)
            .ToList();

        if (candidates.Count == 0)
        {
            return new TextResourceCaptureResult(resources, contents);
        }

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 WebRebuildRecorder SourceSnapshotText/0.1");

        var totalBytes = 0L;
        var fetchIndex = 1;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totalBytes >= MaxTotalTextResourceBytes)
            {
                warnings.Add("Frontend text-resource capture stopped at 8MB total limit.");
                break;
            }

            var resource = new SourceSnapshotTextResource
            {
                Url = candidate.Url,
                Kind = InferTextResourceKind(candidate.Url, string.Empty),
                LocalRelativePath = Relative(
                    $"{ProjectDirectoryV2.SourceSnapshotResources}/text",
                    $"{fetchIndex:000}_{SafeTextResourceFileName(candidate.Url, candidate.Kind)}.txt")
            };

            try
            {
                using var response = await client.GetAsync(
                    candidate.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                resource.StatusCode = (int)response.StatusCode;
                resource.ContentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;

                if (!response.IsSuccessStatusCode)
                {
                    resource.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    warnings.Add($"Text resource skipped: {candidate.Url} returned {resource.Error}.");
                }
                else if (!IsAllowedTextContentType(candidate.Url, resource.ContentType))
                {
                    resource.Error = $"Unsupported content type: {resource.ContentType}";
                    warnings.Add($"Text resource skipped because content type is not text-like: {candidate.Url}");
                }
                else
                {
                    var remaining = Math.Max(0, MaxTotalTextResourceBytes - totalBytes);
                    var bounded = await ReadBoundedTextResourceAsync(
                        response.Content,
                        Math.Min(MaxTextResourceBytes, (int)Math.Min(int.MaxValue, remaining)),
                        cancellationToken);
                    var bytes = Encoding.UTF8.GetByteCount(bounded.Text);
                    totalBytes += bytes;
                    resource.Fetched = true;
                    resource.ByteLength = bytes;
                    resource.Truncated = bounded.Truncated;
                    resource.Kind = InferTextResourceKind(candidate.Url, resource.ContentType);
                    resource.ContentPreview = Limit(bounded.Text, 2000);
                    if (bounded.Truncated)
                    {
                        warnings.Add($"Text resource truncated at 1MB: {candidate.Url}");
                    }

                    var diskPath = Path.Combine(
                        projectDirectory,
                        resource.LocalRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
                    await File.WriteAllTextAsync(diskPath, bounded.Text, Encoding.UTF8, cancellationToken);
                    contents[resource.LocalRelativePath] = bounded.Text;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                resource.Error = ex.Message;
                warnings.Add($"Text resource capture failed: {candidate.Url} - {ex.Message}");
            }

            resources.Add(resource);
            fetchIndex++;
        }

        return new TextResourceCaptureResult(resources, contents);
    }

    private static IEnumerable<SourceSnapshotResourceItem> GetTextResourceCandidates(SourceSnapshotResourceManifest manifest)
    {
        return manifest.Css
            .Concat(manifest.JavaScript)
            .Concat(manifest.Images.Where(item => Path.GetExtension(new Uri(item.Url).AbsolutePath)
                .Equals(".svg", StringComparison.OrdinalIgnoreCase)))
            .Concat(manifest.Other.Where(item =>
            {
                var extension = Path.GetExtension(new Uri(item.Url).AbsolutePath).ToLowerInvariant();
                return extension is ".json" or ".webmanifest" or ".manifest" or ".map" or ".xml" or ".svg";
            }))
            .Where(item => item.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || item.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    private static async Task<BoundedContent> ReadBoundedTextResourceAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Max(1, maxBytes));
        var chunk = new byte[65536];
        var total = 0;
        var truncated = false;

        while (total < maxBytes)
        {
            var requested = Math.Min(chunk.Length, maxBytes - total);
            var read = await stream.ReadAsync(chunk.AsMemory(0, requested), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            total += read;
        }

        if (total == maxBytes)
        {
            truncated = await stream.ReadAsync(chunk.AsMemory(0, 1), cancellationToken) > 0;
        }

        var encoding = ResolveEncoding(content.Headers.ContentType);
        return new BoundedContent(encoding.GetString(buffer.ToArray()), truncated);
    }

    private static bool IsAllowedTextContentType(string url, string contentType)
    {
        var normalized = contentType.Split(';')[0].Trim().ToLowerInvariant();
        var extension = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        return normalized.StartsWith("text/", StringComparison.Ordinal)
            || normalized.Contains("javascript", StringComparison.Ordinal)
            || normalized.Contains("json", StringComparison.Ordinal)
            || normalized.Contains("xml", StringComparison.Ordinal)
            || normalized.Contains("svg", StringComparison.Ordinal)
            || normalized.Contains("manifest", StringComparison.Ordinal)
            || extension is ".css" or ".js" or ".json" or ".webmanifest" or ".manifest" or ".map" or ".svg" or ".xml";
    }

    private static string InferTextResourceKind(string url, string contentType)
    {
        var extension = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        if (extension == ".css" || contentType.Contains("css", StringComparison.OrdinalIgnoreCase)) return "css";
        if (extension == ".js" || contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase)) return "js";
        if (extension == ".svg" || contentType.Contains("svg", StringComparison.OrdinalIgnoreCase)) return "svg";
        if (extension is ".webmanifest" or ".manifest" || contentType.Contains("manifest", StringComparison.OrdinalIgnoreCase)) return "manifest";
        if (extension == ".map") return "source-map";
        if (extension == ".json" || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)) return "json";
        return "other-text";
    }

    private static string SafeTextResourceFileName(string url, string kind)
    {
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
        var name = uri is null
            ? "resource"
            : Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = uri?.Host ?? "resource";
        }

        name = WebUtility.UrlDecode(name);
        var safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray());
        var suffix = string.IsNullOrWhiteSpace(kind) ? "text" : kind.Replace("javascript", "js");
        if (!safe.Contains('.', StringComparison.Ordinal))
        {
            safe = $"{safe}.{suffix}";
        }

        return Limit(safe, 80);
    }

    private sealed class SourceSnapshotDiskPaths
    {
        public string Root { get; set; } = string.Empty;
        public string Raw { get; set; } = string.Empty;
        public string Rendered { get; set; } = string.Empty;
        public string Resources { get; set; } = string.Empty;
        public string Analysis { get; set; } = string.Empty;
    }

    private sealed record TextResourceCaptureResult(
        List<SourceSnapshotTextResource> Resources,
        Dictionary<string, string> Contents);

    private sealed record BoundedContent(string Text, bool Truncated);
}
