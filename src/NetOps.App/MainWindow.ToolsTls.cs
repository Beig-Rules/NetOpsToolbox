using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly TlsProbeService _tls = new();
    private bool _extraToolsWired;

    /// <summary>Called from Nav when Tools opens — inject TLS if not present.</summary>
    private void EnsureExtraToolButtons()
    {
        if (_extraToolsWired) return;
        try
        {
            var panel = FindToolsWrapPanel(ContentTools);
            if (panel is null) return;
            if (panel.Children.OfType<Button>().Any(b => (b.Content as string) == "TLS"))
            {
                _extraToolsWired = true;
                return;
            }
            var btn = new Button
            {
                Content = "TLS",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(Border.BorderBrushProperty, "TextPrimary");
            btn.Click += ToolTls_Click;
            panel.Children.Add(btn);
            _extraToolsWired = true;
        }
        catch { /* non-fatal */ }
    }

    private static WrapPanel? FindToolsWrapPanel(DependencyObject root)
    {
        if (root is WrapPanel wp) return wp;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindToolsWrapPanel(child);
            if (found is not null) return found;
        }
        // logical tree fallback
        if (root is Panel p)
        {
            foreach (var c in p.Children)
            {
                if (c is WrapPanel w) return w;
                if (c is DependencyObject d)
                {
                    var f = FindToolsWrapPanel(d);
                    if (f is not null) return f;
                }
            }
        }
        return null;
    }

    private async void ToolTls_Click(object sender, RoutedEventArgs e)
    {
        var parts = ToolsInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var raw = parts.Length > 0 ? parts[0] : "example.com";
        var host = raw;
        var port = 443;
        if (raw.Contains(':'))
        {
            var p = raw.Split(':');
            host = p[0];
            if (p.Length > 1 && int.TryParse(p[1], out var pr)) port = pr;
        }
        ToolsOutput.Text = $"TLS probe {host}:{port}…";
        try
        {
            ToolsOutput.Text = await _tls.RunAsync(host, port).ConfigureAwait(true);
            LogJob("TLS", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("TLS", "FAIL");
        }
    }
}
