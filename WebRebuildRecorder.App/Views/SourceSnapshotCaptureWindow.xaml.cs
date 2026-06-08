using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;
using WebRebuildRecorder.App.Services;

namespace WebRebuildRecorder.App.Views;

public partial class SourceSnapshotCaptureWindow : Window
{
    private readonly AppLogger _logger;

    public SourceSnapshotCaptureWindow(AppLogger logger)
    {
        InitializeComponent();
        _logger = logger;
    }

    public async Task<SourceSnapshotCaptureOutput> CaptureAsync(
        string url,
        SourceSnapshotViewportPreset viewport,
        string renderedEvidenceScript,
        TimeSpan timeout)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Controlled Source Snapshot capture requires an absolute HTTP or HTTPS URL.");
        }

        CaptureWebView.Width = viewport.Width;
        CaptureWebView.Height = viewport.Height;
        StatusText.Text = $"Initializing controlled viewport {viewport.Width}x{viewport.Height}...";

        if (!IsVisible)
        {
            Show();
        }

        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

        try
        {
            await CaptureWebView.EnsureCoreWebView2Async();
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            StatusText.Text = "WebView2 Runtime was not found.";
            _logger.Error("Controlled Source Snapshot WebView2 Runtime not found.", ex);
            throw;
        }

        var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                navigation.TrySetResult(true);
            }
            else
            {
                navigation.TrySetException(
                    new InvalidOperationException($"Controlled WebView2 navigation failed: {args.WebErrorStatus}"));
            }
        }

        CaptureWebView.CoreWebView2.NavigationCompleted += NavigationCompleted;
        try
        {
            StatusText.Text = $"Loading {uri.AbsoluteUri}";
            CaptureWebView.Source = uri;

            var completed = await Task.WhenAny(navigation.Task, Task.Delay(timeout));
            if (completed != navigation.Task)
            {
                throw new TimeoutException(
                    $"Controlled WebView2 navigation timed out after {timeout.TotalSeconds:n0} seconds.");
            }

            await navigation.Task;
            StatusText.Text = "Waiting for first-screen scripts and lazy media...";
            await Task.Delay(TimeSpan.FromMilliseconds(2500));

            var scriptResult = await CaptureWebView.CoreWebView2.ExecuteScriptAsync(renderedEvidenceScript);
            var evidence = JsonSerializer.Deserialize<SourceSnapshotRenderedEvidence>(
                scriptResult,
                WrbJsonOptions.Default)
                ?? throw new InvalidOperationException("Controlled WebView2 returned empty rendered evidence.");
            evidence.RenderSucceeded = true;

            await using var screenshot = new MemoryStream();
            await CaptureWebView.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png,
                screenshot);

            StatusText.Text =
                $"Captured {evidence.Viewport.Width}x{evidence.Viewport.Height}, {evidence.Elements.Count} evidence elements.";
            _logger.Info(
                $"Controlled Source Snapshot capture completed: requested={viewport.Width}x{viewport.Height}, actual={evidence.Viewport.Width}x{evidence.Viewport.Height}");

            return new SourceSnapshotCaptureOutput
            {
                Evidence = evidence,
                FirstScreenPngBytes = screenshot.ToArray()
            };
        }
        finally
        {
            CaptureWebView.CoreWebView2.NavigationCompleted -= NavigationCompleted;
        }
    }
}
