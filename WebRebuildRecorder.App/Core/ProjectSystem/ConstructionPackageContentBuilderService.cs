using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;

namespace WebRebuildRecorder.App.Core.ProjectSystem;

public sealed class ConstructionPackageContentBuilderService
{
    public async Task<ConstructionPackageContentBuildResult> BuildAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        var root = ProjectPackagePathHelpers.ValidateProjectRoot(projectRoot);
        var result = new ConstructionPackageContentBuildResult();
        var contextRoot = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            root,
            ConstructionPackageContextSchema.ContextRootRelativePath,
            "construction package context root");
        Directory.CreateDirectory(contextRoot);

        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.ProjectBriefRelativePath,
            BuildProjectBriefAsync(root, result.Warnings, cancellationToken),
            cancellationToken);
        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.ObservationSummaryRelativePath,
            BuildObservationSummaryAsync(root, result.Warnings, cancellationToken),
            cancellationToken);
        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.AssetIndexRelativePath,
            BuildAssetIndexAsync(root, result.Warnings, cancellationToken),
            cancellationToken);
        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.ThemeSummaryRelativePath,
            BuildThemeSummaryAsync(root, result.Warnings, cancellationToken),
            cancellationToken);
        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.ContentMapSummaryRelativePath,
            BuildContentMapSummaryAsync(root, result.Warnings, cancellationToken),
            cancellationToken);
        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.ConstraintsRelativePath,
            Task.FromResult(BuildConstraints()),
            cancellationToken);
        await WriteContextFileAsync(
            root,
            ConstructionPackageContextSchema.AcceptanceChecklistRelativePath,
            BuildAcceptanceChecklistAsync(root, result.Warnings, cancellationToken),
            cancellationToken);

        result.Files = await WritePackageIndexAsync(root, result.Warnings, cancellationToken);
        await UpdateConstructionPackageAsync(root, result, cancellationToken);
        return result;
    }

    private static async Task WriteContextFileAsync(
        string projectRoot,
        string relativePath,
        Task<string> contentTask,
        CancellationToken cancellationToken)
    {
        var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, "construction context file");
        if (!normalized.StartsWith(ConstructionPackageContextSchema.ContextRootRelativePath + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Construction context output must stay under {ConstructionPackageContextSchema.ContextRootRelativePath}: {relativePath}");
        }

        var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, "construction context file");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, await contentTask, Encoding.UTF8, cancellationToken);
    }

    private static async Task<List<ConstructionPackageContextFileItem>> WritePackageIndexAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var files = new List<ConstructionPackageContextFileItem>();
        foreach (var definition in ConstructionPackageContextSchema.ContextFiles
                     .Where(file => file.RelativePath != ConstructionPackageContextSchema.PackageIndexRelativePath))
        {
            files.Add(CreateIndexItem(projectRoot, definition.RelativePath, definition.Kind, now));
        }

        var index = new ConstructionPackageContextIndex
        {
            CreatedAt = now,
            Files = files,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };

        var indexPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(
            projectRoot,
            ConstructionPackageContextSchema.PackageIndexRelativePath,
            "construction package index");
        await using (var stream = File.Create(indexPath))
        {
            await JsonSerializer.SerializeAsync(stream, index, WrbJsonOptions.Default, cancellationToken);
        }

        index.Files.Add(CreateIndexItem(
            projectRoot,
            ConstructionPackageContextSchema.PackageIndexRelativePath,
            "packageIndex",
            now));
        await using (var stream = File.Create(indexPath))
        {
            await JsonSerializer.SerializeAsync(stream, index, WrbJsonOptions.Default, cancellationToken);
        }

        return index.Files;
    }

    private static ConstructionPackageContextFileItem CreateIndexItem(
        string projectRoot,
        string relativePath,
        string kind,
        DateTimeOffset createdAt)
    {
        var normalized = ProjectPackagePathHelpers.NormalizeProjectRelativePath(projectRoot, relativePath, "construction context index file");
        var fullPath = ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, normalized, "construction context index file");
        var info = new FileInfo(fullPath);
        return new ConstructionPackageContextFileItem
        {
            RelativePath = normalized,
            Kind = kind,
            Sha256 = ProjectPackagePathHelpers.ComputeSha256(fullPath),
            SizeBytes = info.Length,
            CreatedAt = createdAt
        };
    }

    private static async Task<string> BuildProjectBriefAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Project Brief");
        builder.AppendLine();
        try
        {
            var manifest = await new ProjectManifestService().LoadAsync(projectRoot, cancellationToken);
            builder.AppendLine($"- Project ID: `{manifest.ProjectId}`");
            builder.AppendLine($"- Project name: {EmptyFallback(manifest.ProjectName)}");
            builder.AppendLine($"- Reference URL: {EmptyFallback(manifest.ReferenceUrl)}");
            builder.AppendLine($"- Project state: `{manifest.State}`");
            builder.AppendLine("- Current stage: P1.3 construction context generation.");
            builder.AppendLine("- Goal: prepare a structured, brand-led static website reconstruction context from local project data.");
            builder.AppendLine("- Language boundary: describe the work as branded reconstruction or redesign, not as clone, copy, or site duplication.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or InvalidOperationException or IOException)
        {
            warnings.Add($"project.wrbproj could not be loaded for project brief ({ex.GetType().Name}).");
            builder.AppendLine("- Project ID: unavailable");
            builder.AppendLine("- Project name: unavailable");
            builder.AppendLine("- Reference URL: unavailable");
            builder.AppendLine("- Project state: unavailable");
            builder.AppendLine("- Current stage: P1.3 construction context generation.");
            builder.AppendLine("- Goal: prepare a structured, brand-led static website reconstruction context from available local project data.");
        }

        return builder.ToString();
    }

    private static async Task<string> BuildObservationSummaryAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Observation Summary");
        builder.AppendLine();
        try
        {
            var manifest = await new ObservationPackageService().LoadAsync(projectRoot, cancellationToken);
            builder.AppendLine("## Artifacts");
            foreach (var artifact in manifest.Artifacts)
            {
                builder.AppendLine($"- `{artifact.ArtifactId}` | {artifact.Kind} | `{artifact.RelativePath}` | {EmptyFallback(artifact.Note)}");
            }

            builder.AppendLine();
            builder.AppendLine("## Sections");
            foreach (var section in manifest.Sections)
            {
                builder.AppendLine($"- `{section.SectionId}` | {section.SectionType} | {section.DisplayName} | {section.VisualIntent}");
            }

            builder.AppendLine();
            builder.AppendLine("## Interactions");
            foreach (var interaction in manifest.Interactions)
            {
                builder.AppendLine($"- `{interaction.InteractionId}` | {interaction.Trigger} | {interaction.TargetHint} | {interaction.ObservedEffect} | {interaction.Confidence}");
            }

            builder.AppendLine();
            builder.AppendLine("## Findings");
            foreach (var finding in manifest.Findings)
            {
                builder.AppendLine($"- `{finding.FindingId}` | {finding.Category} | {finding.Summary} | {finding.Confidence}");
            }

            AppendWarnings(builder, manifest.Warnings);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or InvalidOperationException or IOException)
        {
            warnings.Add($"Observation package could not be loaded ({ex.GetType().Name}).");
            builder.AppendLine("- Observation package is unavailable; continue in draft mode.");
        }

        return builder.ToString();
    }

    private static async Task<string> BuildAssetIndexAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Asset Index");
        builder.AppendLine();
        try
        {
            var manifest = await new AssetsManifestService().LoadAsync(projectRoot, cancellationToken);
            if (manifest.Assets.Count == 0)
            {
                builder.AppendLine("- No assets are registered yet.");
            }

            foreach (var asset in manifest.Assets)
            {
                builder.AppendLine($"- `{asset.AssetId}` | kind: {asset.Kind} | role: {asset.Role} | path: `{asset.RelativePath}` | sourceType: {asset.SourceType} | userProvided: {asset.IsUserProvided} | aiGenerated: {asset.IsAiGenerated} | approvedForExport: {asset.IsApprovedForExport} | tags: {string.Join(", ", asset.Tags)}");
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or InvalidOperationException or IOException)
        {
            warnings.Add($"Assets manifest could not be loaded ({ex.GetType().Name}).");
            builder.AppendLine("- Assets manifest is unavailable; continue in draft mode.");
        }

        return builder.ToString();
    }

    private static async Task<string> BuildThemeSummaryAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Theme Summary");
        builder.AppendLine();
        try
        {
            var manifest = await new ThemeManifestService().LoadAsync(projectRoot, cancellationToken);
            builder.AppendLine("## Current Palette");
            builder.AppendLine($"- `{manifest.CurrentPalette.PaletteId}` | {manifest.CurrentPalette.Name} | source: {manifest.CurrentPalette.Source}");
            foreach (var color in manifest.CurrentPalette.Colors)
            {
                builder.AppendLine($"  - {color.Role}: `{color.Hex}` ({color.Note})");
            }

            builder.AppendLine();
            builder.AppendLine($"- Candidate palettes: {manifest.CandidatePalettes.Count}");
            builder.AppendLine($"- Typography: display `{manifest.Typography.DisplayFont}`, body `{manifest.Typography.BodyFont}`, base {manifest.Typography.BaseFontSize}, heading scale {manifest.Typography.HeadingScale}");
            builder.AppendLine($"- Spacing: section gap {manifest.Spacing.SectionGap}, max width {manifest.Spacing.ContentMaxWidth}, radius {manifest.Spacing.BorderRadius}");
            builder.AppendLine($"- Visual tone: brightness {manifest.VisualTone.Brightness}, contrast {manifest.VisualTone.Contrast}, saturation {manifest.VisualTone.Saturation}, mood {manifest.VisualTone.Mood}");
            builder.AppendLine($"- Notes: {EmptyFallback(manifest.Notes)}");
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or InvalidOperationException or IOException)
        {
            warnings.Add($"Theme manifest could not be loaded ({ex.GetType().Name}).");
            builder.AppendLine("- Theme manifest is unavailable; continue in draft mode.");
        }

        return builder.ToString();
    }

    private static async Task<string> BuildContentMapSummaryAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Content Map Summary");
        builder.AppendLine();
        try
        {
            var contentMap = await new ContentMapService().LoadAsync(projectRoot, cancellationToken);
            foreach (var page in contentMap.Pages)
            {
                builder.AppendLine($"## Page `{page.PageId}`");
                builder.AppendLine($"- Route: `{page.Route}`");
                builder.AppendLine($"- Title: {page.Title}");
                foreach (var section in page.Sections)
                {
                    builder.AppendLine($"- Section `{section.SectionId}` | {section.SectionType} | tune: `{section.DataTuneId}` | {section.DisplayName}");
                    foreach (var element in section.Elements)
                    {
                        builder.AppendLine($"  - Element `{element.ElementId}` | {element.ElementType} | tune: `{element.DataTuneId}` | source: {element.SourceField} | basic editable: {element.IsUserEditableInBasicMode}");
                    }
                }
            }

            var dataTuneIds = contentMap.Pages
                .SelectMany(page => page.Sections)
                .Select(section => section.DataTuneId)
                .Concat(contentMap.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Elements).Select(element => element.DataTuneId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            builder.AppendLine();
            builder.AppendLine("## DataTuneId Values");
            foreach (var id in dataTuneIds)
            {
                builder.AppendLine($"- `{id}`");
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or InvalidOperationException or IOException)
        {
            warnings.Add($"Content map could not be loaded ({ex.GetType().Name}).");
            builder.AppendLine("- Content map is unavailable; continue in draft mode.");
        }

        return builder.ToString();
    }

    private static string BuildConstraints()
    {
        return """
        # Constraints

        - Use the reference site only for high-level style observation; do not copy proprietary logos, copy, images, or distinctive graphic assets.
        - Use user brand materials from this project before any reference-site material.
        - Prefer project-relative paths.
        - Do not write `.git`.
        - Do not write system directories.
        - Do not write user credential directories.
        - Do not write software installation directories.
        - Do not call external APIs.
        - Do not execute Codex CLI.
        - Do not generate zip archives.
        - This round only generates construction context; it does not generate a real website.
        """;
    }

    private static async Task<string> BuildAcceptanceChecklistAsync(
        string projectRoot,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Acceptance Checklist");
        builder.AppendLine();
        foreach (var relativePath in new[]
                 {
                     WrbProjectSchema.FileName,
                     AssetsManifestSchema.RelativePath,
                     ThemeManifestSchema.RelativePath,
                     ContentMapSchema.RelativePath,
                     ObservationPackageSchema.RelativePath,
                     ConstructionPackageSchema.RelativePath,
                     CodexTaskPackageSchema.RelativePath,
                     CodexTaskPackageSchema.InstructionsRelativePath
                 })
        {
            var exists = File.Exists(ProjectPackagePathHelpers.ResolveRelativeFilePath(projectRoot, relativePath, "acceptance checklist file"));
            builder.AppendLine($"- [{(exists ? "x" : " ")}] `{relativePath}` exists");
        }

        var scan = await new SecretAndLocalPathScanService().ScanProjectAsync(projectRoot, cancellationToken);
        builder.AppendLine($"- [{(scan.IsOk ? "x" : " ")}] secret scan passes");
        if (!scan.IsOk)
        {
            warnings.Add("Secret/local-path scan reported blocking findings while building acceptance checklist.");
        }

        builder.AppendLine("- [x] sandbox path validation passes for generated context paths");
        return builder.ToString();
    }

    private static async Task UpdateConstructionPackageAsync(
        string projectRoot,
        ConstructionPackageContentBuildResult result,
        CancellationToken cancellationToken)
    {
        var service = new ConstructionPackageService();
        var manifest = File.Exists(ConstructionPackageService.GetManifestPath(projectRoot))
            ? await service.LoadAsync(projectRoot, cancellationToken)
            : await service.CreateNewAsync(projectRoot, cancellationToken: cancellationToken);

        foreach (var file in result.Files)
        {
            UpsertInput(
                manifest,
                new ConstructionInputItem
                {
                    InputId = "context-" + Path.GetFileNameWithoutExtension(file.RelativePath).Replace('.', '-'),
                    Kind = file.Kind,
                    RelativePath = file.RelativePath,
                    Description = $"Generated P1.3 construction context file: {file.RelativePath}",
                    Required = true
                });
        }

        foreach (var warning in result.Warnings)
        {
            var normalized = "Context builder warning: " + warning;
            if (!manifest.Warnings.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                manifest.Warnings.Add(normalized);
            }
        }

        await service.SaveAsync(projectRoot, manifest, cancellationToken);
    }

    private static void UpsertInput(ConstructionPackageManifest manifest, ConstructionInputItem item)
    {
        var index = manifest.Inputs.FindIndex(input =>
            string.Equals(input.InputId, item.InputId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            manifest.Inputs[index] = item;
        }
        else
        {
            manifest.Inputs.Add(item);
        }
    }

    private static void AppendWarnings(StringBuilder builder, IReadOnlyCollection<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("## Warnings");
        foreach (var warning in warnings)
        {
            builder.AppendLine($"- {warning}");
        }
    }

    private static string EmptyFallback(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unavailable" : value.Trim();
    }
}
