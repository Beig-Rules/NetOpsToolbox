using System.Text;
using System.Windows;
using NetOps.Core.Jobs;

namespace NetOps.App;

/// <summary>Vault-backed schedules — handlers bound from XAML.</summary>
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

    private void EnsureScheduleButtons() => _ = Scheduler;

    private void ScheduleMt_Click(object s, RoutedEventArgs e)
        => RegisterSchedule(JobKind.MikroTikExport, "MikroTik");

    private void ScheduleCisco_Click(object s, RoutedEventArgs e)
        => RegisterSchedule(JobKind.CiscoShowRun, "Cisco");

    private void ScheduleHuawei_Click(object s, RoutedEventArgs e)
        => RegisterSchedule(JobKind.HuaweiConfig, "Huawei");

    private void RegisterSchedule(JobKind kind, string vendor)
    {
        try { _vault.Load(); } catch { }

        var matching = _vault.Entries
            .Where(v => string.IsNullOrWhiteSpace(v.Vendor)
                        || v.Vendor.Contains(vendor, StringComparison.OrdinalIgnoreCase))
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
        sb.AppendLine("Schedule registered:");
        sb.AppendLine(entry.SummaryLine());
        sb.AppendLine($"Vault total: {_vault.Entries.Count}  matching '{vendor}': {matching.Count}");
        if (matching.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Nothing will queue until Vault has credentials:");
            sb.AppendLine("  Devices → host/user/password → choose Brand → Vault Save");
            sb.AppendLine("  then Jobs → Schedule run due");
        }
        else
        {
            sb.AppendLine("Press Schedule run due to queue SSH jobs now.");
        }

        JobsPanelText.Text = sb.ToString();
        LogJob("Schedule", $"{entry.Id} match={matching.Count}");
    }

    private void ScheduleRunDue_Click(object s, RoutedEventArgs e)
    {
        try { _vault.Load(); } catch { }
        var before = _jobs.Snapshot().Count;
        var n = Scheduler.ForceDueAll();
        // allow queue pump a moment
        RefreshJobsAndSchedule();
        var after = _jobs.Snapshot().Count;
        JobsPanelText.Text =
            $"Force due on {n} schedule(s). Jobs {before} → {after}\n\n" + JobsPanelText.Text;
        LogJob("ScheduleDue", $"{before}->{after}");
    }

    private void ScheduleList_Click(object s, RoutedEventArgs e) => RefreshJobsAndSchedule();

    private void ScheduleClear_Click(object s, RoutedEventArgs e)
    {
        foreach (var e2 in Scheduler.Snapshot().ToList())
            Scheduler.Remove(e2.Id);
        RefreshJobsAndSchedule();
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
            sb.AppendLine("No jobs. Save Vault entries, then Queue or Schedule run due.");
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
