using System.Windows;
using System.Windows.Input;
using WebRebuildRecorder.App.Models;
using WebRebuildRecorder.App.Services;

namespace WebRebuildRecorder.App.Views;

public partial class RegionSelectorWindow : Window
{
    private System.Windows.Point _start;
    private bool _dragging;

    public RecordingArea? SelectedArea { get; private set; }

    public RegionSelectorWindow()
    {
        InitializeComponent();
        var bounds = ScreenCaptureService.GetVirtualScreenBounds();
        Left = bounds.X;
        Top = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _dragging = true;
        _start = e.GetPosition(RootCanvas);
        SelectionRect.Visibility = Visibility.Visible;
        InfoBadge.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start, _start);
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        UpdateSelection(_start, e.GetPosition(RootCanvas));
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        var area = CreateArea(_start, e.GetPosition(RootCanvas));
        if (area.Width < 20 || area.Height < 20)
        {
            DialogResult = false;
            return;
        }

        SelectedArea = area;
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void UpdateSelection(System.Windows.Point start, System.Windows.Point current)
    {
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(start.X - current.X);
        var height = Math.Abs(start.Y - current.Y);

        System.Windows.Controls.Canvas.SetLeft(SelectionRect, x);
        System.Windows.Controls.Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;

        System.Windows.Controls.Canvas.SetLeft(InfoBadge, x + 8);
        System.Windows.Controls.Canvas.SetTop(InfoBadge, y + 8);

        var area = CreateArea(start, current);
        InfoText.Text = $"x={area.X}, y={area.Y}, {area.Width} x {area.Height}";
    }

    private RecordingArea CreateArea(System.Windows.Point start, System.Windows.Point current)
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(start.X - current.X);
        var height = Math.Abs(start.Y - current.Y);

        return new RecordingArea
        {
            Mode = RecordingAreaModes.Region,
            X = (int)Math.Round((Left + x) * transform.M11),
            Y = (int)Math.Round((Top + y) * transform.M22),
            Width = (int)Math.Round(width * transform.M11),
            Height = (int)Math.Round(height * transform.M22)
        };
    }
}
