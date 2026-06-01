using System.Text;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class MarkerService
{
    private readonly AppLogger _logger;
    private RebuildProject? _project;

    public MarkerService(AppLogger logger)
    {
        _logger = logger;
    }

    public void ResetForProject(RebuildProject project)
    {
        _project = project;
        var markersPath = Path.Combine(project.MarkersDirectory, "markers.md");
        Directory.CreateDirectory(project.MarkersDirectory);
        if (!File.Exists(markersPath))
        {
            File.WriteAllText(markersPath, CreateMarkerHeader(), Encoding.UTF8);
        }
    }

    public void ClearRuntime()
    {
        _project = null;
    }

    public async Task AddMarkerAsync(RebuildProject project, TimeSpan elapsed, string type, string? url, string description)
    {
        var markersPath = Path.Combine(project.MarkersDirectory, "markers.md");
        if (!File.Exists(markersPath))
        {
            await File.WriteAllTextAsync(markersPath, CreateMarkerHeader(), Encoding.UTF8);
        }

        var line = $"| {TimeUtil.FormatTime(elapsed)} | {Escape(type)} | {Escape(url ?? string.Empty)} | {Escape(description)} |{Environment.NewLine}";
        await File.AppendAllTextAsync(markersPath, line, Encoding.UTF8);
        _logger.Info($"marker {TimeUtil.FormatTime(elapsed)} {type}");
    }

    public static string CreateMarkerHeader()
    {
        return "# Markers" + Environment.NewLine
            + Environment.NewLine
            + "| 时间点 | 类型 | URL | 说明 |" + Environment.NewLine
            + "|---|---|---|---|" + Environment.NewLine;
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|").Replace(Environment.NewLine, " ");
    }
}
