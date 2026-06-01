using System.Windows;
using WebRebuildRecorder.App.Models;
using WebRebuildRecorder.App.Views;

namespace WebRebuildRecorder.App.Services;

public sealed class RegionSelectionService
{
    public RecordingArea? SelectRegion(Window owner)
    {
        var selector = new RegionSelectorWindow
        {
            Owner = owner
        };

        var result = selector.ShowDialog();
        return result == true ? selector.SelectedArea : null;
    }
}
