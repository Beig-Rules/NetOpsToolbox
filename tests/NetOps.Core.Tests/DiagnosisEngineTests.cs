using NetOps.Core.Diagnosis;
using NetOps.Core.Facts;
using Xunit;

namespace NetOps.Core.Tests;

public class DiagnosisEngineTests
{
    private static HostFacts BaseLan(
        bool namesOk = true,
        bool publicOk = true,
        bool proxy = false,
        string? proxyServer = null,
        long? gwRtt = 2)
    {
        return new HostFacts
        {
            Adapters =
            [
                new AdapterFact
                {
                    Name = "Ethernet",
                    IsUp = true,
                    Status = "Up",
                    IPv4Addresses = ["192.168.1.10"],
                    DnsServers = ["192.168.1.1"]
                }
            ],
            DefaultGateways = ["192.168.1.1"],
            DnsServers = ["192.168.1.1"],
            ProxyEnabled = proxy,
            ProxyServer = proxyServer,
            Connectivity = new ConnectivityFact
            {
                GatewayReachable = true,
                GatewayRttMs = gwRtt,
                PublicDnsReachable = publicOk,
                NameResolutionWorks = namesOk
            }
        };
    }

    [Fact]
    public void Healthy_host_matches_no_incident()
    {
        var report = new DiagnosisEngine().Evaluate(BaseLan());
        Assert.Empty(report.Results);
        Assert.Contains("No active", report.Headline);
    }

    [Fact]
    public void Dns_fail_matches_and_offers_flush()
    {
        var report = new DiagnosisEngine().Evaluate(BaseLan(namesOk: false));
        Assert.Contains(report.Results, r => r.FlowId == "DNS_FAIL");
        Assert.Contains(report.RankedSolutions, s => s.Id == "flush_dns");
    }

    [Fact]
    public void Proxy_on_matches()
    {
        var report = new DiagnosisEngine().Evaluate(BaseLan(proxy: true, proxyServer: "http://proxy:8080"));
        Assert.Contains(report.Results, r => r.FlowId == "PROXY_ON");
    }

    [Fact]
    public void Captive_portal_heuristic_matches()
    {
        var report = new DiagnosisEngine().Evaluate(BaseLan(namesOk: false, publicOk: false));
        Assert.Contains(report.Results, r => r.FlowId == "CAPTIVE_PORTAL");
        Assert.Contains(report.RankedSolutions, s => s.Id == "open_portal");
    }

    [Fact]
    public void Engine_registers_ten_flows()
    {
        var ids = new DiagnosisEngine().FlowIds;
        Assert.Equal(10, ids.Count);
        Assert.Contains("ADAPTER_ALL_DOWN", ids);
        Assert.Contains("HIGH_LATENCY_GW", ids);
        Assert.Contains("NO_DNS_CONFIG", ids);
        Assert.Contains("WAN_PARTIAL_FAIL", ids);
    }

    [Fact]
    public void Adapter_all_down_matches()
    {
        var facts = new HostFacts
        {
            Adapters =
            [
                new AdapterFact { Name = "Wi-Fi", IsUp = false, Status = "Down", IPv4Addresses = [] }
            ],
            Connectivity = new ConnectivityFact()
        };
        var report = new DiagnosisEngine().Evaluate(facts);
        Assert.Contains(report.Results, r => r.FlowId == "ADAPTER_ALL_DOWN");
        Assert.Contains(report.RankedSolutions, s => s.Id == "enable_adapter");
    }

    [Fact]
    public void High_latency_gateway_matches()
    {
        var report = new DiagnosisEngine().Evaluate(BaseLan(gwRtt: 120));
        Assert.Contains(report.Results, r => r.FlowId == "HIGH_LATENCY_GW");
        Assert.Contains(report.RankedSolutions, s => s.Id == "check_wifi_signal");
    }

    [Fact]
    public void No_dns_config_matches()
    {
        var facts = BaseLan();
        facts = new HostFacts
        {
            Adapters =
            [
                new AdapterFact
                {
                    Name = "Ethernet",
                    IsUp = true,
                    Status = "Up",
                    IPv4Addresses = ["192.168.1.10"],
                    DnsServers = []
                }
            ],
            DefaultGateways = facts.DefaultGateways,
            DnsServers = [],
            Connectivity = facts.Connectivity
        };
        var report = new DiagnosisEngine().Evaluate(facts);
        Assert.Contains(report.Results, r => r.FlowId == "NO_DNS_CONFIG");
        Assert.Contains(report.RankedSolutions, s => s.Id == "set_public_dns");
    }

    [Fact]
    public void Wan_partial_fail_matches()
    {
        var report = new DiagnosisEngine().Evaluate(BaseLan(publicOk: false));
        Assert.Contains(report.Results, r => r.FlowId == "WAN_PARTIAL_FAIL");
        Assert.Contains(report.RankedSolutions, s => s.Id == "check_isp");
    }

    [Fact]
    public void Subnet_calculate_basic()
    {
        var text = NetOps.Core.Tools.NetworkTools.SubnetCalculate("192.168.1.10/24");
        Assert.Contains("192.168.1", text);
    }
}
