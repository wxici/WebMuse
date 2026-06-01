using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class AiSiteWorkflowService
{
    private const int MaxReferenceFramesForGpt = 50;
    private const int MaxAssetsForGpt = 30;
    private const int MaxCodexReferenceFrames = 60;
    private const int MaxCodexAssets = 40;

    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;

    private readonly AppLogger _logger;

    public AiSiteWorkflowService(AppLogger logger)
    {
        _logger = logger;
    }

    public void EnsureAiSiteProjectStructure(RebuildProject project)
    {
        var directories = new[]
        {
            project.SourceAssetsDirectory,
            project.ImportedAssetsDirectory,
            Path.Combine(project.ImportedAssetsDirectory, "images"),
            Path.Combine(project.ImportedAssetsDirectory, "videos"),
            Path.Combine(project.ImportedAssetsDirectory, "logos"),
            Path.Combine(project.ImportedAssetsDirectory, "fonts"),
            Path.Combine(project.ImportedAssetsDirectory, "documents"),
            Path.Combine(project.ImportedAssetsDirectory, "raw"),
            project.AssetThumbnailsDirectory,
            project.RunsDirectory
        };

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(project.AssetManifestPath))
        {
            SaveAssetManifest(project, new AssetManifest
            {
                ProjectName = project.ProjectName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }
    }

    public AssetManifest LoadAssetManifest(RebuildProject project)
    {
        EnsureAiSiteProjectStructure(project);
        if (!File.Exists(project.AssetManifestPath))
        {
            return new AssetManifest { ProjectName = project.ProjectName };
        }

        using var stream = File.OpenRead(project.AssetManifestPath);
        return JsonSerializer.Deserialize<AssetManifest>(stream, JsonOptions)
            ?? new AssetManifest { ProjectName = project.ProjectName };
    }

    public async Task<AssetManifest> ImportAssetsAsync(RebuildProject project, string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException("用户素材目录不存在。");
        }

        EnsureAiSiteProjectStructure(project);
        var manifest = LoadAssetManifest(project);
        var existingHashes = manifest.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Sha256))
            .Select(asset => asset.Sha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var hash = await ComputeSha256Async(source);
            if (existingHashes.Contains(hash))
            {
                continue;
            }

            var type = ClassifyAsset(source);
            var id = CreateNextAssetId(manifest, type);
            var destinationDirectory = Path.Combine(project.ImportedAssetsDirectory, ToAssetFolder(type));
            Directory.CreateDirectory(destinationDirectory);
            var destination = GetUniqueDestination(destinationDirectory, Path.GetFileName(source));
            File.Copy(source, destination, overwrite: false);

            var asset = new ProjectAsset
            {
                Id = id,
                File = ToRelative(project.ProjectDirectory, destination),
                Type = type,
                Extension = Path.GetExtension(destination).ToLowerInvariant(),
                SizeBytes = new FileInfo(destination).Length,
                Sha256 = hash,
                Status = "imported"
            };

            if (type is "image" or "logo")
            {
                TryReadImageInfo(destination, asset);
                TryCreateThumbnail(project, destination, asset);
            }

            manifest.Assets.Add(asset);
            existingHashes.Add(hash);
        }

        manifest.ProjectName = project.ProjectName;
        manifest.UpdatedAt = DateTime.Now;
        SaveAssetManifest(project, manifest);
        _logger.Info($"Imported assets. Manifest now contains {manifest.Assets.Count} assets.");
        return manifest;
    }

    public string CreateRunId(RebuildProject project)
    {
        var baseRunId = $"run-{DateTime.Now:yyyyMMdd-HHmmss}";
        var runId = baseRunId;
        for (var index = 2; Directory.Exists(GetRunDirectory(project, runId)); index++)
        {
            runId = $"{baseRunId}-{index:00}";
        }

        EnsureRunStructure(project, runId);
        project.LastRunId = runId;
        return runId;
    }

    public string GetOrCreateRunId(RebuildProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.LastRunId)
            && Directory.Exists(GetRunDirectory(project, project.LastRunId)))
        {
            EnsureRunStructure(project, project.LastRunId);
            return project.LastRunId;
        }

        return CreateRunId(project);
    }

    public async Task SaveUserIntentAsync(RebuildProject project, string runId, UserIntent intent)
    {
        EnsureRunStructure(project, runId);
        var observationDirectory = GetObservationDirectory(project, runId);
        var intentPath = Path.Combine(observationDirectory, "user-intent.md");
        var intentJsonPath = Path.Combine(observationDirectory, "user-intent.json");
        await File.WriteAllTextAsync(
            intentPath,
            BuildUserIntentMarkdown(intent) + Environment.NewLine + Environment.NewLine + BuildUserIntentAssetReferenceMarkdown(project),
            Encoding.UTF8);
        await WriteJsonAsync(intentJsonPath, intent);

        await UpdateRunManifestAsync(project, runId, manifest =>
        {
            manifest.UserIntent = intent;
            manifest.UserIntentPath = ToRelative(project.ProjectDirectory, intentPath);
        });
    }

    public async Task<UserIntent?> LoadUserIntentAsync(RebuildProject project, string runId)
    {
        var path = Path.Combine(GetObservationDirectory(project, runId), "user-intent.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<UserIntent>(await File.ReadAllTextAsync(path, Encoding.UTF8), JsonOptions);
    }

    public async Task<GptAnalysisPackageResult> CreateGptAnalysisPackageAsync(
        RebuildProject project,
        string runId,
        UserIntent intent,
        FrameExtractResult? frameResult,
        string? observationMarkdownPath,
        string? recordingFilePath)
    {
        if (!intent.IsValid)
        {
            intent = UserIntent.CreateEmptyFallback();
        }

        EnsureRunStructure(project, runId);
        await SaveUserIntentAsync(project, runId, intent);

        var observationDirectory = GetObservationDirectory(project, runId);
        var gptDirectory = GetGptPackageDirectory(project, runId);
        var referenceScreenshotsDirectory = Path.Combine(gptDirectory, "reference-screenshots");
        var selectedAssetsDirectory = Path.Combine(gptDirectory, "selected-assets");
        var videoSnippetsDirectory = Path.Combine(gptDirectory, "video-snippets");
        Directory.CreateDirectory(referenceScreenshotsDirectory);
        Directory.CreateDirectory(selectedAssetsDirectory);
        Directory.CreateDirectory(videoSnippetsDirectory);

        await EnsureObservationSummaryFilesAsync(project, observationDirectory);

        if (!string.IsNullOrWhiteSpace(recordingFilePath) && File.Exists(recordingFilePath))
        {
            var videoDirectory = Path.Combine(observationDirectory, "video");
            Directory.CreateDirectory(videoDirectory);
            CopyFileIfExists(recordingFilePath, Path.Combine(videoDirectory, Path.GetFileName(recordingFilePath)));
        }

        if (!string.IsNullOrWhiteSpace(observationMarkdownPath) && File.Exists(observationMarkdownPath))
        {
            CopyFileIfExists(observationMarkdownPath, Path.Combine(observationDirectory, "observation.md"));
        }

        var copiedFrames = CopyReferenceFrames(project, frameResult, observationDirectory, referenceScreenshotsDirectory);
        var assetManifest = LoadAssetManifest(project);
        var selectedAssets = SelectAssetsForGpt(project, assetManifest, selectedAssetsDirectory);
        var gptAssetManifestPath = Path.Combine(gptDirectory, "asset-manifest-for-gpt.json");
        await WriteJsonAsync(gptAssetManifestPath, new
        {
            projectName = project.ProjectName,
            referenceUrl = project.ReferenceUrl,
            runId,
            generatedAt = DateTime.Now,
            selectionRule = "Prefer image/logo thumbnails and lightweight representative assets. Full source assets remain in source-assets/imported.",
            assets = selectedAssets
        });

        CopyFileIfExists(Path.Combine(observationDirectory, "user-intent.md"), Path.Combine(gptDirectory, "user-intent.md"));
        CopyFileIfExists(Path.Combine(observationDirectory, "dom-summary.json"), Path.Combine(gptDirectory, "dom-summary.json"));
        CopyFileIfExists(Path.Combine(observationDirectory, "css-summary.json"), Path.Combine(gptDirectory, "css-summary.json"));
        CopyFileIfExists(Path.Combine(observationDirectory, "interactive-targets.md"), Path.Combine(gptDirectory, "interactive-targets.md"));
        CopyFileIfExists(Path.Combine(observationDirectory, "interactive-targets.json"), Path.Combine(gptDirectory, "interactive-targets.json"));
        CopyUserIntentAssetsIntoGptPackage(project, gptDirectory);
        await WriteJsonAsync(Path.Combine(gptDirectory, "manifest.json"), new
        {
            project.ProjectName,
            project.ReferenceUrl,
            runId,
            generatedAt = DateTime.Now,
            userIntent = intent,
            observation = new
            {
                referenceScreenshots = "reference-screenshots/",
                videoSnippets = "video-snippets/",
                domSummary = "dom-summary.json",
                cssSummary = "css-summary.json",
                interactiveTargets = "interactive-targets.md"
            },
            assets = new
            {
                selectedAssets = "selected-assets/",
                manifest = "asset-manifest-for-gpt.json",
                userIntentAssets = "user-intent-assets.md",
                userIntentImages = "user-intent-images/"
            }
        });
        await File.WriteAllTextAsync(
            Path.Combine(videoSnippetsDirectory, "README.md"),
            "视频/GIF 片段为后续版本预留。本包请优先使用 reference-screenshots/ 以及 observation/frames 中复制的关键帧。",
            Encoding.UTF8);

        var promptPath = Path.Combine(gptDirectory, "prompt.md");
        var prompt = BuildGptPrompt(project, runId, intent, copiedFrames, selectedAssets.Count);
        await File.WriteAllTextAsync(promptPath, prompt, Encoding.UTF8);

        var zipPath = Path.Combine(gptDirectory, "package.zip");
        CreateZipFromDirectory(gptDirectory, zipPath);

        var result = new GptAnalysisPackageResult
        {
            RunId = runId,
            ZipPath = zipPath,
            PromptPath = promptPath,
            AssetManifestForGptPath = gptAssetManifestPath,
            SelectedAssetCount = selectedAssets.Count,
            ReferenceFrameCount = copiedFrames,
            ZipSizeBytes = new FileInfo(zipPath).Length
        };

        await UpdateRunManifestAsync(project, runId, manifest =>
        {
            manifest.GptPackageZipPath = ToRelative(project.ProjectDirectory, zipPath);
            manifest.UserIntent = intent;
        });

        _logger.Info($"GPT 分析包已生成：{zipPath}");
        return result;
    }

    public async Task<AssetRequirementReport> ImportGptAnalysisAsync(RebuildProject project, string runId, string analysisText)
    {
        if (string.IsNullOrWhiteSpace(analysisText))
        {
            throw new InvalidOperationException("GPT 分析文本为空。");
        }

        EnsureRunStructure(project, runId);
        var outputDirectory = GetAiOutputDirectory(project, runId);
        var rawPath = Path.Combine(outputDirectory, "gpt-analysis-raw.md");
        await File.WriteAllTextAsync(rawPath, analysisText, Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "analysis.md"), analysisText, Encoding.UTF8);

        var report = ParseAssetRequirements(analysisText);
        var reportPath = Path.Combine(outputDirectory, "asset-requirements.json");
        await WriteJsonAsync(reportPath, report);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "asset-compare.md"), BuildAssetCompareMarkdown(report), Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "codex-task-draft.md"), BuildCodexTaskDraft(project, runId, report), Encoding.UTF8);

        _logger.Info($"GPT analysis imported: {rawPath}");
        return report;
    }

    public async Task<AssetRequirementReport?> TryLoadAssetRequirementReportAsync(RebuildProject project, string runId)
    {
        var path = Path.Combine(GetAiOutputDirectory(project, runId), "asset-requirements.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AssetRequirementReport>(await File.ReadAllTextAsync(path, Encoding.UTF8), JsonOptions);
    }

    public async Task<CodexPackageResult> CreateFinalCodexPackageAsync(
        RebuildProject project,
        string runId,
        bool useFallbackStrategy)
    {
        EnsureRunStructure(project, runId);
        var codexDirectory = GetCodexPackageDirectory(project, runId);
        var outputDirectory = GetAiOutputDirectory(project, runId);
        var gptDirectory = GetGptPackageDirectory(project, runId);
        var selectedAssetsTarget = Path.Combine(codexDirectory, "selected-assets");
        var referenceScreenshotsTarget = Path.Combine(codexDirectory, "reference-screenshots");
        var gifsTarget = Path.Combine(codexDirectory, "gifs");
        Directory.CreateDirectory(selectedAssetsTarget);
        Directory.CreateDirectory(referenceScreenshotsTarget);
        Directory.CreateDirectory(gifsTarget);

        var report = await TryLoadAssetRequirementReportAsync(project, runId) ?? new AssetRequirementReport();
        var manifest = LoadAssetManifest(project);
        var finalTaskPath = Path.Combine(codexDirectory, "final-codex-task.md");
        await File.WriteAllTextAsync(finalTaskPath, BuildFinalCodexTask(project, runId, manifest, report, useFallbackStrategy), Encoding.UTF8);
        await WriteJsonAsync(Path.Combine(codexDirectory, "assets-manifest.json"), manifest);

        CopyFileIfExists(Path.Combine(outputDirectory, "analysis.md"), Path.Combine(codexDirectory, "analysis.md"));
        CopyFileIfExists(Path.Combine(outputDirectory, "asset-compare.md"), Path.Combine(codexDirectory, "asset-compare.md"));
        CopyFileIfExists(Path.Combine(outputDirectory, "asset-requirements.json"), Path.Combine(codexDirectory, "asset-requirements.json"));
        CopyFileIfExists(Path.Combine(GetObservationDirectory(project, runId), "user-intent.md"), Path.Combine(codexDirectory, "user-intent.md"));
        CopyDirectoryFiles(Path.Combine(gptDirectory, "selected-assets"), selectedAssetsTarget, MaxCodexAssets);
        CopyDirectoryFiles(Path.Combine(gptDirectory, "reference-screenshots"), referenceScreenshotsTarget, MaxCodexReferenceFrames);
        CopyDirectoryFiles(Path.Combine(GetObservationDirectory(project, runId), "gifs"), gifsTarget, maxFiles: 20);
        await File.WriteAllTextAsync(Path.Combine(codexDirectory, "file-tree.txt"), BuildFileTree(codexDirectory), Encoding.UTF8);

        var zipPath = Path.Combine(codexDirectory, "package.zip");
        CreateZipFromDirectory(codexDirectory, zipPath);

        await UpdateRunManifestAsync(project, runId, manifestRecord =>
        {
            manifestRecord.CodexPackageZipPath = ToRelative(project.ProjectDirectory, zipPath);
        });

        _logger.Info($"Final Codex package created: {zipPath}");
        return new CodexPackageResult
        {
            RunId = runId,
            ZipPath = zipPath,
            FinalTaskPath = finalTaskPath,
            ZipSizeBytes = new FileInfo(zipPath).Length
        };
    }

    public static AssetRequirementReport ParseAssetRequirements(string text)
    {
        const string startMarker = "ASSET_REQUIREMENTS_JSON_START";
        const string endMarker = "ASSET_REQUIREMENTS_JSON_END";
        var start = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var end = text.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end <= start)
        {
            return new AssetRequirementReport
            {
                HasAssetWarning = false,
                BlockingLevel = "none",
                Items = []
            };
        }

        var block = text[(start + startMarker.Length)..end];
        var match = Regex.Match(block, "```json\\s*(.*?)\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var json = match.Success ? match.Groups[1].Value : block;

        try
        {
            var report = JsonSerializer.Deserialize<AssetRequirementReport>(json.Trim(), JsonOptions) ?? new AssetRequirementReport();
            report.BlockingLevel = string.IsNullOrWhiteSpace(report.BlockingLevel) ? "none" : report.BlockingLevel;
            report.Items ??= [];
            return report;
        }
        catch
        {
            return new AssetRequirementReport
            {
                HasAssetWarning = true,
                BlockingLevel = "warning",
                Items =
                [
                    new AssetRequirementItem
                    {
                        Id = "parse-error",
                        Title = "ASSET_REQUIREMENTS_JSON could not be parsed",
                        Priority = "high",
                        Reason = "The JSON block exists but is invalid.",
                        Recommendation = "请让 GPT 只重新输出标记之间的 JSON 块。"
                    }
                ]
            };
        }
    }

    private static void SaveAssetManifest(RebuildProject project, AssetManifest manifest)
    {
        Directory.CreateDirectory(project.SourceAssetsDirectory);
        File.WriteAllText(project.AssetManifestPath, JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);
    }

    private static string GetRunDirectory(RebuildProject project, string runId) => Path.Combine(project.RunsDirectory, runId);
    private static string GetObservationDirectory(RebuildProject project, string runId) => Path.Combine(GetRunDirectory(project, runId), "observation");
    private static string GetGptPackageDirectory(RebuildProject project, string runId) => Path.Combine(GetRunDirectory(project, runId), "gpt-package");
    private static string GetAiOutputDirectory(RebuildProject project, string runId) => Path.Combine(GetRunDirectory(project, runId), "ai-output");
    private static string GetCodexPackageDirectory(RebuildProject project, string runId) => Path.Combine(GetRunDirectory(project, runId), "codex-package");

    private static void EnsureRunStructure(RebuildProject project, string runId)
    {
        var directories = new[]
        {
            GetRunDirectory(project, runId),
            GetObservationDirectory(project, runId),
            Path.Combine(GetObservationDirectory(project, runId), "screenshots"),
            Path.Combine(GetObservationDirectory(project, runId), "gifs"),
            Path.Combine(GetObservationDirectory(project, runId), "video"),
            Path.Combine(GetObservationDirectory(project, runId), "frames"),
            GetGptPackageDirectory(project, runId),
            Path.Combine(GetGptPackageDirectory(project, runId), "reference-screenshots"),
            Path.Combine(GetGptPackageDirectory(project, runId), "selected-assets"),
            Path.Combine(GetGptPackageDirectory(project, runId), "video-snippets"),
            GetAiOutputDirectory(project, runId),
            GetCodexPackageDirectory(project, runId),
            Path.Combine(GetCodexPackageDirectory(project, runId), "selected-assets"),
            Path.Combine(GetCodexPackageDirectory(project, runId), "reference-screenshots"),
            Path.Combine(GetCodexPackageDirectory(project, runId), "gifs"),
            Path.Combine(GetRunDirectory(project, runId), "review")
        };

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ClassifyAsset(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.Contains("logo", StringComparison.OrdinalIgnoreCase)
            && ext is ".png" or ".jpg" or ".jpeg" or ".svg" or ".webp")
        {
            return "logo";
        }

        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif" or ".tif" or ".tiff" => "image",
            ".svg" => "logo",
            ".mp4" or ".mov" or ".webm" or ".avi" or ".mkv" => "video",
            ".ttf" or ".otf" or ".woff" or ".woff2" => "font",
            ".pdf" or ".doc" or ".docx" or ".txt" or ".md" => "document",
            _ => "raw"
        };
    }

    private static string ToAssetFolder(string type)
    {
        return type switch
        {
            "image" => "images",
            "video" => "videos",
            "logo" => "logos",
            "font" => "fonts",
            "document" => "documents",
            _ => "raw"
        };
    }

    private static string CreateNextAssetId(AssetManifest manifest, string type)
    {
        var prefix = type switch
        {
            "image" => "asset-img",
            "video" => "asset-video",
            "logo" => "asset-logo",
            "font" => "asset-font",
            "document" => "asset-doc",
            _ => "asset-raw"
        };

        var index = manifest.Assets.Count(asset => asset.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) + 1;
        string id;
        do
        {
            id = $"{prefix}-{index:000}";
            index++;
        } while (manifest.Assets.Any(asset => string.Equals(asset.Id, id, StringComparison.OrdinalIgnoreCase)));

        return id;
    }

    private static string GetUniqueDestination(string directory, string fileName)
    {
        var candidate = Path.Combine(directory, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; i < 10_000; i++)
        {
            candidate = Path.Combine(directory, $"{name}_{i:000}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("无法创建唯一的素材文件名。");
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryReadImageInfo(string path, ProjectAsset asset)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            asset.Width = frame.PixelWidth;
            asset.Height = frame.PixelHeight;
        }
        catch
        {
            // SVG and unsupported bitmap formats remain valid assets without dimensions.
        }
    }

    private static void TryCreateThumbnail(RebuildProject project, string path, ProjectAsset asset)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var scale = Math.Min(320d / Math.Max(1, frame.PixelWidth), 220d / Math.Max(1, frame.PixelHeight));
            scale = Math.Min(1, scale);
            var transformed = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
            encoder.Frames.Add(BitmapFrame.Create(transformed));

            Directory.CreateDirectory(project.AssetThumbnailsDirectory);
            var thumbPath = Path.Combine(project.AssetThumbnailsDirectory, $"{asset.Id}_thumb.jpg");
            using var output = File.Create(thumbPath);
            encoder.Save(output);
            asset.Thumb = ToRelative(project.ProjectDirectory, thumbPath);
        }
        catch
        {
            asset.Thumb = string.Empty;
        }
    }

    private static string BuildUserIntentMarkdown(UserIntent intent)
    {
        return $"""
{BuildIntentStatusSection(intent)}

# 用户主观感受与目标动效

## 第一感受
{Blank(intent.FirstImpression)}

## 最想复刻的部分
{Blank(intent.FavoriteParts)}

## 关键动效描述
{Blank(intent.TargetEffects)}

## 不希望复刻或必须避免的部分
{Blank(intent.AvoidParts)}

## 希望最终实现的效果
{Blank(intent.DesiredOutcome)}
""";
    }

    private static string BuildUserIntentAssetReferenceMarkdown(RebuildProject project)
    {
        var manifest = LoadUserIntentAssetManifest(project);
        if (manifest.Items.Count == 0)
        {
            return """
                   ## 用户粘贴/上传的重点图片

                   用户未添加图片。
                   """;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## 用户粘贴/上传的重点图片");
        builder.AppendLine();
        foreach (var group in manifest.Items.GroupBy(item => item.FieldDisplayName))
        {
            builder.AppendLine($"### {group.Key}");
            builder.AppendLine();
            foreach (var item in group)
            {
                builder.AppendLine($"- {item.RelativePath}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildIntentStatusSection(UserIntent intent)
    {
        if (!intent.IsFallback)
        {
            return """
## 用户主观意图状态

用户已填写主观设计意图。
""";
        }

        return """
## 用户主观意图状态

用户未填写第一印象或目标动效。

请不要中止分析。

请先根据以下资料做基础网页结构与动效分析：

1. observation.md
2. selected-frames-index.md
3. selected-frames/
4. action-log.md
5. markers.md

如果缺少用户偏好，请在分析结尾列出需要用户补充的问题。
""";
    }

    private static int CopyReferenceFrames(
        RebuildProject project,
        FrameExtractResult? frameResult,
        string observationDirectory,
        string referenceScreenshotsDirectory)
    {
        if (frameResult is null || frameResult.Frames.Count == 0)
        {
            return 0;
        }

        var framesDirectory = Path.Combine(observationDirectory, "frames");
        var screenshotsDirectory = Path.Combine(observationDirectory, "screenshots");
        Directory.CreateDirectory(framesDirectory);
        Directory.CreateDirectory(screenshotsDirectory);

        var selected = SelectEvenly(frameResult.Frames, MaxReferenceFramesForGpt);
        var count = 0;
        foreach (var frame in selected)
        {
            var source = Path.Combine(project.ProjectDirectory, frame.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                continue;
            }

            CopyFileIfExists(source, Path.Combine(framesDirectory, frame.FileName));
            CopyFileIfExists(source, Path.Combine(screenshotsDirectory, frame.FileName));
            CopyFileIfExists(source, Path.Combine(referenceScreenshotsDirectory, frame.FileName));
            count++;
        }

        return count;
    }

    private static List<FrameIndexItem> SelectEvenly(IReadOnlyList<FrameIndexItem> frames, int maxCount)
    {
        if (frames.Count <= maxCount)
        {
            return frames.ToList();
        }

        var selected = new List<FrameIndexItem>();
        var step = (double)(frames.Count - 1) / (maxCount - 1);
        for (var i = 0; i < maxCount; i++)
        {
            selected.Add(frames[(int)Math.Round(i * step)]);
        }

        return selected
            .GroupBy(frame => frame.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static List<SelectedAssetForGpt> SelectAssetsForGpt(
        RebuildProject project,
        AssetManifest manifest,
        string selectedAssetsDirectory)
    {
        var selected = manifest.Assets
            .Where(asset => asset.Type is "image" or "logo")
            .Take(MaxAssetsForGpt)
            .ToList();

        var result = new List<SelectedAssetForGpt>();
        foreach (var asset in selected)
        {
            var relativeSource = string.IsNullOrWhiteSpace(asset.Thumb) ? asset.File : asset.Thumb;
            var source = Path.Combine(project.ProjectDirectory, relativeSource.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                continue;
            }

            var destination = Path.Combine(selectedAssetsDirectory, Path.GetFileName(source));
            CopyFileIfExists(source, destination);
            result.Add(new SelectedAssetForGpt
            {
                Id = asset.Id,
                File = asset.File,
                Type = asset.Type,
                Width = asset.Width,
                Height = asset.Height,
                SizeBytes = asset.SizeBytes,
                Extension = asset.Extension,
                Sha256 = asset.Sha256,
                Thumb = asset.Thumb,
                PackagePath = $"selected-assets/{Path.GetFileName(destination)}",
                Status = asset.Status
            });
        }

        return result;
    }

    private static string BuildGptPrompt(
        RebuildProject project,
        string runId,
        UserIntent intent,
        int copiedFrames,
        int selectedAssetCount)
    {
        return $"""
# GPT 网站重构分析任务

{BuildIntentStatusSection(intent)}

## 用户主观感受与目标动效

### 第一感受
{Blank(intent.FirstImpression)}

### 最想复刻的部分
{Blank(intent.FavoriteParts)}

### 关键动效描述
{Blank(intent.TargetEffects)}

### 不希望复刻或必须避免的部分
{Blank(intent.AvoidParts)}

### 希望最终实现的效果
{Blank(intent.DesiredOutcome)}

## 项目信息

- 项目名称：{project.ProjectName}
- 参考网站：{project.ReferenceUrl}
- RunId：{runId}
- 参考截图数量：{copiedFrames}
- 精选素材数量：{selectedAssetCount}

## 文件阅读顺序

1. user-intent.md
2. reference-screenshots/
3. asset-manifest-for-gpt.json
4. selected-assets/
5. dom-summary.json / css-summary.json
6. interactive-targets.md
7. user-intent-assets.md / user-intent-images/

## 素材适配分析要求

请同时分析参考网站观察资料和用户提供的项目素材库。

你必须判断：

1. 参考网站的核心视觉效果需要哪些素材。
2. 用户当前素材库中哪些素材可以直接使用。
3. 哪些素材需要二次处理，例如调暗、裁切、去背景、压缩、生成暗光版。
4. 哪些素材缺失。
5. 如果缺失，是否可以用 CSS、滤镜、遮罩、叠加层等方式临时降级。
6. Codex 建站时应优先使用哪些素材。
7. 哪些素材禁止使用或不适合当前风格。
8. 哪些动效不是单靠代码能实现，必须依赖多套素材配合。

## 素材前置条件提醒

如果存在素材缺失、素材不足、素材版本不匹配的问题，请在输出开头显著显示：

# ⚠️ 素材前置条件提醒

内容必须包括：

1. 缺少什么素材。
2. 影响哪个动效。
3. 没有素材时会造成什么失真。
4. 是否可以降级实现。
5. 是否建议暂停生成最终 Codex 指令。

## 必须输出机器可解析 JSON

请在报告末尾输出下面的稳定区块。即使没有素材问题，也必须输出空 items：

<!-- ASSET_REQUIREMENTS_JSON_START -->
```json
{"{"}
  "hasAssetWarning": false,
  "blockingLevel": "none",
  "items": []
{"}"}
```
<!-- ASSET_REQUIREMENTS_JSON_END -->

如果有素材问题，请把 hasAssetWarning 设为 true，并在 items 中列出缺失素材、影响动效、fallback 和 recommendation。

blockingLevel 只能是 none、warning 或 blocking。

请最后生成：网站分析、素材适配分析、缺失素材提醒、Codex 建站指令草稿。
""";
    }

    private static string BuildAssetCompareMarkdown(AssetRequirementReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Asset Compare");
        builder.AppendLine();
        builder.AppendLine($"- Has warning: {report.HasAssetWarning}");
        builder.AppendLine($"- Blocking level: {report.BlockingLevel}");
        builder.AppendLine();
        foreach (var item in report.Items)
        {
            builder.AppendLine($"## {item.Title}");
            builder.AppendLine($"- Priority: {item.Priority}");
            builder.AppendLine($"- Effect: {item.Effect}");
            builder.AppendLine($"- Required assets: {string.Join(", ", item.RequiredAssets)}");
            builder.AppendLine($"- Current status: {item.CurrentStatus}");
            builder.AppendLine($"- Fallback: {item.Fallback}");
            builder.AppendLine($"- Recommendation: {item.Recommendation}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildCodexTaskDraft(RebuildProject project, string runId, AssetRequirementReport report)
    {
        return $"""
# Codex 任务草稿

项目：{project.ProjectName}
参考网站：{project.ReferenceUrl}
RunId: {runId}
素材阻断级别：{report.BlockingLevel}

请在用户决定补充素材或使用降级方案后，再使用 final-codex-task.md。
""";
    }

    private static string BuildFinalCodexTask(
        RebuildProject project,
        string runId,
        AssetManifest manifest,
        AssetRequirementReport report,
        bool useFallbackStrategy)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 最终 Codex 网站重构任务");
        builder.AppendLine();
        builder.AppendLine($"项目：{project.ProjectName}");
        builder.AppendLine($"参考网站：{project.ReferenceUrl}");
        builder.AppendLine($"RunId: {runId}");
        if (!string.IsNullOrWhiteSpace(project.LocalCodeProjectPath))
        {
            builder.AppendLine($"本地源码项目路径：{project.LocalCodeProjectPath}");
        }

        builder.AppendLine();
        builder.AppendLine("## 素材使用要求");
        builder.AppendLine();
        builder.AppendLine("请优先使用以下素材。不要让 Codex 自己猜测素材路径。");
        builder.AppendLine();

        foreach (var asset in manifest.Assets.Take(20))
        {
            var targetName = Path.GetFileName(asset.File);
            builder.AppendLine($"{asset.Id}");
            builder.AppendLine($"- 原路径：{asset.File}");
            builder.AppendLine($"- 类型：{asset.Type}");
            builder.AppendLine("- 用途：根据 analysis.md 与 asset-compare.md 中的素材适配建议使用。");
            builder.AppendLine($"- 目标路径：wwwroot/assets/imported/{targetName}");
            builder.AppendLine();
        }

        if (report.Items.Count > 0)
        {
            builder.AppendLine("## 当前缺失");
            builder.AppendLine();
            foreach (var item in report.Items)
            {
                builder.AppendLine($"- {item.Title}");
                builder.AppendLine($"  - Required assets: {string.Join(", ", item.RequiredAssets)}");
                builder.AppendLine($"  - Effect: {item.Effect}");
                builder.AppendLine($"  - Current status: {item.CurrentStatus}");
                builder.AppendLine($"  - Recommendation: {item.Recommendation}");
            }

            builder.AppendLine();
        }

        if (useFallbackStrategy || string.Equals(report.BlockingLevel, "warning", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("## 本轮降级策略");
            builder.AppendLine();
            builder.AppendLine("- 当前缺少关键素材时，本轮使用 CSS filter / overlay / clip-path 等方式降级模拟。");
            builder.AppendLine("- 不要硬编码不存在的图片路径。");
            builder.AppendLine("- 代码中预留后续替换为真实素材的结构。");
            builder.AppendLine();
        }

        builder.AppendLine("## 禁止破坏项");
        builder.AppendLine();
        builder.AppendLine("- 不要删除现有录屏、截屏、打包功能。");
        builder.AppendLine("- 不要破坏现有项目路径配置。");
        builder.AppendLine("- 不要把用户素材直接散落到正式项目多个目录。");
        builder.AppendLine("- 不要硬编码不存在的素材路径。");
        builder.AppendLine("- 不要删除历史项目配置。");
        builder.AppendLine("- 不要改变已有文件命名规则，除非有兼容迁移。");
        builder.AppendLine("- 不要把 PPT、后端应用、本地模型训练作为本轮实现内容。");
        return builder.ToString();
    }

    private static async Task UpdateRunManifestAsync(RebuildProject project, string runId, Action<SiteRunManifest> update)
    {
        var manifestPath = Path.Combine(GetRunDirectory(project, runId), "manifest.json");
        SiteRunManifest manifest;
        if (File.Exists(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<SiteRunManifest>(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8), JsonOptions)
                ?? new SiteRunManifest();
        }
        else
        {
            manifest = new SiteRunManifest
            {
                RunId = runId,
                ProjectName = project.ProjectName,
                ReferenceUrl = project.ReferenceUrl,
                CreatedAt = DateTime.Now
            };
        }

        manifest.RunId = runId;
        manifest.ProjectName = project.ProjectName;
        manifest.ReferenceUrl = project.ReferenceUrl;
        update(manifest);
        await WriteJsonAsync(manifestPath, manifest);
    }

    private static void CreateZipFromDirectory(string sourceDirectory, string zipPath)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        var tempZip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        try
        {
            ZipFile.CreateFromDirectory(sourceDirectory, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false, Encoding.UTF8);
            File.Move(tempZip, zipPath);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }
    }

    private static void CopyDirectoryFiles(string sourceDirectory, string destinationDirectory, int maxFiles)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory).Take(maxFiles))
        {
            CopyFileIfExists(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
        }
    }

    private async Task EnsureObservationSummaryFilesAsync(RebuildProject project, string observationDirectory)
    {
        var domSummaryPath = Path.Combine(project.ActionsDirectory, "dom-summary.json");
        var interactiveTargetsJsonPath = Path.Combine(project.ActionsDirectory, "interactive-targets.json");
        var interactiveTargetsMarkdownPath = Path.Combine(project.ActionsDirectory, "interactive-targets.md");

        if (File.Exists(domSummaryPath))
        {
            CopyFileIfExists(domSummaryPath, Path.Combine(observationDirectory, "dom-summary.json"));
        }
        else
        {
            await WriteJsonAsync(Path.Combine(observationDirectory, "dom-summary.json"), new
            {
                status = "unavailable",
                reason = "当前外部浏览器模式无法读取 DOM，请切换 Playwright/CDP/WebView2 模式以启用元素级交互采集。",
                fallback = "hotspot-coordinate-observe"
            });
        }

        if (File.Exists(interactiveTargetsJsonPath))
        {
            CopyFileIfExists(interactiveTargetsJsonPath, Path.Combine(observationDirectory, "interactive-targets.json"));
        }

        if (File.Exists(interactiveTargetsMarkdownPath))
        {
            CopyFileIfExists(interactiveTargetsMarkdownPath, Path.Combine(observationDirectory, "interactive-targets.md"));
        }
        else
        {
            await File.WriteAllTextAsync(
                Path.Combine(observationDirectory, "interactive-targets.md"),
                """
                # 网页交互目标清单

                - 采集状态：unavailable
                - 说明：当前外部浏览器模式无法读取 DOM，请切换 Playwright/CDP/WebView2 模式以启用元素级交互采集。
                - 降级方式：hotspot-coordinate-observe
                """,
                Encoding.UTF8);
        }

        await WriteJsonAsync(Path.Combine(observationDirectory, "css-summary.json"), new
        {
            status = "unavailable",
            reason = "本轮未实现 CSS 规则采集。请优先参考 observation.md、selected frames 和交互目标清单。",
            fallback = "visual-frame-analysis"
        });
    }

    private static void CopyUserIntentAssetsIntoGptPackage(RebuildProject project, string gptDirectory)
    {
        CopyFileIfExists(project.UserIntentAssetManifestMarkdownPath, Path.Combine(gptDirectory, "user-intent-assets.md"));
        CopyFileIfExists(project.UserIntentAssetManifestJsonPath, Path.Combine(gptDirectory, "user-intent-assets.json"));

        var manifest = LoadUserIntentAssetManifest(project);
        foreach (var item in manifest.Items)
        {
            var source = Path.Combine(project.ProjectDirectory, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var destination = Path.Combine(gptDirectory, "user-intent-images", item.Field, Path.GetFileName(item.RelativePath));
            CopyFileIfExists(source, destination);
        }
    }

    private static UserIntentAssetManifest LoadUserIntentAssetManifest(RebuildProject project)
    {
        if (!File.Exists(project.UserIntentAssetManifestJsonPath))
        {
            return new UserIntentAssetManifest();
        }

        try
        {
            return JsonSerializer.Deserialize<UserIntentAssetManifest>(
                File.ReadAllText(project.UserIntentAssetManifestJsonPath, Encoding.UTF8),
                JsonOptions) ?? new UserIntentAssetManifest();
        }
        catch
        {
            return new UserIntentAssetManifest();
        }
    }

    private static void CopyFileIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static string BuildFileTree(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), "package.zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.AppendLine(ToRelative(directory, file));
        }

        return builder.ToString();
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8);
    }

    private static string ToRelative(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string Blank(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(未填写)" : value.Trim();
    }

    private sealed class SelectedAssetForGpt
    {
        public string Id { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long SizeBytes { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string PackagePath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
