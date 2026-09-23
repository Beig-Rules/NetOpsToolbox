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
        string? proxyServer = null)
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
                GatewayRttMs = 2,
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
    public void Subnet_calculate_basic()
    {
        var text = NetOps.Core.Tools.NetworkTools.SubnetCalculate("192.168.1.10/24");
        Assert.Contains("192.168.1", text);
    }
}
