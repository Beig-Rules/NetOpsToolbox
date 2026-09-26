using System.Collections.Concurrent;
using NetOps.Core.Devices;
using NetOps.Core.Security;

namespace NetOps.Core.Jobs;

public enum JobKind
{
    MikroTikExport,
    CiscoShowRun,
    CiscoVersion,
    UbiquitiExport,
    UbiquitiIdentity,
    JuniperConfig,
    JuniperVersion,
    ArubaShowRun,
    ArubaVersion,
    FortinetConfig,
    FortinetStatus,
    PaloAltoConfig,
    PaloAltoInfo
}

public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed class DeviceJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public JobKind Kind { get; init; }
    public string Host { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string? EnablePassword { get; init; }
    public int Port { get; init; } = 22;
    public string Vendor { get; init; } = "";
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string? Message { get; set; }
    public string? LocalPath { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>
/// Sequential multi-device job runner (safe default concurrency for SSH stability).
/// </summary>
public sealed class JobQueue
{
    private readonly ConcurrentQueue<DeviceJob> _queue = new();
    private readonly List<DeviceJob> _history = new();
    private readonly object _lock = new();
    private readonly MikroTikSshService _mt = new();
    private readonly CiscoSshService _cisco = new();
    private readonly UbiquitiSshService _ubnt = new();
    private readonly JuniperSshService _junos = new();
    private readonly ArubaSshService _aruba = new();
    private readonly FortinetSshService _forti = new();
    private readonly PaloAltoSshService _palo = new();
    private int _running;

    public int MaxConcurrency { get; set; } = 2;
    public string BackupDirectory { get; set; } = "";

    public event Action? Changed;

    public IReadOnlyList<DeviceJob> Snapshot()
    {
        lock (_lock)
            return _history.Concat(_queue).OrderByDescending(j => j.CreatedAt).ToList();
    }

    public void Enqueue(DeviceJob job)
    {
        _queue.Enqueue(job);
        lock (_lock) _history.Add(job);
        Changed?.Invoke();
        _ = PumpAsync();
    }

    public void EnqueueFromVault(
        IEnumerable<VaultEntry> entries,
        JobKind kind,
        Func<VaultEntry, string?> unprotectLogin,
        Func<VaultEntry, string?> unprotectEnable)
    {
        foreach (var e in entries)
        {
            var pass = unprotectLogin(e);
            if (string.IsNullOrEmpty(pass)) continue;
            Enqueue(new DeviceJob
            {
                Kind = kind,
                Host = e.Host,
                Username = e.Username,
                Password = pass,
                EnablePassword = unprotectEnable(e),
                Port = e.Port,
                Vendor = e.Vendor
            });
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            Interlocked.CompareExchange(ref _running, 1, 0);

            var started = 0;
            while (started < MaxConcurrency && _queue.TryDequeue(out var job))
            {
                started++;
                _ = RunOneAsync(job);
            }

            if (started == 0)
            {
                Interlocked.Exchange(ref _running, 0);
                return;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    private async Task RunOneAsync(DeviceJob job)
    {
        job.Status = JobStatus.Running;
        Changed?.Invoke();
        try
        {
            DeviceBackupResult result = job.Kind switch
            {
                JobKind.MikroTikExport => await _mt.ExportConfigAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port).ConfigureAwait(false),
                JobKind.CiscoShowRun => await _cisco.ShowRunningConfigAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port, job.EnablePassword).ConfigureAwait(false),
                JobKind.CiscoVersion => await _cisco.ShowVersionAsync(
                    job.Host, job.Username, job.Password, job.Port, job.EnablePassword).ConfigureAwait(false),
                JobKind.UbiquitiExport => await _ubnt.ExportConfigAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port).ConfigureAwait(false),
                JobKind.UbiquitiIdentity => await _ubnt.IdentityAsync(
                    job.Host, job.Username, job.Password, job.Port).ConfigureAwait(false),
                JobKind.JuniperConfig => await _junos.ShowConfigAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port).ConfigureAwait(false),
                JobKind.JuniperVersion => await _junos.ShowVersionAsync(
                    job.Host, job.Username, job.Password, job.Port).ConfigureAwait(false),
                JobKind.ArubaShowRun => await _aruba.ShowRunningConfigAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port, job.EnablePassword).ConfigureAwait(false),
                JobKind.ArubaVersion => await _aruba.ShowVersionAsync(
                    job.Host, job.Username, job.Password, job.Port, job.EnablePassword).ConfigureAwait(false),
                JobKind.FortinetConfig => await _forti.ShowFullConfigAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port).ConfigureAwait(false),
                JobKind.FortinetStatus => await _forti.GetSystemStatusAsync(
                    job.Host, job.Username, job.Password, job.Port).ConfigureAwait(false),
                JobKind.PaloAltoConfig => await _palo.ShowConfigRunningAsync(
                    job.Host, job.Username, job.Password, BackupDirectory, job.Port).ConfigureAwait(false),
                JobKind.PaloAltoInfo => await _palo.ShowSystemInfoAsync(
                    job.Host, job.Username, job.Password, job.Port).ConfigureAwait(false),
                _ => new DeviceBackupResult { Success = false, Message = "Unknown job kind." }
            };

            job.Status = result.Success ? JobStatus.Succeeded : JobStatus.Failed;
            job.Message = result.Message;
            job.LocalPath = result.LocalPath;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Message = ex.Message;
        }
        finally
        {
            job.FinishedAt = DateTimeOffset.Now;
            job.Password = "";
            job.EnablePassword = null;
            Changed?.Invoke();
            _ = PumpAsync();
        }
    }
}
