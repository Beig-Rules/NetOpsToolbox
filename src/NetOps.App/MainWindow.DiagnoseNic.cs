using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Tools;

namespace NetOps.App;

/// <summary>Diagnose panel: NIC combo for Set DNS (modular UI inject).</summary>
public partial class MainWindow
{
    private ComboBox? _dnsNicBox;
    private bool _diagnoseNicWired;

    private void EnsureDiagnoseNicPicker()
    {
        if (_diagnoseNicWired) return;
        try
        {
            var host = ContentDiagnose as Panel
                       ?? FindVisualChild<DockPanel>(ContentDiagnose);
            if (host is null) return;

            // Prefer first WrapPanel in Diagnose
            var wrap = FindVisualChild<WrapPanel>(host);
            if (wrap is null) return;

            var label = new TextBlock
            {
                Text = "NIC for DNS:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 6, 6),
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11
            };
            _dnsNicBox = new ComboBox
            {
                Width = 200,
                Margin = new Thickness(0, 0, 8, 6),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            RefreshDnsNicList();
            wrap.Children.Insert(0, label);
            wrap.Children.Insert(1, _dnsNicBox);
            _diagnoseNicWired = true;
        }
        catch { /* non-fatal */ }
    }

    private void RefreshDnsNicList()
    {
        if (_dnsNicBox is null) return;
        _dnsNicBox.Items.Clear();
        foreach (var n in NicInventory.ListUseful())
        {
            var mark = n.IsUp ? (n.HasGateway ? "*" : "+") : "-";
            _dnsNicBox.Items.Add($"{mark} {n.Name}");
        }
        var prefer = NicInventory.PreferDefault();
        if (prefer is not null)
        {
            for (var i = 0; i < _dnsNicBox.Items.Count; i++)
            {
                if (_dnsNicBox.Items[i]?.ToString()?.Contains(prefer, StringComparison.Ordinal) == true)
                {
                    _dnsNicBox.SelectedIndex = i;
                    break;
                }
            }
        }
        else if (_dnsNicBox.Items.Count > 0)
            _dnsNicBox.SelectedIndex = 0;
    }

    private string? SelectedDnsNicName()
    {
        var s = _dnsNicBox?.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(s)) return NicInventory.PreferDefault();
        // strip leading mark "* " / "+ " / "- "
        var t = s.Trim();
        if (t.Length > 2 && (t[0] is '*' or '+' or '-') && t[1] == ' ')
            return t[2..].Trim();
        return t;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        if (parent is T t) return t;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        if (parent is Panel panel)
        {
            foreach (var c in panel.Children)
            {
                if (c is T ct) return ct;
                if (c is DependencyObject d)
                {
                    var f = FindVisualChild<T>(d);
                    if (f is not null) return f;
                }
            }
        }
        return null;
    }
}
