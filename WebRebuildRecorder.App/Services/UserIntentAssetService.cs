using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class UserIntentAssetService
{
    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly AppLogger _logger;

    public UserIntentAssetService(AppLogger logger)
    {
        _logger = logger;
    }

    public void EnsureStructure(RebuildProject project)
    {
        Directory.CreateDirectory(project.UserIntentDirectory);
        foreach (var field in UserIntentFieldNames.All)
        {
            Directory.CreateDirectory(GetFieldDirectory(project, field));
        }

        if (!File.Exists(project.UserIntentAssetManifestJsonPath))
        {
            SaveManifest(project, new UserIntentAssetManifest());
        }

        if (!File.Exists(project.UserIntentAssetManifestMarkdownPath))
        {
            WriteMarkdown(project, LoadManifest(project));
        }
    }

    public UserIntentAssetManifest SaveClipboardImage(
        RebuildProject project,
        string field,
        BitmapSource image)
    {
        EnsureStructure(project);
        var targetPath = GetNextTargetPath(project, field, ".png");
        using (var stream = File.Create(targetPath))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        return AddManifestItem(project, field, targetPath, "clipboard");
    }

    public UserIntentAssetManifest ImportImage(
        RebuildProject project,
        string field,
        string sourceFile)
    {
        EnsureStructure(project);
        var extension = Path.GetExtension(sourceFile);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("仅支持 png、jpg、jpeg、webp 图片。");
        }

        var targetPath = GetNextTargetPath(project, field, extension.ToLowerInvariant());
        File.Copy(sourceFile, targetPath);
        return AddManifestItem(project, field, targetPath, "upload");
    }

    public UserIntentAssetManifest LoadManifest(RebuildProject project)
    {
        EnsureDirectoriesOnly(project);
        if (!File.Exists(project.UserIntentAssetManifestJsonPath))
        {
            return new UserIntentAssetManifest();
        }

        try
        {
            var json = File.ReadAllText(project.UserIntentAssetManifestJsonPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<UserIntentAssetManifest>(json, JsonOptions) ?? new UserIntentAssetManifest();
        }
        catch (Exception ex)
        {
            _logger.Error("读取用户意图图片清单失败。", ex);
            return new UserIntentAssetManifest();
        }
    }

    public void SaveManifest(RebuildProject project, UserIntentAssetManifest manifest)
    {
        EnsureDirectoriesOnly(project);
        File.WriteAllText(project.UserIntentAssetManifestJsonPath, JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);
        WriteMarkdown(project, manifest);
    }

    public int CountByField(RebuildProject project, string field)
    {
        return LoadManifest(project).Items.Count(item => string.Equals(item.Field, field, StringComparison.OrdinalIgnoreCase));
    }

    public string GetFieldDirectory(RebuildProject project, string field)
    {
        return Path.Combine(project.UserIntentDirectory, field);
    }

    public string BuildReferenceMarkdown(RebuildProject project)
    {
        var manifest = LoadManifest(project);
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
        AppendFieldList(builder, manifest, UserIntentFieldNames.FavoriteParts);
        AppendFieldList(builder, manifest, UserIntentFieldNames.TargetEffects);
        AppendFieldList(builder, manifest, UserIntentFieldNames.FirstImpression);
        AppendFieldList(builder, manifest, UserIntentFieldNames.Avoid);
        AppendFieldList(builder, manifest, UserIntentFieldNames.DesiredResult);
        return builder.ToString();
    }

    public IReadOnlyList<UserIntentAssetItem> GetItems(RebuildProject project)
    {
        return LoadManifest(project).Items;
    }

    public void CopyImagesToPackage(RebuildProject project, string targetRootDirectory)
    {
        var manifest = LoadManifest(project);
        foreach (var item in manifest.Items)
        {
            var source = Path.Combine(project.ProjectDirectory, item.RelativePath);
            if (!File.Exists(source))
            {
                continue;
            }

            var target = Path.Combine(targetRootDirectory, item.Field, Path.GetFileName(item.RelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    private UserIntentAssetManifest AddManifestItem(RebuildProject project, string field, string targetPath, string source)
    {
        var manifest = LoadManifest(project);
        var item = new UserIntentAssetItem
        {
            Id = $"intent-img-{manifest.Items.Count + 1:000}",
            Field = field,
            FieldDisplayName = UserIntentFieldNames.GetDisplayName(field),
            RelativePath = NormalizeRelativePath(Path.GetRelativePath(project.ProjectDirectory, targetPath)),
            Source = source,
            Note = $"用户标记的{UserIntentFieldNames.GetDisplayName(field)}参考图",
            CreatedAt = DateTime.Now
        };

        manifest.Items.Add(item);
        SaveManifest(project, manifest);
        return manifest;
    }

    private string GetNextTargetPath(RebuildProject project, string field, string extension)
    {
        var directory = GetFieldDirectory(project, field);
        Directory.CreateDirectory(directory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var index = CountByField(project, field) + 1;
        while (index < 10_000)
        {
            var candidate = Path.Combine(directory, $"{timestamp}_{field}_{index:000}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }

        throw new IOException("无法生成唯一的用户意图图片文件名。");
    }

    private void WriteMarkdown(RebuildProject project, UserIntentAssetManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 用户意图图片清单");
        builder.AppendLine();
        builder.AppendLine("这些图片由用户手动粘贴或上传，用于说明用户喜欢的局部、目标动效或希望避免的内容。");
        builder.AppendLine();

        if (manifest.Items.Count == 0)
        {
            builder.AppendLine("用户尚未粘贴或上传重点参考图片。");
        }
        else
        {
            builder.AppendLine("| 序号 | 字段 | 图片 | 来源 | 说明 |");
            builder.AppendLine("|---|---|---|---|---|");
            for (var index = 0; index < manifest.Items.Count; index++)
            {
                var item = manifest.Items[index];
                builder.AppendLine($"| {index + 1} | {Escape(item.FieldDisplayName)} | {Escape(item.RelativePath)} | {GetSourceDisplayName(item.Source)} | {Escape(item.Note)} |");
            }
        }

        File.WriteAllText(project.UserIntentAssetManifestMarkdownPath, builder.ToString(), Encoding.UTF8);
    }

    private static void AppendFieldList(StringBuilder builder, UserIntentAssetManifest manifest, string field)
    {
        var items = manifest.Items
            .Where(item => string.Equals(item.Field, field, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (items.Count == 0)
        {
            return;
        }

        builder.AppendLine($"### {UserIntentFieldNames.GetDisplayName(field)}");
        builder.AppendLine();
        foreach (var item in items)
        {
            builder.AppendLine($"- {item.RelativePath}");
        }

        builder.AppendLine();
    }

    private static string GetSourceDisplayName(string source) => source switch
    {
        "clipboard" => "剪贴板",
        "upload" => "上传",
        _ => source
    };

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string Escape(string text)
    {
        return text.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private void EnsureDirectoriesOnly(RebuildProject project)
    {
        Directory.CreateDirectory(project.UserIntentDirectory);
        foreach (var field in UserIntentFieldNames.All)
        {
            Directory.CreateDirectory(GetFieldDirectory(project, field));
        }
    }
}
