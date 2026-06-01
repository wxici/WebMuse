using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.ViewModels;

public sealed class RecordingViewModel
{
    public int FrameRate { get; set; } = 30;
    public int Crf { get; set; } = 23;
    public RecordingArea Area { get; set; } = RecordingArea.FullScreen();
}
