using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly NetworkEventLogService _evt = new();
    private bool _evtBtnWired;

    private void EnsureEventLogButton()
    {
        if (_evtBtnWired) return;
        try
        {
            var panel = FindToolsWrapPanel(ContentTools);
            if (panel is null) return;
            if (panel.Children.OfType<Button>().Any(b => (b.Content as string) == "Event log"))
            {
                _evtBtnWired = true;
                return;
            }
            var btn = new Button
            {
                Content = "Event log",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(Border.BorderBrushProperty, "TextPrimary");
            btn.Click += ToolEventLog_Click;
            panel.Children.Add(btn);
            _evtBtnWired = true;
        }
        catch { }
    }

    private async void ToolEventLog_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Reading System log (network-related, last 24h)…";
        try
        {
            ToolsOutput.Text = await _evt.RunAsync().ConfigureAwait(true);
            LogJob("EventLog", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("EventLog", "FAIL");
        }
    }
}
