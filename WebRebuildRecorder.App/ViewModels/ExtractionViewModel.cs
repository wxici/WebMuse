namespace WebRebuildRecorder.App.ViewModels;

public sealed class ExtractionViewModel
{
    public int IntervalMs { get; set; } = 500;
    public string Format { get; set; } = "JPEG";
    public int Quality { get; set; } = 90;
}
