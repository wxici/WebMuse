using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WebRebuildRecorder.App.Services;

namespace WebRebuildRecorder.App.Views;

public partial class DetachedPreviewWindow : Window
{
    private readonly AppLogger _logger;
    private bool _initialized;
    private string? _lastUri;

    public DetachedPreviewWindow(AppLogger logger)
    {
        InitializeComponent();
        _logger = logger;
        SizeChanged += (_, _) => UpdateViewportStatus();
        Loaded += (_, _) => UpdateViewportStatus();
    }

    public async Task NavigateAsync(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            StatusText.Text = "独立预览窗口：没有可打开的地址。";
            return;
        }

        var normalized = NormalizeInputUri(uri);
        AddressBox.Text = normalized;

        await EnsurePreviewAsync();

        _lastUri = normalized;
        StatusText.Text = $"独立预览窗口：正在打开 {normalized}";
        _logger.Info($"Detached WebView2 preview navigate: {normalized}");
        DetachedPreviewWebView.Source = new Uri(normalized);
        UpdateViewportStatus();
    }

    private async Task EnsurePreviewAsync()
    {
        if (_initialized)
        {
            return;
        }

        StatusText.Text = "独立预览窗口：正在初始化 WebView2...";
        try
        {
            await DetachedPreviewWebView.EnsureCoreWebView2Async();

            DetachedPreviewWebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                StatusText.Text = $"独立预览窗口：正在加载 {args.Uri}";
            };

            DetachedPreviewWebView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                StatusText.Text = args.IsSuccess
                    ? $"独立预览窗口：加载完成 {_lastUri}"
                    : $"独立预览窗口：加载失败，错误 {args.WebErrorStatus}";
            };

            _initialized = true;
            StatusText.Text = "独立预览窗口：WebView2 初始化完成。";
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            StatusText.Text = "独立预览窗口：未检测到 WebView2 Runtime。请安装 Microsoft Edge WebView2 Runtime。";
            _logger.Error("Detached WebView2 Runtime not found.", ex);
            throw;
        }
    }

    private async void NavigateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await NavigateAsync(AddressBox.Text);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"独立预览窗口：打开失败 - {ex.Message}";
            _logger.Error("Detached preview navigation failed.", ex);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastUri))
            {
                StatusText.Text = "独立预览窗口：没有可刷新的页面。";
                return;
            }

            await EnsurePreviewAsync();
            DetachedPreviewWebView.Reload();
            StatusText.Text = $"独立预览窗口：刷新 {_lastUri}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"独立预览窗口：刷新失败 - {ex.Message}";
            _logger.Error("Detached preview refresh failed.", ex);
        }
    }

    private void OpenExternalButton_Click(object sender, RoutedEventArgs e)
    {
        var uri = string.IsNullOrWhiteSpace(AddressBox.Text) ? _lastUri : AddressBox.Text;
        if (string.IsNullOrWhiteSpace(uri))
        {
            StatusText.Text = "独立预览窗口：没有可外部打开的地址。";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = NormalizeInputUri(uri),
            UseShellExecute = true
        });
    }

    private void Desktop1366Button_Click(object sender, RoutedEventArgs e)
    {
        ApplyWindowSize(1366, 768);
    }

    private void Desktop1440Button_Click(object sender, RoutedEventArgs e)
    {
        ApplyWindowSize(1440, 900);
    }

    private void Desktop1920Button_Click(object sender, RoutedEventArgs e)
    {
        ApplyWindowSize(1920, 1080);
    }

    private void Tablet1024Button_Click(object sender, RoutedEventArgs e)
    {
        ApplyWindowSize(1024, 768);
    }

    private void Phone390Button_Click(object sender, RoutedEventArgs e)
    {
        ApplyWindowSize(390, 844);
    }

    private void ApplyWindowSize(double width, double height)
    {
        WindowState = WindowState.Normal;
        Width = width;
        Height = height;
        UpdateViewportStatus();
    }

    private void UpdateViewportStatus()
    {
        ViewportStatusText.Text = $"窗口尺寸：{Math.Round(ActualWidth)} × {Math.Round(ActualHeight)}";
    }

    private static string NormalizeInputUri(string input)
    {
        var value = input.Trim();

        if (File.Exists(value))
        {
            return new Uri(Path.GetFullPath(value)).AbsoluteUri;
        }

        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            return $"https://{value}";
        }

        return value;
    }
}
