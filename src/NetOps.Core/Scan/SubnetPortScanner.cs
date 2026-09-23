using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NetOps.Core.Scan;

public sealed class HostPortHit
{
    public string Ip { get; init; } = "";
    public int Port { get; init; }
    public bool Open { get; init; }
    public int? LatencyMs { get; init; }
}

/// <summary>
/// Concurrent TCP connect scan limited to small LAN ranges.
/// Safety: max /24 equivalent (256 hosts), default common ports only.
/// </summary>
public sealed class SubnetPortScanner
{
    public static readonly int[] CommonPorts =
        [22, 23, 53, 80, 443, 445, 3389, 8080, 8443, 8728, 8729];

    public async Task<string> ScanAsync(
        string cidrOrPrefix,
        int[]? ports = null,
        int timeoutMs = 400,
        int maxHosts = 256,
        int concurrency = 64,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ports ??= CommonPorts;
        var hosts = ExpandHosts(cidrOrPrefix, maxHosts);
        if (hosts.Count == 0)
            return "No hosts to scan. Use e.g. 192.168.1.0/24 or 192.168.1.1-50";

        var sb = new StringBuilder();
        sb.AppendLine($"Targets: {hosts.Count}  Ports: {string.Join(",", ports)}  timeout={timeoutMs}ms");
        sb.AppendLine(new string('-', 48));

        var open = new List<HostPortHit>();
        using var gate = new SemaphoreSlim(concurrency);
        var tasks = new List<Task>();

        foreach (var ip in hosts)
        {
            foreach (var port in ports)
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var hit = await ProbeAsync(ip, port, timeoutMs, ct).ConfigureAwait(false);
                        if (hit.Open)
                        {
                            lock (open) open.Add(hit);
                            progress?.Report($"OPEN {ip}:{port} ({hit.LatencyMs}ms)");
                        }
                    }
                    finally { gate.Release(); }
                }, ct));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var h in open.OrderBy(x => x.Ip).ThenBy(x => x.Port))
            sb.AppendLine($"  {h.Ip,-15} :{h.Port,-5}  open  {h.LatencyMs}ms");

        if (open.Count == 0)
            sb.AppendLine("  (no open ports in scope)");

        sb.AppendLine();
        sb.AppendLine($"Done. Open hits: {open.Count}");
        return sb.ToString();
    }

    /// <summary>Ping sweep first (optional helper for live hosts only).</summary>
    public async Task<List<string>> AliveHostsAsync(
        string cidrOrPrefix, int maxHosts = 256, int timeoutMs = 600, CancellationToken ct = default)
    {
        var hosts = ExpandHosts(cidrOrPrefix, maxHosts);
        var alive = new List<string>();
        using var gate = new SemaphoreSlim(64);
        var tasks = hosts.Select(async ip =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using var p = new Ping();
                var r = await p.SendPingAsync(ip, timeoutMs).ConfigureAwait(false);
                if (r.Status == IPStatus.Success)
                {
                    lock (alive) alive.Add(ip);
                }
            }
            catch { /* ignore */ }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return alive.OrderBy(x => x).ToList();
    }

    private static async Task<HostPortHit> ProbeAsync(string ip, int port, int timeoutMs, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(IPAddress.Parse(ip), port);
            var done = await Task.WhenAny(connect, Task.Delay(timeoutMs, ct)).ConfigureAwait(false);
            if (done != connect || !client.Connected)
                return new HostPortHit { Ip = ip, Port = port, Open = false };
            sw.Stop();
            return new HostPortHit { Ip = ip, Port = port, Open = true, LatencyMs = (int)sw.ElapsedMilliseconds };
        }
        catch
        {
            return new HostPortHit { Ip = ip, Port = port, Open = false };
        }
    }

    public static List<string> ExpandHosts(string input, int maxHosts)
    {
        input = input.Trim();
        var list = new List<string>();

        // 192.168.1.1-50
        if (input.Contains('-') && !input.Contains('/'))
        {
            var parts = input.Split('-', 2);
            if (IPAddress.TryParse(parts[0].Trim(), out var start))
            {
                var bytes = start.GetAddressBytes();
                if (int.TryParse(parts[1].Trim(), out var endHost) && bytes.Length == 4)
                {
                    var startHost = bytes[3];
                    for (int h = startHost; h <= endHost && list.Count < maxHosts; h++)
                    {
                        list.Add($"{bytes[0]}.{bytes[1]}.{bytes[2]}.{h}");
                    }
                    return list;
                }
            }
        }

        // 192.168.1.0/24
        if (input.Contains('/'))
        {
            var parts = input.Split('/', 2);
            if (IPAddress.TryParse(parts[0], out var net) && int.TryParse(parts[1], out var prefix))
            {
                if (prefix < 24) prefix = 24; // safety floor for this tool
                if (prefix > 32) prefix = 32;
                var ipBytes = net.GetAddressBytes();
                if (ipBytes.Length != 4) return list;
                uint ip = ((uint)ipBytes[0] << 24) | ((uint)ipBytes[1] << 16) | ((uint)ipBytes[2] << 8) | ipBytes[3];
                int hostBits = 32 - prefix;
                uint size = hostBits >= 32 ? 0xFFFFFFFFu : (1u << hostBits);
                uint mask = size == 0 ? 0 : ~(size - 1);
                uint baseIp = ip & mask;
                // skip network and broadcast for /24 and larger host counts
                uint start = size > 2 ? 1u : 0u;
                uint end = size > 2 ? size - 1 : size;
                for (uint i = start; i < end && list.Count < maxHosts; i++)
                {
                    uint a = baseIp + i;
                    list.Add($"{(a >> 24) & 0xFF}.{(a >> 16) & 0xFF}.{(a >> 8) & 0xFF}.{a & 0xFF}");
                }
                return list;
            }
        }

        // single host
        if (IPAddress.TryParse(input, out _))
            list.Add(input);

        return list;
    }
}
