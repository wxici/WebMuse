using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WebRebuildRecorder.App.Views;

public partial class FloatingRecorderWindow : Window
{
    private double _opacityIdle = 0.70;
    private double _opacityHover = 0.95;
    private bool _expanded;
    private bool _minimized;

    public event EventHandler? AutoRecordRequested;
    public event EventHandler? ManualRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? MarkerRequested;
    public event EventHandler? ScreenshotRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? OpenDirectoryRequested;
    public event EventHandler? DeliveryRequested;
    public event EventHandler? EmergencyStopRequested;
    public event EventHandler? ManualPromptAcceptRequested;
    public event EventHandler? ManualPromptIgnoreRequested;
    public event EventHandler<int>? IntervalChanged;
    public event EventHandler<bool>? AutoExtractChanged;
    public event EventHandler? ToolbarLayoutChanged;

    public FloatingRecorderWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 80;
        Opacity = _opacityIdle;
        LocationChanged += (_, _) => ToolbarLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool AutoExtractAfterStop
    {
        get => AutoExtractCheckBox.IsChecked == true;
        set => AutoExtractCheckBox.IsChecked = value;
    }

    public int IntervalMs
    {
        get => int.TryParse(IntervalBox.Text, out var interval) ? interval : 500;
        set => IntervalBox.Text = value.ToString();
    }

    public string MarkerType
    {
        get
        {
            if (MarkerTypeCombo.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? item.Content?.ToString() ?? "other";
            }

            return "other";
        }
    }

    public string ToolbarMode => _minimized ? "minimized" : _expanded ? "expanded" : "compact";

    public void Configure(double? left, double top, double opacityIdle, double opacityHover, string mode)
    {
        _opacityIdle = Math.Clamp(opacityIdle, 0.30, 1.0);
        _opacityHover = Math.Clamp(opacityHover, _opacityIdle, 1.0);
        Opacity = _opacityIdle;

        if (left is not null)
        {
            Left = left.Value;
        }
        else
        {
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        }

        Top = SystemParameters.WorkArea.Top + Math.Max(0, top);
        SetExpanded(string.Equals(mode, "expanded", StringComparison.OrdinalIgnoreCase));
        if (string.Equals(mode, "minimized", StringComparison.OrdinalIgnoreCase))
        {
            SetMinimized(true);
        }
    }

    public void SetElapsed(TimeSpan elapsed)
    {
        ElapsedText.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    public void SetState(string state)
    {
        StateText.Text = state;
        if (!string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            RecGlyphText.Text = "●";
            RecGlyphText.Foreground = System.Windows.Media.Brushes.Firebrick;
            ManualButton.Visibility = Visibility.Visible;
            ResumeButton.Visibility = Visibility.Visible;
            MarkerButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Visible;
            DeliveryCompactButton.Visibility = Visibility.Collapsed;
            DirectoryCompactButton.Visibility = Visibility.Collapsed;
        }
    }

    public void SetFileSize(string sizeText)
    {
        FileSizeText.Text = sizeText;
    }

    public void SetSpeedPreset(string speedText)
    {
        ToolTip = $"自动观察速度：{speedText}";
    }

    public void SetExtractionProgress(int generatedFrames, int estimatedFrames)
    {
        StateText.Text = estimatedFrames <= 0
            ? $"抽帧 {generatedFrames}"
            : $"抽帧 {Math.Min(100, generatedFrames * 100 / estimatedFrames)}%";
    }

    public void SetExtractionCompleted(int totalFrames)
    {
        StateText.Text = $"截图 {totalFrames}";
    }

    public void SetCompleted(string packageSizeText)
    {
        RecGlyphText.Text = "✓";
        RecGlyphText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        ElapsedText.Text = "Done";
        FileSizeText.Text = packageSizeText;
        StateText.Text = "Completed";
        ManualButton.Visibility = Visibility.Collapsed;
        ResumeButton.Visibility = Visibility.Collapsed;
        MarkerButton.Visibility = Visibility.Collapsed;
        StopButton.Visibility = Visibility.Collapsed;
        DeliveryCompactButton.Visibility = Visibility.Visible;
        DirectoryCompactButton.Visibility = Visibility.Visible;
        SetExpanded(false);
        Show();
    }

    public void ShowManualTakeoverPrompt()
    {
        ManualPromptPanel.Visibility = Visibility.Visible;
        Height = _expanded ? 118 : 82;
    }

    public void HideManualTakeoverPrompt()
    {
        ManualPromptPanel.Visibility = Visibility.Collapsed;
        Height = _expanded ? 86 : 44;
    }

    private void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        ExpandedPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandButton.Content = expanded ? "▴" : "▾";
        Height = ManualPromptPanel.Visibility == Visibility.Visible
            ? expanded ? 118 : 82
            : expanded ? 86 : 44;
        Width = expanded ? 650 : 620;
        ToolbarLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetMinimized(bool minimized)
    {
        _minimized = minimized;
        MinimizedPanel.Visibility = minimized ? Visibility.Visible : Visibility.Collapsed;
        ToolbarPanel.Visibility = minimized ? Visibility.Collapsed : Visibility.Visible;
        Width = minimized ? 96 : _expanded ? 650 : 620;
        Height = minimized ? 36 : _expanded ? 86 : 44;
        ToolbarLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            EmergencyStopRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Opacity = _opacityHover;
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Opacity = _opacityIdle;
    }

    private void AutoRecordButton_Click(object sender, RoutedEventArgs e) => AutoRecordRequested?.Invoke(this, EventArgs.Empty);
    private void ManualButton_Click(object sender, RoutedEventArgs e) => ManualRequested?.Invoke(this, EventArgs.Empty);
    private void ResumeButton_Click(object sender, RoutedEventArgs e) => ResumeRequested?.Invoke(this, EventArgs.Empty);
    private void MarkerButton_Click(object sender, RoutedEventArgs e) => MarkerRequested?.Invoke(this, EventArgs.Empty);
    private void ScreenshotButton_Click(object sender, RoutedEventArgs e) => ScreenshotRequested?.Invoke(this, EventArgs.Empty);
    private void StopButton_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
    private void OpenDirectoryButton_Click(object sender, RoutedEventArgs e) => OpenDirectoryRequested?.Invoke(this, EventArgs.Empty);
    private void DeliveryButton_Click(object sender, RoutedEventArgs e) => DeliveryRequested?.Invoke(this, EventArgs.Empty);
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void ExpandButton_Click(object sender, RoutedEventArgs e) => SetExpanded(!_expanded);
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => SetMinimized(true);
    private void RestoreButton_Click(object sender, RoutedEventArgs e) => SetMinimized(false);
    private void ManualPromptAcceptButton_Click(object sender, RoutedEventArgs e) => ManualPromptAcceptRequested?.Invoke(this, EventArgs.Empty);
    private void ManualPromptIgnoreButton_Click(object sender, RoutedEventArgs e) => ManualPromptIgnoreRequested?.Invoke(this, EventArgs.Empty);

    private void IntervalPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string value } && int.TryParse(value, out var interval))
        {
            IntervalMs = interval;
            IntervalChanged?.Invoke(this, interval);
        }
    }

    private void IntervalBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(IntervalBox.Text, out var interval))
        {
            IntervalChanged?.Invoke(this, interval);
        }
    }

    private void AutoExtractCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        AutoExtractChanged?.Invoke(this, AutoExtractAfterStop);
    }
}
