using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly ProxyPacService _proxy = new();
    private bool _proxyBtnWired;

    private void EnsureProxyButton()
    {
        if (_proxyBtnWired) return;
        try
        {
            var panel = FindToolsWrapPanel(ContentTools);
            if (panel is null) return;
            if (panel.Children.OfType<Button>().Any(b => (b.Content as string) == "Proxy"))
            {
                _proxyBtnWired = true;
                return;
            }
            var btn = new Button
            {
                Content = "Proxy",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(Border.BorderBrushProperty, "TextPrimary");
            btn.Click += ToolProxy_Click;
            panel.Children.Add(btn);
            _proxyBtnWired = true;
        }
        catch { }
    }

    private void ToolProxy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ToolsOutput.Text = _proxy.Run();
            LogJob("Proxy", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Proxy", "FAIL");
        }
    }
}
