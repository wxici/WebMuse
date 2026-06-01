using System.Text;

namespace WebRebuildRecorder.App.Services;

public sealed class AppLogger
{
    private readonly object _sync = new();
    private string? _logFilePath;

    public event EventHandler<string>? LineWritten;

    public void AttachLogFile(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null ? message : $"{message}: {exception.Message}";
        Write("ERROR", fullMessage);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(_logFilePath))
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        LineWritten?.Invoke(this, line);
    }
}
