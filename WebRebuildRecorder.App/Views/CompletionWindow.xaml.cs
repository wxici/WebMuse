using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace WebRebuildRecorder.App.Views;

public partial class CompletionWindow : Window
{
    private readonly string _projectDirectory;
    private readonly string _packageDirectory;
    private readonly string _zipPath;
    private readonly string _promptText;

    public CompletionWindow(
        string details,
        string projectDirectory,
        string packageDirectory,
        string zipPath,
        string promptText)
    {
        InitializeComponent();
        DetailsBox.Text = details;
        _projectDirectory = projectDirectory;
        _packageDirectory = packageDirectory;
        _zipPath = zipPath;
        _promptText = promptText;

        Topmost = false;
        StatusText.Text = "该面板为非模态窗口，主窗口和悬浮工具条仍可操作。";
    }

    public CompletionWindow(string details, string projectDirectory, string markdownDirectory)
        : this(details, projectDirectory, markdownDirectory, string.Empty, string.Empty)
    {
    }

    private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(_projectDirectory);
    }

    private void OpenPackageButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(_packageDirectory);
    }

    private void OpenChatGptButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://chatgpt.com/",
            UseShellExecute = true
        });
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_promptText))
        {
            System.Windows.Clipboard.SetText(_promptText);
            StatusText.Text = "提示词已复制。";
        }
    }

    private void CopyZipPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_zipPath))
        {
            System.Windows.Clipboard.SetText(_zipPath);
            StatusText.Text = "ZIP 路径已复制。";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}
