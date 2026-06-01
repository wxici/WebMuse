using System.Diagnostics;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class ExternalBrowserController : IBrowserController
{
    private readonly AppLogger _logger;

    public ExternalBrowserController(AppLogger logger)
    {
        _logger = logger;
    }

    public Task OpenAsync(string url, BrowserProfile profile)
    {
        if (string.Equals(profile.DefaultBrowser, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("已选择手动浏览器模式。录屏前请手动打开参考网站。");
            return Task.CompletedTask;
        }

        var processStartInfo = CreateBrowserStartInfo(url, profile.DefaultBrowser);
        Process.Start(processStartInfo);
        _logger.Info($"浏览器已打开：{profile.DefaultBrowser} {url}");
        return Task.CompletedTask;
    }

    public Task StartAutoObserveAsync(ActionProfile profile, CancellationToken token)
    {
        _logger.Info("外部浏览器模式不执行 DOM 自动化，保持人工操作。");
        return Task.CompletedTask;
    }

    public Task PauseAutoObserveAsync()
    {
        _logger.Info("Automation paused.");
        return Task.CompletedTask;
    }

    public Task ResumeAutoObserveAsync()
    {
        _logger.Info("Automation resumed.");
        return Task.CompletedTask;
    }

    public Task StopAutoObserveAsync()
    {
        _logger.Info("自动观察已停止。");
        return Task.CompletedTask;
    }

    public Task<string?> GetCurrentUrlAsync() => Task.FromResult<string?>(null);

    public Task<string?> GetPageTitleAsync() => Task.FromResult<string?>(null);

    private static ProcessStartInfo CreateBrowserStartInfo(string url, string browser)
    {
        if (string.Equals(browser, "Edge", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = ResolveBrowserPath("msedge.exe", BrowserEdgeCandidates()),
                Arguments = url,
                UseShellExecute = true
            };
        }

        if (string.Equals(browser, "Chrome", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = ResolveBrowserPath("chrome.exe", BrowserChromeCandidates()),
                Arguments = url,
                UseShellExecute = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
    }

    private static string ResolveBrowserPath(string executableName, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return executableName;
    }

    private static IEnumerable<string> BrowserEdgeCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe");
    }

    private static IEnumerable<string> BrowserChromeCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe");
    }
}
