using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NetOps.Core.Facts;

namespace NetOps.Core.Collectors;

public sealed class HostNetworkCollector
{
    public async Task<HostFacts> CollectAsync(CancellationToken ct = default)
    {
        var notes = new List<string>();
        var adapters = new List<AdapterFact>();
        var gateways = new List<string>();
        var dns = new List<string>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var props = nic.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .ToList();

            var dnsList = props.DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList();

            foreach (var g in props.GatewayAddresses)
            {
                if (g.Address.AddressFamily == AddressFamily.InterNetwork)
                    gateways.Add(g.Address.ToString());
            }

            dns.AddRange(dnsList);

            adapters.Add(new AdapterFact
            {
                Name = nic.Name,
                Description = nic.Description,
                Status = nic.OperationalStatus.ToString(),
                IsUp = nic.OperationalStatus == OperationalStatus.Up,
                IPv4Addresses = ipv4,
                DnsServers = dnsList
            });
        }

        gateways = gateways.Distinct().ToList();
        dns = dns.Distinct().ToList();

        var connectivity = await ProbeConnectivityAsync(gateways, ct).ConfigureAwait(false);

        bool proxyEnabled = false;
        string? proxyServer = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var reg = RegistryNetworkCollector.ReadProxy();
                proxyEnabled = reg.Enabled;
                proxyServer = reg.Server;
            }
            catch (Exception ex)
            {
                notes.Add("Registry proxy read skipped: " + ex.Message);
            }
        }
        else
        {
            notes.Add("Registry collector skipped (non-Windows).");
        }

        var routes = new List<RouteFact>();
        foreach (var g in gateways)
        {
            routes.Add(new RouteFact
            {
                Destination = "0.0.0.0/0",
                Gateway = g,
                Interface = "",
                Metric = 0
            });
        }

        return new HostFacts
        {
            Adapters = adapters,
            DefaultGateways = gateways,
            DnsServers = dns,
            ProxyEnabled = proxyEnabled,
            ProxyServer = proxyServer,
            Routes = routes,
            Connectivity = connectivity,
            CollectorNotes = notes
        };
    }

    private static async Task<ConnectivityFact> ProbeConnectivityAsync(List<string> gateways, CancellationToken ct)
    {
        bool? gwOk = null;
        long? gwRtt = null;
        if (gateways.Count > 0)
        {
            var (ok, rtt) = await PingOnceAsync(gateways[0], 2000, ct).ConfigureAwait(false);
            gwOk = ok;
            gwRtt = rtt;
        }

        var (pubOk, _) = await PingOnceAsync("1.1.1.1", 2500, ct).ConfigureAwait(false);

        bool? nameOk = null;
        string? resolved = null;
        try
        {
            var entry = await Dns.GetHostEntryAsync("dns.google", ct).ConfigureAwait(false);
            nameOk = entry.AddressList.Length > 0;
            resolved = string.Join(", ", entry.AddressList.Select(a => a.ToString()).Take(3));
        }
        catch
        {
            nameOk = false;
        }

        return new ConnectivityFact
        {
            GatewayReachable = gwOk,
            GatewayRttMs = gwRtt,
            PublicDnsReachable = pubOk,
            NameResolutionWorks = nameOk,
            ResolvedProbe = resolved
        };
    }

    private static async Task<(bool ok, long? rtt)> PingOnceAsync(string host, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var p = new Ping();
            var reply = await p.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (reply.Status == IPStatus.Success)
                return (true, reply.RoundtripTime);
            return (false, null);
        }
        catch
        {
            return (false, null);
        }
    }
}
