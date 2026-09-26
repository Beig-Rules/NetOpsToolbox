using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Jobs;

namespace NetOps.App;

public partial class MainWindow
{
    private ScheduledJobService? _scheduler;
    private bool _scheduleWired;

    private ScheduledJobService Scheduler
    {
        get
        {
            if (_scheduler is null)
            {
                _scheduler = new ScheduledJobService(_jobs);
                _scheduler.Changed += () => Dispatcher.Invoke(RefreshSchedulePanel);
            }
            return _scheduler;
        }
    }

    private void EnsureScheduleButtons()
    {
        if (_scheduleWired) return;
        try
        {
            var root = ContentJobs as DependencyObject;
            if (root is null) return;
            var panel = FindScheduleWrap(root);
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

            Add("Schedule MT 60m", ScheduleMt_Click);
            Add("Schedule Cisco 60m", ScheduleCisco_Click);
            Add("Schedule run due", ScheduleRunDue_Click);
            Add("Schedule list", ScheduleList_Click);
            Add("Schedule clear", ScheduleClear_Click);
            _scheduleWired = true;
        }
        catch { }
    }

    private static WrapPanel? FindScheduleWrap(DependencyObject root)
    {
        if (root is WrapPanel wp) return wp;
        var n = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < n; i++)
        {
            var f = FindScheduleWrap(VisualTreeHelper.GetChild(root, i));
            if (f is not null) return f;
        }
        if (root is Panel p)
        {
            foreach (var c in p.Children)
            {
                if (c is WrapPanel w) return w;
                if (c is DependencyObject d)
                {
                    var f = FindScheduleWrap(d);
                    if (f is not null) return f;
                }
            }
        }
        return null;
    }

    private void ScheduleMt_Click(object s, RoutedEventArgs e)
        => AddSchedule(JobKind.MikroTikExport, "MikroTik");

    private void ScheduleCisco_Click(object s, RoutedEventArgs e)
        => AddSchedule(JobKind.CiscoShowRun, "Cisco");

    private void AddSchedule(JobKind kind, string vendor)
    {
        var entry = Scheduler.Upsert(
            id: vendor.ToLowerInvariant() + "-60",
            kind: kind,
            vendorFilter: vendor,
            interval: TimeSpan.FromMinutes(60),
            getEntries: () => _vault.Entries,
            unprotectLogin: _vault.UnprotectPassword,
            unprotectEnable: _vault.UnprotectEnablePassword);
        RefreshSchedulePanel();
        JobsPanelText.Text = (JobsPanelText.Text + "\nScheduled: " + entry.SummaryLine()).Trim();
        LogJob("Schedule", entry.Id);
    }

    private void ScheduleRunDue_Click(object s, RoutedEventArgs e)
    {
        Scheduler.RunDueNow();
        RefreshSchedulePanel();
        LogJob("ScheduleDue", "OK");
    }

    private void ScheduleList_Click(object s, RoutedEventArgs e) => RefreshSchedulePanel();

    private void ScheduleClear_Click(object s, RoutedEventArgs e)
    {
        foreach (var e2 in Scheduler.Snapshot().ToList())
            Scheduler.Remove(e2.Id);
        RefreshSchedulePanel();
        LogJob("ScheduleClear", "OK");
    }

    private void RefreshSchedulePanel()
    {
        var snap = Scheduler.Snapshot();
        if (snap.Count == 0)
        {
            // keep jobs panel content; only append note if empty schedules
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine("=== Schedules ===");
        foreach (var e in snap)
            sb.AppendLine(e.SummaryLine());
        sb.AppendLine();
        sb.AppendLine("=== Jobs ===");
        foreach (var j in _jobs.Snapshot().Take(30))
        {
            sb.AppendLine($"{j.CreatedAt:HH:mm:ss}  {j.Status,-10}  {j.Kind,-16}  {j.Host}  {j.Message}");
        }
        JobsPanelText.Text = sb.ToString();
    }
}
