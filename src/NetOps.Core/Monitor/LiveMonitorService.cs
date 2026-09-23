using System.Net.NetworkInformation;
using System.Text;

namespace NetOps.Core.Monitor;

public sealed class PingSample
{
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;
    public string Host { get; init; } = "";
    public bool Ok { get; init; }
    public long? RttMs { get; init; }
    public string Status { get; init; } = "";
}

public sealed class TrafficSample
{
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;
    public string Adapter { get; init; } = "";
    public long BytesSent { get; init; }
    public long BytesRecv { get; init; }
    public double SendMbps { get; init; }
    public double RecvMbps { get; init; }
}

/// <summary>Live ping + NIC byte counters (delta Mbps).</summary>
public sealed class LiveMonitorService : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly List<PingSample> _pings = new();
    private readonly Dictionary<string, (long sent, long recv, DateTimeOffset at)> _prev = new();
    private readonly object _lock = new();

    public IReadOnlyList<PingSample> RecentPings
    {
        get { lock (_lock) return _pings.TakeLast(40).ToList(); }
    }

    public event Action? Updated;

    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;

    public void Start(string[] hosts, int intervalMs = 2000)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var h in hosts.Where(x => !string.IsNullOrWhiteSpace(x)))
                    await SamplePingAsync(h.Trim(), token).ConfigureAwait(false);
                SampleTraffic();
                Updated?.Invoke();
                try { await Task.Delay(intervalMs, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    private async Task SamplePingAsync(string host, CancellationToken ct)
    {
        try
        {
            using var p = new Ping();
            var reply = await p.SendPingAsync(host, 1500).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            var sample = new PingSample
            {
                Host = host,
                Ok = reply.Status == IPStatus.Success,
                RttMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : null,
                Status = reply.Status.ToString()
            };
            lock (_lock)
            {
                _pings.Add(sample);
                if (_pings.Count > 200) _pings.RemoveRange(0, _pings.Count - 200);
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _pings.Add(new PingSample { Host = host, Ok = false, Status = ex.Message });
            }
        }
    }

    private void SampleTraffic()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            var stats = nic.GetIPv4Statistics();
            var now = DateTimeOffset.Now;
            var key = nic.Name;
            lock (_lock)
            {
                if (_prev.TryGetValue(key, out var prev))
                {
                    var dt = (now - prev.at).TotalSeconds;
                    if (dt > 0.2)
                    {
                        var sendBps = (stats.BytesSent - prev.sent) * 8.0 / dt;
                        var recvBps = (stats.BytesReceived - prev.recv) * 8.0 / dt;
                        // store last as message via event consumers reading Format()
                        _lastTraffic[key] = new TrafficSample
                        {
                            Adapter = key,
                            BytesSent = stats.BytesSent,
                            BytesRecv = stats.BytesReceived,
                            SendMbps = Math.Max(0, sendBps / 1_000_000),
                            RecvMbps = Math.Max(0, recvBps / 1_000_000)
                        };
                    }
                }
                _prev[key] = (stats.BytesSent, stats.BytesReceived, now);
            }
        }
    }

    private readonly Dictionary<string, TrafficSample> _lastTraffic = new();

    public string FormatSnapshot()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== LIVE PING ===");
        lock (_lock)
        {
            foreach (var p in _pings.TakeLast(12))
                sb.AppendLine($"{p.At:HH:mm:ss}  {p.Host,-18} {(p.Ok ? "OK" : "FAIL"),-4}  rtt={p.RttMs}  {p.Status}");
            sb.AppendLine();
            sb.AppendLine("=== NIC TRAFFIC (approx Mbps) ===");
            foreach (var t in _lastTraffic.Values.OrderBy(x => x.Adapter))
                sb.AppendLine($"{t.Adapter,-28}  ↓ {t.RecvMbps,7:F2}  ↑ {t.SendMbps,7:F2}  Mbps");
        }
        return sb.ToString();
    }

    public void Dispose() => Stop();
}
