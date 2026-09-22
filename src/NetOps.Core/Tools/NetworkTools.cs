using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace NetOps.Core.Tools;

public sealed class PingTargetResult
{
    public string Host { get; init; } = "";
    public bool Success { get; init; }
    public long? RttMs { get; init; }
    public string Status { get; init; } = "";
}

public sealed class NetworkTools
{
    public async Task<IReadOnlyList<PingTargetResult>> PingManyAsync(
        IEnumerable<string> hosts, int timeoutMs = 2000, int count = 4, CancellationToken ct = default)
    {
        var results = new List<PingTargetResult>();
        foreach (var host in hosts.Select(h => h.Trim()).Where(h => h.Length > 0))
        {
            ct.ThrowIfCancellationRequested();
            long total = 0;
            int ok = 0;
            string lastStatus = "";
            for (var i = 0; i < count; i++)
            {
                try
                {
                    using var p = new Ping();
                    var reply = await p.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
                    lastStatus = reply.Status.ToString();
                    if (reply.Status == IPStatus.Success)
                    {
                        ok++;
                        total += reply.RoundtripTime;
                    }
                }
                catch (Exception ex)
                {
                    lastStatus = ex.Message;
                }
            }

            results.Add(new PingTargetResult
            {
                Host = host,
                Success = ok > 0,
                RttMs = ok > 0 ? total / ok : null,
                Status = ok > 0 ? $"{ok}/{count} ok avg {total / ok}ms" : lastStatus
            });
        }
        return results;
    }

    public async Task<string> DnsLookupAsync(string name, CancellationToken ct = default)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(name.Trim(), ct).ConfigureAwait(false);
            var sb = new StringBuilder();
            sb.AppendLine("Host: " + entry.HostName);
            foreach (var a in entry.AddressList)
                sb.AppendLine("  " + a.AddressFamily + " " + a);
            foreach (var alias in entry.Aliases)
                sb.AppendLine("  alias " + alias);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "DNS lookup failed: " + ex.Message;
        }
    }

    public async Task<string> PortCheckAsync(string host, int port, int timeoutMs = 3000, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host.Trim(), port);
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs, ct)).ConfigureAwait(false);
            if (completed != task)
                return $"{host}:{port} TIMEOUT ({timeoutMs}ms)";
            await task.ConfigureAwait(false);
            return client.Connected ? $"{host}:{port} OPEN" : $"{host}:{port} CLOSED";
        }
        catch (Exception ex)
        {
            return $"{host}:{port} FAIL — {ex.Message}";
        }
    }

    public async Task<string> TracerouteAsync(string host, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Traceroute uses tracert and is Windows-only in this build.";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tracert",
                Arguments = "-d -h 15 -w 1000 " + host.Trim(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            return (stdout + "\n" + stderr).Trim();
        }
        catch (Exception ex)
        {
            return "tracert failed: " + ex.Message;
        }
    }

    public static string SubnetCalculate(string cidrOrIpMask)
    {
        try
        {
            // Accept a.b.c.d/nn or a.b.c.d mask
            var input = cidrOrIpMask.Trim();
            IPAddress ip;
            int prefix;

            if (input.Contains('/'))
            {
                var parts = input.Split('/', 2);
                ip = IPAddress.Parse(parts[0]);
                prefix = int.Parse(parts[1]);
            }
            else
            {
                var parts = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return "Usage: 192.168.1.0/24  or  192.168.1.10 255.255.255.0";
                ip = IPAddress.Parse(parts[0]);
                var maskBytes = IPAddress.Parse(parts[1]).GetAddressBytes();
                prefix = maskBytes.Select(b => Convert.ToString(b, 2).Count(c => c == '1')).Sum();
            }

            if (ip.AddressFamily != AddressFamily.InterNetwork || prefix is < 0 or > 32)
                return "IPv4 only; prefix 0-32.";

            var ipBytes = ip.GetAddressBytes();
            var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
            var ipVal = (uint)(ipBytes[0] << 24 | ipBytes[1] << 16 | ipBytes[2] << 8 | ipBytes[3]);
            var network = ipVal & mask;
            var broadcast = network | ~mask;
            var hosts = prefix >= 31 ? 0 : (int)Math.Pow(2, 32 - prefix) - 2;

            string Fmt(uint v) =>
                $"{(v >> 24) & 0xff}.{(v >> 16) & 0xff}.{(v >> 8) & 0xff}.{v & 0xff}";

            var sb = new StringBuilder();
            sb.AppendLine($"Input: {input}");
            sb.AppendLine($"Network:   {Fmt(network)}/{prefix}");
            sb.AppendLine($"Mask:      {Fmt(mask)}");
            sb.AppendLine($"Broadcast: {Fmt(broadcast)}");
            if (hosts > 0)
            {
                sb.AppendLine($"First host: {Fmt(network + 1)}");
                sb.AppendLine($"Last host:  {Fmt(broadcast - 1)}");
            }
            sb.AppendLine($"Usable hosts: {hosts}");
            sb.AppendLine($"Wildcard:  {Fmt(~mask)}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "Subnet calc error: " + ex.Message;
        }
    }
}
