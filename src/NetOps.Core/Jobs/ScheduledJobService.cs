using System.Collections.Concurrent;
using NetOps.Core.Security;

namespace NetOps.Core.Jobs;

/// <summary>
/// Periodic vault-based job scheduler (read-only backups / version probes).
/// Safe defaults: min interval 5 minutes, max concurrency delegated to JobQueue.
/// </summary>
public sealed class ScheduledJobService : IDisposable
{
    private readonly JobQueue _queue;
    private readonly ConcurrentDictionary<string, ScheduleEntry> _entries = new();
    private Timer? _timer;
    private readonly object _gate = new();

    public ScheduledJobService(JobQueue queue) => _queue = queue;

    public IReadOnlyCollection<ScheduleEntry> Snapshot() => _entries.Values.OrderBy(e => e.Id).ToList();

    public event Action? Changed;

    /// <summary>Register or update a schedule. Interval clamped to ≥ 5 minutes.</summary>
    public ScheduleEntry Upsert(
        string id,
        JobKind kind,
        string vendorFilter,
        TimeSpan interval,
        Func<IEnumerable<VaultEntry>> getEntries,
        Func<VaultEntry, string?> unprotectLogin,
        Func<VaultEntry, string?> unprotectEnable)
    {
        if (interval < TimeSpan.FromMinutes(5))
            interval = TimeSpan.FromMinutes(5);

        var entry = new ScheduleEntry
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..8] : id.Trim(),
            Kind = kind,
            VendorFilter = vendorFilter ?? "",
            Interval = interval,
            Enabled = true,
            NextRunAt = DateTimeOffset.Now + interval,
            GetEntries = getEntries,
            UnprotectLogin = unprotectLogin,
            UnprotectEnable = unprotectEnable
        };
        _entries[entry.Id] = entry;
        EnsureTimer();
        Changed?.Invoke();
        return entry;
    }

    public bool Remove(string id)
    {
        var ok = _entries.TryRemove(id, out _);
        if (ok) Changed?.Invoke();
        if (_entries.IsEmpty) StopTimer();
        return ok;
    }

    public void SetEnabled(string id, bool enabled)
    {
        if (!_entries.TryGetValue(id, out var e)) return;
        e.Enabled = enabled;
        if (enabled && e.NextRunAt < DateTimeOffset.Now)
            e.NextRunAt = DateTimeOffset.Now + e.Interval;
        Changed?.Invoke();
    }

    public void RunDueNow()
    {
        lock (_gate) Tick();
    }

    private void EnsureTimer()
    {
        lock (_gate)
        {
            _timer ??= new Timer(_ =>
            {
                try { Tick(); }
                catch { /* never crash host */ }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
        }
    }

    private void StopTimer()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Tick()
    {
        var now = DateTimeOffset.Now;
        foreach (var e in _entries.Values.Where(x => x.Enabled))
        {
            if (e.NextRunAt > now) continue;
            try
            {
                var list = e.GetEntries()
                    .Where(v => string.IsNullOrEmpty(e.VendorFilter)
                                || v.Vendor.Contains(e.VendorFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (list.Count > 0)
                {
                    _queue.EnqueueFromVault(list, e.Kind, e.UnprotectLogin, e.UnprotectEnable);
                    e.LastRunAt = now;
                    e.LastMessage = $"Queued {list.Count} job(s) {e.Kind}";
                }
                else
                {
                    e.LastRunAt = now;
                    e.LastMessage = "No vault entries matched filter.";
                }
            }
            catch (Exception ex)
            {
                e.LastRunAt = now;
                e.LastMessage = "Error: " + ex.Message;
            }
            e.NextRunAt = now + e.Interval;
        }
        Changed?.Invoke();
    }

    public void Dispose() => StopTimer();
}

public sealed class ScheduleEntry
{
    public string Id { get; init; } = "";
    public JobKind Kind { get; init; }
    public string VendorFilter { get; set; } = "";
    public TimeSpan Interval { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastMessage { get; set; }

    internal Func<IEnumerable<VaultEntry>> GetEntries { get; init; } = () => Array.Empty<VaultEntry>();
    internal Func<VaultEntry, string?> UnprotectLogin { get; init; } = _ => null;
    internal Func<VaultEntry, string?> UnprotectEnable { get; init; } = _ => null;

    public string SummaryLine()
        => $"{Id}  {(Enabled ? "ON" : "OFF")}  {Kind}  every {Interval.TotalMinutes:0}m  vendor={VendorFilter}  next={NextRunAt:HH:mm}  last={LastMessage}";
}
