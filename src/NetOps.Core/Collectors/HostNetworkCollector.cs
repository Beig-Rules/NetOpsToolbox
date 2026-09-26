using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetOps.Core.Facts;

namespace NetOps.Core.Collectors;

/// <summary>
/// Collect host network facts (adapters, gateways, DNS, basic connectivity probes).
/// </summary>
public sealed class HostNetworkCollector
{
    public async Task<HostFacts> CollectAsync(CancellationToken ct = default)
    {
        var adapters = new List<AdapterFact>();
        var gateways = new List<string>();
        var dns = new List<string>();
        var notes = new List<string>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var props = nic.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .ToList();

            // DnsAddresses is IPAddressCollection — each item is IPAddress (not UnicastIPAddressInformation)
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
                DnsServers = dnsList,
                InterfaceMetric = props.GetIPv4Properties()?.Index
            });
        }

        gateways = gateways.Distinct().ToList();
        dns = dns.Distinct().ToList();

        bool? gwOk = null;
        long? gwRtt = null;
        if (gateways.Count > 0)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(gateways[0], 2500).ConfigureAwait(false);
                gwOk = reply.Status == IPStatus.Success;
                if (gwOk == true) gwRtt = reply.RoundtripTime;
            }
            catch (Exception ex)
            {
                notes.Add("Gateway ping: " + ex.Message);
                gwOk = false;
            }
        }

        bool? publicDns = null;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("1.1.1.1", 2500).ConfigureAwait(false);
            publicDns = reply.Status == IPStatus.Success;
        }
        catch { publicDns = false; }

        bool? nameOk = null;
        string? resolved = null;
        try
        {
            var addrs = await Dns.GetHostAddressesAsync("dns.google", ct).ConfigureAwait(false);
            nameOk = addrs.Length > 0;
            resolved = string.Join(",", addrs.Select(a => a.ToString()).Take(3));
        }
        catch
        {
            nameOk = false;
        }

        bool proxyEnabled = false;
        string? proxyServer = null;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
                var enable = key?.GetValue("ProxyEnable");
                proxyEnabled = enable is int i && i == 1;
                proxyServer = key?.GetValue("ProxyServer") as string;
            }
        }
        catch (Exception ex)
        {
            notes.Add("Proxy read: " + ex.Message);
        }

        RegistryHostFacts? reg = null;
        try
        {
            reg = new RegistryNetworkCollector().Collect();
        }
        catch (Exception ex)
        {
            notes.Add("Registry: " + ex.Message);
        }

        return new HostFacts
        {
            Adapters = adapters,
            DefaultGateways = gateways,
            DnsServers = dns,
            ProxyEnabled = proxyEnabled,
            ProxyServer = proxyServer,
            Connectivity = new ConnectivityFact
            {
                GatewayReachable = gwOk,
                GatewayRttMs = gwRtt,
                PublicDnsReachable = publicDns,
                NameResolutionWorks = nameOk,
                ResolvedProbe = resolved
            },
            Registry = reg,
            CollectorNotes = notes
        };
    }
}
