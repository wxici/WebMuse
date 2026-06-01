using System.Windows;

namespace WebRebuildRecorder.App.Views;

public partial class CountdownWindow : Window
{
    public CountdownWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 2;
    }

    public async Task RunAsync(string finalText, CancellationToken token = default)
    {
        Show();
        foreach (var text in new[] { "3", "2", "1", finalText })
        {
            token.ThrowIfCancellationRequested();
            CountdownText.Text = text;
            await Task.Delay(text == finalText ? 650 : 900, token);
        }

        Close();
    }
}
