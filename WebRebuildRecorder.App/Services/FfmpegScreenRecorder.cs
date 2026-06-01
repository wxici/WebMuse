using System.Diagnostics;
using System.Globalization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class FfmpegScreenRecorder : IScreenRecorder
{
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _stopLock = new(1, 1);
    private Process? _process;

    public FfmpegScreenRecorder(AppLogger logger)
    {
        _logger = logger;
    }

    public bool IsRecording
    {
        get
        {
            var process = _process;
            if (process is null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
    public DateTime? StartedAt { get; private set; }

    public Task StartAsync(RecordingOptions options)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("录屏已经在进行中。");
        }

        EnsureExecutableLooksValid(options.FfmpegPath);
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);

        var startInfo = new ProcessStartInfo
        {
            FileName = options.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("gdigrab");
        startInfo.ArgumentList.Add("-framerate");
        startInfo.ArgumentList.Add(options.FrameRate.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-draw_mouse");
        startInfo.ArgumentList.Add(options.DrawMouse ? "1" : "0");

        if (options.Area.IsRegion)
        {
            startInfo.ArgumentList.Add("-offset_x");
            startInfo.ArgumentList.Add(options.Area.X.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-offset_y");
            startInfo.ArgumentList.Add(options.Area.Y.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-video_size");
            startInfo.ArgumentList.Add($"{options.Area.Width}x{options.Area.Height}");
        }

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add("desktop");
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add(options.VideoCodec);
        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add(options.Preset);
        startInfo.ArgumentList.Add("-crf");
        startInfo.ArgumentList.Add(options.Crf.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(options.OutputPath);

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data) && e.Data.Contains("frame=", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"ffmpeg {e.Data}");
            }
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("FFmpeg 录屏进程启动失败。");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        StartedAt = DateTime.Now;
        _logger.Info($"录屏已开始：{options.OutputPath}");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _stopLock.WaitAsync();
        try
        {
            var process = _process;
            if (process is null)
            {
                return;
            }

            if (HasExited(process))
            {
        _logger.Info($"录屏已停止。退出码={GetExitCodeText(process)}");
                CleanupProcess(process);
                return;
            }

            try
            {
                await process.StandardInput.WriteLineAsync("q");
                await process.StandardInput.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not send q to FFmpeg: {ex.Message}");
            }

            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(8)));
            if (completed != waitTask && !HasExited(process))
            {
                _logger.Warn("FFmpeg did not exit after q. Killing process.");
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

        _logger.Info($"录屏已停止。退出码={GetExitCodeText(process)}");
            CleanupProcess(process);
        }
        finally
        {
            _stopLock.Release();
        }
    }

    private void CleanupProcess(Process process)
    {
        process.Dispose();
        if (ReferenceEquals(_process, process))
        {
            _process = null;
            StartedAt = null;
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static string GetExitCodeText(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "still-running";
        }
        catch
        {
            return "unknown";
        }
    }

    public static void EnsureExecutableLooksValid(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("FFmpeg 路径不能为空。");
        }

        var containsPath = executablePath.Contains(Path.DirectorySeparatorChar)
            || executablePath.Contains(Path.AltDirectorySeparatorChar)
            || Path.IsPathRooted(executablePath);

        if (containsPath && !File.Exists(executablePath))
        {
            throw new FileNotFoundException("FFmpeg 路径不存在。", executablePath);
        }
    }
}
