using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly SnmpGetService _snmp = new();
    private readonly WindowsFirewallService _fw = new();
    private bool _netExtrasWired;

    private void EnsureNetExtraButtons()
    {
        if (_netExtrasWired) return;
        try
        {
            var panel = FindToolsWrapPanel(ContentTools);
            if (panel is null) return;

            void Add(string title, RoutedEventHandler handler)
            {
                if (panel.Children.OfType<Button>().Any(b => (b.Content as string) == title))
                    return;
                var btn = new Button
                {
                    Content = title,
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 6, 6),
                    BorderThickness = new Thickness(1),
                    Background = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btn.SetResourceReference(Border.BorderBrushProperty, "TextPrimary");
                btn.Click += handler;
                panel.Children.Add(btn);
            }

            Add("SNMP", ToolSnmp_Click);
            Add("Firewall", ToolFirewall_Click);
            _netExtrasWired = true;
        }
        catch { }
    }

    private async void ToolSnmp_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "SNMP GET…";
        try
        {
            // Tools input: host  or host|community  or host|community|oid
            var raw = ToolsInput.Text.Trim();
            // If user typed comma-separated ping list, take first host only for SNMP
            if (!raw.Contains('|') && raw.Contains(','))
                raw = raw.Split(',')[0].Trim();

            if (raw.Contains('|') && raw.Split('|').Length == 2)
            {
                // host|community → system summary
                var p = raw.Split('|');
                ToolsOutput.Text = await _snmp.RunSystemSummaryAsync(p[0].Trim(), p[1].Trim()).ConfigureAwait(true);
            }
            else if (!raw.Contains('|') && !string.IsNullOrWhiteSpace(raw))
            {
                ToolsOutput.Text = await _snmp.RunSystemSummaryAsync(raw.Trim()).ConfigureAwait(true);
            }
            else
            {
                ToolsOutput.Text = await _snmp.RunAsync(raw).ConfigureAwait(true);
            }

            LogJob("SNMP", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("SNMP", "FAIL");
        }
    }

    private async void ToolFirewall_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Reading firewall profiles…";
        try
        {
            ToolsOutput.Text = await _fw.RunAsync().ConfigureAwait(true);
            LogJob("Firewall", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Firewall", "FAIL");
        }
    }
}
