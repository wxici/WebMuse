using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebRebuildRecorder.App.Core.ProjectSystem;
using WebRebuildRecorder.App.Core.Security;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Core.State;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class ProjectService : IProjectManager
{
    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;

    private readonly AppLogger _logger;
    private readonly ProjectDirectoryV2Service _directoryV2Service = new();
    private readonly ProjectManifestService _manifestService = new();

    public ProjectService(AppLogger logger)
    {
        _logger = logger;
    }

    public RebuildProject? CurrentProject { get; private set; }

    public string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WebRebuildRecorder");

    public string RecentProjectsPath => Path.Combine(SettingsDirectory, "recent-projects.json");

    public string GetFallbackRootDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WebRebuildRecorderProjects");
    }

    public string ResolveProjectsRootDirectory(string? rootDirectory)
    {
        return string.IsNullOrWhiteSpace(rootDirectory)
            ? GetFallbackRootDirectory()
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootDirectory.Trim()));
    }

    public Task<RebuildProject> CreateProjectAsync(string projectName, string referenceUrl, string rootDirectory)
    {
        return CreateNewProjectAsync(new NewProjectOptions
        {
            ProjectName = projectName,
            ReferenceUrl = referenceUrl,
            RootDirectory = rootDirectory
        });
    }

    public async Task<RebuildProject> CreateNewProjectAsync(NewProjectOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProjectName))
        {
            throw new InvalidOperationException("项目名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(options.ReferenceUrl))
        {
            throw new InvalidOperationException("参考网站链接不能为空。");
        }

        if (!Uri.TryCreate(options.ReferenceUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("参考网站链接格式无效。");
        }

        var rootDirectory = ResolveProjectsRootDirectory(options.RootDirectory);
        var slug = Slugify(options.ProjectName);
        var baseDirectoryName = $"{DateTime.Now:yyyy-MM-dd}_{slug}";
        var projectDirectory = GetNextProjectDirectory(rootDirectory, baseDirectoryName);
        var projectRootValidation = SandboxPathPolicy.ValidateProjectRoot(projectDirectory);
        if (!projectRootValidation.IsAllowed)
        {
            throw new InvalidOperationException(projectRootValidation.Message);
        }

        projectDirectory = projectRootValidation.NormalizedTargetPath;
        rootDirectory = Directory.GetParent(projectDirectory)?.FullName ?? rootDirectory;
        var now = DateTime.Now;

        var project = new RebuildProject
        {
            ProjectId = Path.GetFileName(projectDirectory),
            ProjectName = options.ProjectName.Trim(),
            Slug = slug,
            ReferenceUrl = options.ReferenceUrl.Trim(),
            RootDirectory = rootDirectory,
            ProjectDirectory = projectDirectory,
            RecordingId = "001",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _directoryV2Service.CreateAsync(project.ProjectDirectory);
        CreateProjectFolders(project);
        await WriteProjectFilesAsync(project);
        CurrentProject = project;
        await AddOrUpdateRecentProjectAsync(project);

        _logger.AttachLogFile(Path.Combine(project.LogsDirectory, "app.log"));
        _logger.Info($"项目已创建：{project.ProjectDirectory}");
        return project;
    }

    public async Task<IReadOnlyList<ProjectHistoryItem>> LoadRecentProjectsAsync()
    {
        if (!File.Exists(RecentProjectsPath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(RecentProjectsPath);
            var items = await JsonSerializer.DeserializeAsync<List<ProjectHistoryItem>>(stream, JsonOptions) ?? [];
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.ProjectDirectory))
                .OrderByDescending(item => item.LastOpenedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("读取最近项目列表失败。", ex);
            return [];
        }
    }

    public async Task<RebuildProject> OpenProjectAsync(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new InvalidOperationException("项目目录不能为空。");
        }

        projectDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(projectDirectory.Trim()));
        var projectJsonPath = Path.Combine(projectDirectory, "project.json");
        var legacyProjectInfoPath = Path.Combine(projectDirectory, "project-info.json");
        var manifestPath = ProjectManifestService.GetManifestPath(projectDirectory);
        var canUseManifest = File.Exists(manifestPath)
            && SandboxPathPolicy.ValidateProjectRoot(projectDirectory).IsAllowed;
        WrbProjectManifest? manifest = null;
        if (canUseManifest)
        {
            manifest = await _manifestService.LoadAsync(projectDirectory);
        }

        var loadPath = File.Exists(projectJsonPath)
            ? projectJsonPath
            : File.Exists(legacyProjectInfoPath)
                ? legacyProjectInfoPath
                : string.Empty;

        if (string.IsNullOrWhiteSpace(loadPath) && manifest is null)
        {
            throw new FileNotFoundException("未找到 project.json 或 project-info.json。", projectJsonPath);
        }

        RebuildProject project;
        if (!string.IsNullOrWhiteSpace(loadPath))
        {
            var json = await File.ReadAllTextAsync(loadPath, Encoding.UTF8);
            project = JsonSerializer.Deserialize<RebuildProject>(json, JsonOptions)
            ?? throw new InvalidOperationException("项目文件格式无效。");

        }
        else
        {
            project = CreateProjectFromManifest(manifest!, projectDirectory);
        }

        if (manifest is not null)
        {
            ApplyManifestToProject(project, manifest, projectDirectory);
        }

        NormalizeProject(project, projectDirectory);
        if (SandboxPathPolicy.ValidateProjectRoot(project.ProjectDirectory).IsAllowed)
        {
            await _directoryV2Service.CreateAsync(project.ProjectDirectory);
        }

        CreateProjectFolders(project);
        await SaveProjectFilesAsync(project);

        CurrentProject = project;
        await AddOrUpdateRecentProjectAsync(project);
        _logger.AttachLogFile(Path.Combine(project.LogsDirectory, "app.log"));
        _logger.Info($"项目已打开：{project.ProjectDirectory}");
        return project;
    }

    public static bool IsValidProjectDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, "project.json"))
            || File.Exists(Path.Combine(directory, "project-info.json"))
            || File.Exists(Path.Combine(directory, WrbProjectSchema.FileName));
    }

    public static List<string> FindChildProjectDirectories(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetDirectories(directory)
            .Where(IsValidProjectDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task CloseCurrentProjectAsync()
    {
        if (CurrentProject is not null)
        {
            await SaveCurrentProjectAsync();
            _logger.Info($"项目已关闭：{CurrentProject.ProjectDirectory}");
        }

        CurrentProject = null;
    }

    public async Task SaveCurrentProjectAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        CurrentProject.UpdatedAt = DateTime.Now;
        await SaveProjectFilesAsync(CurrentProject);
        await AddOrUpdateRecentProjectAsync(CurrentProject);
    }

    public async Task RemoveRecentProjectAsync(string projectDirectory)
    {
        var normalized = NormalizeDirectoryKey(projectDirectory);
        var items = (await LoadRecentProjectsAsync())
            .Where(item => !string.Equals(NormalizeDirectoryKey(item.ProjectDirectory), normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await SaveRecentProjectsAsync(items);
    }

    public async Task UpdateReferenceUrlAsync(RebuildProject project, string referenceUrl)
    {
        if (string.IsNullOrWhiteSpace(referenceUrl))
        {
            throw new InvalidOperationException("参考网站链接不能为空。");
        }

        if (!Uri.TryCreate(referenceUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("参考网站链接格式无效。");
        }

        project.ReferenceUrl = referenceUrl.Trim();
        project.UpdatedAt = DateTime.Now;
        await File.WriteAllTextAsync(Path.Combine(project.InputDirectory, "reference-url.txt"), project.ReferenceUrl + Environment.NewLine, Encoding.UTF8);
        await SaveProjectFilesAsync(project);
    }

    public async Task PrepareNextRecordingAsync(RebuildProject project)
    {
        project.RecordingId = GetNextRecordingId(project);
        project.LastRecordingId = project.RecordingId;
        project.UpdatedAt = DateTime.Now;
        await SaveProjectFilesAsync(project);
    }

    public async Task SaveBrowserProfileAsync(RebuildProject project, BrowserProfile profile)
    {
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "browser-profile.json"), profile);
    }

    public async Task SaveToolProfileAsync(RebuildProject project, ToolProfile profile)
    {
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "tool-profile.json"), profile);
    }

    public async Task SaveActionProfileAsync(RebuildProject project, ActionProfile profile)
    {
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "action-profile.json"), profile);
    }

    public async Task<ActionProfile> LoadActionProfileAsync(RebuildProject project)
    {
        return await ReadJsonOrDefaultAsync(Path.Combine(project.ConfigDirectory, "action-profile.json"), new ActionProfile());
    }

    public async Task<MdGenerationOptions> LoadMdGenerationOptionsAsync(RebuildProject project)
    {
        return await ReadJsonOrDefaultAsync(Path.Combine(project.ConfigDirectory, "md-generation.json"), new MdGenerationOptions());
    }

    public async Task<ToolProfile> LoadToolProfileAsync(RebuildProject project)
    {
        return await ReadJsonOrDefaultAsync(Path.Combine(project.ConfigDirectory, "tool-profile.json"), new ToolProfile());
    }

    public async Task<BrowserProfile> LoadBrowserProfileAsync(RebuildProject project)
    {
        return await ReadJsonOrDefaultAsync(Path.Combine(project.ConfigDirectory, "browser-profile.json"), new BrowserProfile());
    }

    public async Task<DeliveryProfile> LoadDeliveryProfileAsync(RebuildProject project)
    {
        var profile = await ReadJsonOrDefaultAsync(Path.Combine(project.ConfigDirectory, "delivery-profile.json"), new DeliveryProfile());
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "delivery-profile.json"), profile);
        return profile;
    }

    public async Task WriteRecordingInfoAsync(RebuildProject project, RecordingInfo info)
    {
        await WriteJsonAsync(Path.Combine(project.RecordingDirectory, "recording-info.json"), info);
        await WriteJsonAsync(Path.Combine(project.RecordingDirectory, $"recording-info_{project.RecordingId}.json"), info);
    }

    private static void CreateProjectFolders(RebuildProject project)
    {
        var folders = new[]
        {
            project.ProjectDirectory,
            project.InputDirectory,
            Path.Combine(project.InputDirectory, "assets"),
            Path.Combine(project.InputDirectory, "assets", "original"),
            Path.Combine(project.InputDirectory, "assets", "selected"),
            project.UserIntentDirectory,
            Path.Combine(project.UserIntentDirectory, UserIntentFieldNames.FirstImpression),
            Path.Combine(project.UserIntentDirectory, UserIntentFieldNames.FavoriteParts),
            Path.Combine(project.UserIntentDirectory, UserIntentFieldNames.TargetEffects),
            Path.Combine(project.UserIntentDirectory, UserIntentFieldNames.Avoid),
            Path.Combine(project.UserIntentDirectory, UserIntentFieldNames.DesiredResult),
            project.RecordingDirectory,
            project.FramesDirectory,
            project.MarkdownDirectory,
            project.MarkersDirectory,
            project.ActionsDirectory,
            project.PackageDirectory,
            project.ConfigDirectory,
            project.LogsDirectory,
            project.SourceAssetsDirectory,
            project.ImportedAssetsDirectory,
            project.AssetThumbnailsDirectory,
            project.RunsDirectory,
            Path.Combine(project.ImportedAssetsDirectory, "images"),
            Path.Combine(project.ImportedAssetsDirectory, "videos"),
            Path.Combine(project.ImportedAssetsDirectory, "logos"),
            Path.Combine(project.ImportedAssetsDirectory, "fonts"),
            Path.Combine(project.ImportedAssetsDirectory, "documents"),
            Path.Combine(project.ImportedAssetsDirectory, "raw")
        };

        foreach (var folder in folders)
        {
            Directory.CreateDirectory(folder);
        }
    }

    private async Task WriteProjectFilesAsync(RebuildProject project)
    {
        await File.WriteAllTextAsync(Path.Combine(project.InputDirectory, "reference-url.txt"), project.ReferenceUrl + Environment.NewLine, Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(project.InputDirectory, "user-notes.md"), "# User Notes" + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(project.InputDirectory, "style-notes.md"), "# Style Notes" + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(project.MarkersDirectory, "markers.md"), MarkerService.CreateMarkerHeader(), Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(project.ActionsDirectory, "action-log.md"), ActionLogger.CreateMarkdownHeader(), Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(project.ActionsDirectory, "action-log.jsonl"), string.Empty, Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(project.LogsDirectory, "app.log"), string.Empty, Encoding.UTF8);
        await WriteJsonAsync(project.AssetManifestPath, new AssetManifest
        {
            ProjectName = project.ProjectName,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await WriteJsonAsync(project.UserIntentAssetManifestJsonPath, new UserIntentAssetManifest());
        await File.WriteAllTextAsync(project.UserIntentAssetManifestMarkdownPath, CreateEmptyUserIntentAssetsMarkdown(), Encoding.UTF8);

        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "action-profile.json"), new ActionProfile());
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "md-generation.json"), new MdGenerationOptions());
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "browser-profile.json"), new BrowserProfile());
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "delivery-profile.json"), new DeliveryProfile());
        await WriteJsonAsync(Path.Combine(project.ConfigDirectory, "tool-profile.json"), new ToolProfile
        {
            FfmpegPath = ToolPathResolver.GetDefaultFfmpegPath(),
            FfprobePath = ToolPathResolver.GetDefaultFfprobePath()
        });
        await SaveProjectFilesAsync(project);
    }

    private async Task SaveProjectFilesAsync(RebuildProject project)
    {
        await WriteJsonAsync(Path.Combine(project.ProjectDirectory, "project.json"), project);
        await WriteJsonAsync(Path.Combine(project.ProjectDirectory, "project-info.json"), project);
        await TrySaveManifestAsync(project);
    }

    private async Task TrySaveManifestAsync(RebuildProject project)
    {
        var validation = SandboxPathPolicy.ValidateProjectRoot(project.ProjectDirectory);
        if (!validation.IsAllowed)
        {
            _logger.Warn($"Skipping project.wrbproj write because the project directory is outside the V2 safety policy: {validation.Message}");
            return;
        }

        var manifest = File.Exists(ProjectManifestService.GetManifestPath(project.ProjectDirectory))
            ? await _manifestService.LoadAsync(project.ProjectDirectory)
            : await _manifestService.CreateNewAsync(project.ProjectDirectory, project.ProjectName);

        ApplyProjectToManifest(project, manifest);
        await _manifestService.SaveAsync(project.ProjectDirectory, manifest);
    }

    private static RebuildProject CreateProjectFromManifest(WrbProjectManifest manifest, string projectDirectory)
    {
        var createdAt = manifest.CreatedAt == default ? DateTime.Now : manifest.CreatedAt.LocalDateTime;
        var updatedAt = manifest.UpdatedAt == default ? createdAt : manifest.UpdatedAt.LocalDateTime;
        return new RebuildProject
        {
            ProjectId = manifest.ProjectId,
            ProjectName = manifest.ProjectName,
            Slug = Slugify(manifest.ProjectName),
            ReferenceUrl = manifest.ReferenceUrl,
            RootDirectory = Directory.GetParent(projectDirectory)?.FullName ?? projectDirectory,
            ProjectDirectory = projectDirectory,
            RecordingId = "001",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    private static void ApplyManifestToProject(
        RebuildProject project,
        WrbProjectManifest manifest,
        string projectDirectory)
    {
        project.ProjectId = string.IsNullOrWhiteSpace(manifest.ProjectId)
            ? project.ProjectId
            : manifest.ProjectId.Trim();
        project.ProjectName = string.IsNullOrWhiteSpace(manifest.ProjectName)
            ? project.ProjectName
            : manifest.ProjectName.Trim();
        project.Slug = string.IsNullOrWhiteSpace(project.Slug)
            ? Slugify(project.ProjectName)
            : project.Slug;
        project.ReferenceUrl = manifest.ReferenceUrl?.Trim() ?? project.ReferenceUrl;
        project.ProjectDirectory = projectDirectory;
        project.RootDirectory = Directory.GetParent(projectDirectory)?.FullName ?? project.RootDirectory;
        if (manifest.CreatedAt != default)
        {
            project.CreatedAt = manifest.CreatedAt.LocalDateTime;
        }

        if (manifest.UpdatedAt != default)
        {
            project.UpdatedAt = manifest.UpdatedAt.LocalDateTime;
        }
    }

    private static void ApplyProjectToManifest(RebuildProject project, WrbProjectManifest manifest)
    {
        manifest.ProjectId = string.IsNullOrWhiteSpace(project.ProjectId)
            ? manifest.ProjectId
            : project.ProjectId.Trim();
        manifest.ProjectName = project.ProjectName.Trim();
        manifest.ProjectRoot = ".";
        manifest.ReferenceUrl = project.ReferenceUrl.Trim();
        manifest.CreatedAt = project.CreatedAt == default
            ? manifest.CreatedAt
            : new DateTimeOffset(project.CreatedAt);
        manifest.Features.UsesLegacyProjectJson = true;
    }

    private static string CreateEmptyUserIntentAssetsMarkdown()
    {
        return """
               # 用户意图图片清单

               用户尚未粘贴或上传重点参考图片。
               """;
    }

    private async Task AddOrUpdateRecentProjectAsync(RebuildProject project)
    {
        var items = (await LoadRecentProjectsAsync()).ToList();
        var normalized = NormalizeDirectoryKey(project.ProjectDirectory);
        items.RemoveAll(item => string.Equals(NormalizeDirectoryKey(item.ProjectDirectory), normalized, StringComparison.OrdinalIgnoreCase));
        items.Insert(0, new ProjectHistoryItem
        {
            ProjectName = project.ProjectName,
            ReferenceUrl = project.ReferenceUrl,
            ProjectDirectory = project.ProjectDirectory,
            LastOpenedAt = DateTime.Now
        });
        await SaveRecentProjectsAsync(items.Take(30).ToList());
    }

    private async Task SaveRecentProjectsAsync(IReadOnlyList<ProjectHistoryItem> items)
    {
        Directory.CreateDirectory(SettingsDirectory);
        await using var stream = File.Create(RecentProjectsPath);
        await JsonSerializer.SerializeAsync(stream, items, JsonOptions);
    }

    private static async Task<T> ReadJsonOrDefaultAsync<T>(string path, T fallback)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions) ?? fallback;
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
    }

    public static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var normalized = Regex.Replace(lower, @"[^a-z0-9\u4e00-\u9fa5]+", "-");
        normalized = Regex.Replace(normalized, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "project" : normalized;
    }

    public static string PreviewProjectDirectory(string rootDirectory, string projectName)
    {
        var slug = Slugify(projectName);
        return Path.Combine(rootDirectory, $"{DateTime.Now:yyyy-MM-dd}_{slug}");
    }

    private static void NormalizeProject(RebuildProject project, string projectDirectory)
    {
        project.ProjectDirectory = projectDirectory;
        project.RootDirectory = string.IsNullOrWhiteSpace(project.RootDirectory)
            ? Directory.GetParent(projectDirectory)?.FullName ?? projectDirectory
            : project.RootDirectory;
        project.ProjectName = string.IsNullOrWhiteSpace(project.ProjectName) ? Path.GetFileName(projectDirectory) : project.ProjectName;
        project.Slug = string.IsNullOrWhiteSpace(project.Slug) ? Slugify(project.ProjectName) : project.Slug;
        project.ProjectId = string.IsNullOrWhiteSpace(project.ProjectId) ? $"{project.CreatedAt:yyyy-MM-dd}_{project.Slug}" : project.ProjectId;
        project.RecordingId = string.IsNullOrWhiteSpace(project.RecordingId) ? "001" : project.RecordingId;
        project.ReferenceUrl ??= string.Empty;
        project.LocalCodeProjectPath ??= string.Empty;
        project.UserAssetSourceDirectory ??= string.Empty;
        project.LastRunId ??= string.Empty;
        project.RecordingSettings ??= new ProjectRecordingSettings();
        project.FrameExtractSettings ??= new ProjectFrameExtractSettings();
        project.BrowserSettings ??= new ProjectBrowserSettings();
        project.AutoObserveSettings ??= new ProjectAutoObserveSettings();
        if (project.CreatedAt == default)
        {
            project.CreatedAt = DateTime.Now;
        }

        if (project.UpdatedAt == default)
        {
            project.UpdatedAt = project.CreatedAt;
        }
    }

    private static string GetNextProjectDirectory(string rootDirectory, string baseDirectoryName)
    {
        var projectDirectory = Path.Combine(rootDirectory, baseDirectoryName);
        if (!Directory.Exists(projectDirectory))
        {
            return projectDirectory;
        }

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(rootDirectory, $"{baseDirectoryName}_{index:000}");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("无法创建唯一的项目目录。");
    }

    private static string GetNextRecordingId(RebuildProject project)
    {
        for (var index = 1; index < 10_000; index++)
        {
            var recordingId = index.ToString("000");
            var videoPath = Path.Combine(project.RecordingDirectory, $"{project.Slug}_{recordingId}.mp4");
            if (!File.Exists(videoPath))
            {
                return recordingId;
            }
        }

        throw new InvalidOperationException("无法创建唯一的录屏编号。");
    }

    private static string NormalizeDirectoryKey(string directory)
    {
        return Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
