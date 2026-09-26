using System.Text;
using System.Windows;
using NetOps.Core.Jobs;

namespace NetOps.App;

/// <summary>
/// Vault-backed scheduled backups. Handlers are wired from XAML (not decorative).
/// Requires saved vault entries with matching Vendor filter.
/// </summary>
public partial class MainWindow
{
    private ScheduledJobService? _scheduler;

    private ScheduledJobService Scheduler
    {
        get
        {
            if (_scheduler is null)
            {
                _scheduler = new ScheduledJobService(_jobs);
                _scheduler.Changed += () => Dispatcher.Invoke(RefreshJobsAndSchedule);
            }
            return _scheduler;
        }
    }

    // Keep empty for NavExtras compatibility — XAML owns the buttons now.
    private void EnsureScheduleButtons() { _ = Scheduler; }

    private void ScheduleMt_Click(object s, RoutedEventArgs e)
        => RegisterSchedule(JobKind.MikroTikExport, "MikroTik");

    private void ScheduleCisco_Click(object s, RoutedEventArgs e)
        => RegisterSchedule(JobKind.CiscoShowRun, "Cisco");

    private void ScheduleHuawei_Click(object s, RoutedEventArgs e)
        => RegisterSchedule(JobKind.HuaweiConfig, "Huawei");

    private void RegisterSchedule(JobKind kind, string vendor)
    {
        try { _vault.Load(); } catch { /* keep in-memory */ }

        var matching = _vault.Entries
            .Where(v => v.Vendor.Contains(vendor, StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(v.Vendor))
            .ToList();

        var entry = Scheduler.Upsert(
            id: vendor.ToLowerInvariant() + "-60",
            kind: kind,
            vendorFilter: vendor,
            interval: TimeSpan.FromMinutes(60),
            getEntries: () =>
            {
                try { _vault.Load(); } catch { }
                return _vault.Entries;
            },
            unprotectLogin: _vault.UnprotectPassword,
            unprotectEnable: _vault.UnprotectEnablePassword);

        var sb = new StringBuilder();
        sb.AppendLine("Schedule registered: " + entry.SummaryLine());
        sb.AppendLine($"Vault entries total: {_vault.Entries.Count}");
        sb.AppendLine($"Matching vendor '{vendor}' (or empty vendor): {matching.Count}");
        if (matching.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No matching vault rows yet.");
            sb.AppendLine("1) Devices → fill host/user/pass → Vault Save");
            sb.AppendLine("2) Set Brand to " + vendor + " when saving (recommended)");
            sb.AppendLine("3) Jobs → Schedule run due  (or wait until next hour)");
        }
        else
        {
            sb.AppendLine("Click 'Schedule run due' to queue now (does not wait 60m).");
        }

        JobsPanelText.Text = sb.ToString();
        LogJob("Schedule", entry.Id + " vault=" + matching.Count);
    }

    private void ScheduleRunDue_Click(object s, RoutedEventArgs e)
    {
        try { _vault.Load(); } catch { }
        var before = _jobs.Snapshot().Count;
        Scheduler.RunDueNow();
        // Force due: set NextRunAt past for all and tick again if none due
        foreach (var e2 in Scheduler.Snapshot())
        {
            if (e2.Enabled)
                e2.NextRunAt = DateTimeOffset.Now.AddSeconds(-1);
        }
        Scheduler.RunDueNow();
        RefreshJobsAndSchedule();
        var after = _jobs.Snapshot().Count;
        JobsPanelText.Text =
            $"Run due executed. Jobs before={before} after={after}\n\n" + JobsPanelText.Text;
        LogJob("ScheduleDue", $"jobs={after}");
    }

    private void ScheduleList_Click(object s, RoutedEventArgs e) => RefreshJobsAndSchedule();

    private void ScheduleClear_Click(object s, RoutedEventArgs e)
    {
        foreach (var e2 in Scheduler.Snapshot().ToList())
            Scheduler.Remove(e2.Id);
        RefreshJobsAndSchedule();
        JobsPanelText.Text = "All schedules cleared.\n\n" + JobsPanelText.Text;
        LogJob("ScheduleClear", "OK");
    }

    private void RefreshJobsAndSchedule()
    {
        var sb = new StringBuilder();
        var schedules = Scheduler.Snapshot();
        if (schedules.Count > 0)
        {
            sb.AppendLine("=== Schedules ===");
            foreach (var e in schedules)
                sb.AppendLine(e.SummaryLine());
            sb.AppendLine();
        }

        sb.AppendLine("=== Jobs ===");
        var jobs = _jobs.Snapshot().Take(40).ToList();
        if (jobs.Count == 0)
            sb.AppendLine("No jobs yet. Queue from vault or wait for schedule.");
        foreach (var j in jobs)
        {
            sb.AppendLine($"{j.CreatedAt:HH:mm:ss}  {j.Status,-10}  {j.Kind,-16}  {j.Host}  {j.Message}");
            if (!string.IsNullOrEmpty(j.LocalPath))
                sb.AppendLine("         → " + j.LocalPath);
        }

        JobsPanelText.Text = sb.ToString();
        var last = _jobs.Snapshot().Take(8);
        JobsText.Text = string.Join("\n", last.Select(j =>
            $"{j.CreatedAt:HH:mm}  {j.Kind.ToString().PadRight(14)} {j.Status}"));
    }
}
