using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class FutureWebView2BrowserController : IBrowserController
{
    public Task OpenAsync(string url, BrowserProfile profile) => throw new NotSupportedException("WebView2 控制器为后续版本预留。");
    public Task StartAutoObserveAsync(ActionProfile profile, CancellationToken token) => Task.CompletedTask;
    public Task PauseAutoObserveAsync() => Task.CompletedTask;
    public Task ResumeAutoObserveAsync() => Task.CompletedTask;
    public Task StopAutoObserveAsync() => Task.CompletedTask;
    public Task<string?> GetCurrentUrlAsync() => Task.FromResult<string?>(null);
    public Task<string?> GetPageTitleAsync() => Task.FromResult<string?>(null);
}
