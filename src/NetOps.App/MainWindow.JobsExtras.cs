using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Jobs;

namespace NetOps.App;

public partial class MainWindow
{
    private bool _jobExtrasWired;

    private void EnsureJobExtraButtons()
    {
        if (_jobExtrasWired) return;
        try
        {
            var root = ContentJobs as DependencyObject;
            if (root is null) return;
            var panel = FindWrap(root);
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

            Add("Queue Junos all", QueueJunosAll_Click);
            Add("Queue Aruba all", QueueArubaAll_Click);
            Add("Queue Forti all", QueueFortiAll_Click);
            _jobExtrasWired = true;
        }
        catch { }
    }

    private static WrapPanel? FindWrap(DependencyObject root)
    {
        if (root is WrapPanel wp) return wp;
        var n = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < n; i++)
        {
            var f = FindWrap(VisualTreeHelper.GetChild(root, i));
            if (f is not null) return f;
        }
        if (root is Panel p)
        {
            foreach (var c in p.Children)
            {
                if (c is WrapPanel w) return w;
                if (c is DependencyObject d)
                {
                    var f = FindWrap(d);
                    if (f is not null) return f;
                }
            }
        }
        return null;
    }

    private void QueueJunosAll_Click(object s, RoutedEventArgs e) => QueueAll(JobKind.JuniperConfig, "Juniper");
    private void QueueArubaAll_Click(object s, RoutedEventArgs e) => QueueAll(JobKind.ArubaShowRun, "Aruba");
    private void QueueFortiAll_Click(object s, RoutedEventArgs e) => QueueAll(JobKind.FortinetConfig, "Fortinet");
}
