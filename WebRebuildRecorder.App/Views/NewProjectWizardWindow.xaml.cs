using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using WebRebuildRecorder.App.Services;
using WinForms = System.Windows.Forms;

namespace WebRebuildRecorder.App.Views;

public partial class NewProjectWizardWindow : Window
{
    private readonly string _fallbackRootDirectory;

    public NewProjectWizardWindow(string fallbackRootDirectory)
    {
        InitializeComponent();
        _fallbackRootDirectory = fallbackRootDirectory;
        RootDirectoryBox.Text = fallbackRootDirectory;
        UpdatePreview();
    }

    public string ProjectNameValue { get; private set; } = string.Empty;
    public string ReferenceUrlValue { get; private set; } = string.Empty;
    public string RootDirectoryValue { get; private set; } = string.Empty;
    public string LocalCodeProjectPathValue { get; private set; } = string.Empty;
    public string AssetSourceDirectoryValue { get; private set; } = string.Empty;

    public void LoadInitialValues(
        string projectName,
        string referenceUrl,
        string rootDirectory,
        string localCodeProjectPath,
        string assetSourceDirectory)
    {
        ProjectNameBox.Text = projectName;
        ReferenceUrlBox.Text = referenceUrl;
        RootDirectoryBox.Text = string.IsNullOrWhiteSpace(rootDirectory) ? _fallbackRootDirectory : rootDirectory;
        LocalCodePathBox.Text = localCodeProjectPath;
        AssetDirectoryBox.Text = assetSourceDirectory;
        UpdatePreview();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void BrowseRootButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(RootDirectoryBox, "选择项目根目录");
    }

    private void BrowseLocalCodeButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(LocalCodePathBox, "选择源码项目目录（可选）");
    }

    private void BrowseAssetsButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(AssetDirectoryBox, "选择素材目录（可选）");
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out var message))
        {
            ValidationText.Text = message;
            return;
        }

        ProjectNameValue = ProjectNameBox.Text.Trim();
        ReferenceUrlValue = NormalizeUrl(ReferenceUrlBox.Text);
        RootDirectoryValue = Path.GetFullPath(Environment.ExpandEnvironmentVariables(RootDirectoryBox.Text.Trim()));
        LocalCodeProjectPathValue = LocalCodePathBox.Text.Trim();
        AssetSourceDirectoryValue = AssetDirectoryBox.Text.Trim();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void BrowseFolderInto(System.Windows.Controls.TextBox target, string title)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = title,
            SelectedPath = Directory.Exists(target.Text) ? target.Text : _fallbackRootDirectory,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
            UpdatePreview();
        }
    }

    private bool TryValidate(out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(ProjectNameBox.Text))
        {
            message = "请填写项目名称。";
            return false;
        }

        if (ProjectNameBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            message = "项目名称包含非法文件名字符，请修改后再创建。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ReferenceUrlBox.Text))
        {
            message = "请填写参考网站链接。";
            return false;
        }

        var url = NormalizeUrl(ReferenceUrlBox.Text);
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            message = "参考网站链接格式不正确。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RootDirectoryBox.Text))
        {
            message = "请选择项目根目录。";
            return false;
        }

        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(RootDirectoryBox.Text.Trim()));
        if (!Directory.Exists(root))
        {
            var result = System.Windows.MessageBox.Show(
                $"项目根目录不存在，是否创建？{Environment.NewLine}{root}",
                "创建项目根目录",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                message = "项目根目录不存在。";
                return false;
            }

            Directory.CreateDirectory(root);
        }

        return true;
    }

    private void UpdatePreview()
    {
        if (ProjectDirectoryPreviewText is null)
        {
            return;
        }

        try
        {
            var root = string.IsNullOrWhiteSpace(RootDirectoryBox.Text) ? _fallbackRootDirectory : RootDirectoryBox.Text.Trim();
            var name = string.IsNullOrWhiteSpace(ProjectNameBox.Text) ? "project" : ProjectNameBox.Text.Trim();
            var slug = ProjectService.Slugify(name);
            var baseDirectory = Path.Combine(root, $"{DateTime.Now:yyyy-MM-dd}_{slug}");
            ProjectDirectoryPreviewText.Text = GetPreviewDirectory(baseDirectory);
            ValidationText.Text = string.Empty;
        }
        catch
        {
            ProjectDirectoryPreviewText.Text = "--";
        }
    }

    private static string NormalizeUrl(string raw)
    {
        var url = raw.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }

    private static string GetPreviewDirectory(string baseDirectory)
    {
        if (!Directory.Exists(baseDirectory))
        {
            return baseDirectory;
        }

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{baseDirectory}_{index:000}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Regex.Replace(baseDirectory, @"\\+$", string.Empty) + "_next";
    }
}
