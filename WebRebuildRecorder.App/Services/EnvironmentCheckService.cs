using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using WebRebuildRecorder.App.Core.Security;

namespace WebRebuildRecorder.App.Services;

public sealed class EnvironmentCheckService
{
    public Task<EnvironmentCheckResult> CheckAsync(
        string? projectDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new List<EnvironmentCheckItem>
        {
            new()
            {
                Key = "os",
                DisplayName = "Operating system",
                Status = EnvironmentCheckStatus.Ok,
                Message = RuntimeInformation.OSDescription,
                Detail = RuntimeInformation.OSArchitecture.ToString()
            },
            new()
            {
                Key = "dotnetRuntime",
                DisplayName = ".NET runtime",
                Status = EnvironmentCheckStatus.Ok,
                Message = Environment.Version.ToString(),
                Detail = RuntimeInformation.FrameworkDescription
            },
            new()
            {
                Key = "webview2Runtime",
                DisplayName = "WebView2 Runtime",
                Status = EnvironmentCheckStatus.Skipped,
                Message = "Stub only in this round.",
                Detail = "No WebView2 integration or runtime probing is performed yet."
            },
            CheckFfmpeg(),
            new()
            {
                Key = "codexCli",
                DisplayName = "Codex CLI",
                Status = EnvironmentCheckStatus.Skipped,
                Message = "Stub only in this round.",
                Detail = "This round does not invoke or validate Codex CLI."
            },
            new()
            {
                Key = "codexLogin",
                DisplayName = "OpenAI/Codex login",
                Status = EnvironmentCheckStatus.Skipped,
                Message = "Stub only in this round.",
                Detail = "No account or credential inspection is performed."
            },
            CheckNetwork()
        };

        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            items.Add(CheckProjectDirectoryWritable(projectDirectory));
        }
        else
        {
            items.Add(new EnvironmentCheckItem
            {
                Key = "projectDirectoryWritable",
                DisplayName = "Project directory writable",
                Status = EnvironmentCheckStatus.Skipped,
                Message = "No project directory was provided."
            });
        }

        return Task.FromResult(new EnvironmentCheckResult
        {
            Items = items,
            IsOk = items.All(item => item.Status is not EnvironmentCheckStatus.Error)
        });
    }

    private static EnvironmentCheckItem CheckFfmpeg()
    {
        var ffmpegPath = ToolPathResolver.GetDefaultFfmpegPath();
        if (File.Exists(ffmpegPath))
        {
            return new EnvironmentCheckItem
            {
                Key = "ffmpeg",
                DisplayName = "FFmpeg",
                Status = EnvironmentCheckStatus.Ok,
                Message = "Bundled FFmpeg was found.",
                Detail = ffmpegPath
            };
        }

        return new EnvironmentCheckItem
        {
            Key = "ffmpeg",
            DisplayName = "FFmpeg",
            Status = EnvironmentCheckStatus.Warning,
            Message = "Bundled FFmpeg was not found; command fallback is not verified in this stub.",
            Detail = ffmpegPath
        };
    }

    private static EnvironmentCheckItem CheckNetwork()
    {
        return new EnvironmentCheckItem
        {
            Key = "network",
            DisplayName = "Network",
            Status = NetworkInterface.GetIsNetworkAvailable()
                ? EnvironmentCheckStatus.Unknown
                : EnvironmentCheckStatus.Warning,
            Message = NetworkInterface.GetIsNetworkAvailable()
                ? "A network adapter appears available; remote service reachability is not checked in this stub."
                : "No network adapter appears available.",
            Detail = "No remote endpoint is contacted in this round."
        };
    }

    private static EnvironmentCheckItem CheckProjectDirectoryWritable(string projectDirectory)
    {
        var validation = SandboxPathPolicy.ValidateProjectPath(
            projectDirectory,
            Path.Combine(projectDirectory, "logs", ".write-check"));

        if (!validation.IsAllowed)
        {
            return new EnvironmentCheckItem
            {
                Key = "projectDirectoryWritable",
                DisplayName = "Project directory writable",
                Status = EnvironmentCheckStatus.Error,
                Message = validation.Message,
                Detail = validation.NormalizedTargetPath
            };
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(validation.NormalizedTargetPath)!);
            File.WriteAllText(validation.NormalizedTargetPath, "ok");
            File.Delete(validation.NormalizedTargetPath);
            return new EnvironmentCheckItem
            {
                Key = "projectDirectoryWritable",
                DisplayName = "Project directory writable",
                Status = EnvironmentCheckStatus.Ok,
                Message = "Project directory is writable.",
                Detail = projectDirectory
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EnvironmentCheckItem
            {
                Key = "projectDirectoryWritable",
                DisplayName = "Project directory writable",
                Status = EnvironmentCheckStatus.Error,
                Message = ex.Message,
                Detail = projectDirectory
            };
        }
    }
}

public sealed class EnvironmentCheckResult
{
    public bool IsOk { get; init; }
    public List<EnvironmentCheckItem> Items { get; init; } = [];
}

public sealed class EnvironmentCheckItem
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EnvironmentCheckStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public enum EnvironmentCheckStatus
{
    Ok,
    Warning,
    Error,
    Unknown,
    Skipped
}
