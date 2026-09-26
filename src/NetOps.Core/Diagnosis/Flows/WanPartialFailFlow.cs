using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>
/// Gateway OK but public probe fails — often ISP/WAN, firewall, or captive beyond LAN.
/// Distinct from full NET_NO_WAN (no gateway).
/// </summary>
public sealed class WanPartialFailFlow : IDiagnosisFlow
{
    public string Id => "WAN_PARTIAL_FAIL";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var c = facts.Connectivity;
        var matched = c.GatewayReachable == true && c.PublicDnsReachable == false;

        var evidence = new List<string>();
        if (c.GatewayReachable == true)
            evidence.Add("Default gateway responded.");
        if (c.PublicDnsReachable == false)
            evidence.Add("Public probe (1.1.1.1) failed.");
        if (c.NameResolutionWorks == false)
            evidence.Add("Name resolution also failed.");
        if (c.NameResolutionWorks == true)
            evidence.Add("Name resolution still works (partial path)." + (c.ResolvedProbe is not null ? " " + c.ResolvedProbe : ""));

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "check_isp",
                Title = "Check ISP / upstream link",
                Description = "LAN to CPE works; outage may be on the provider side.",
                Risk = RiskLevel.Low,
                Score = 88,
                ActionIds = ["AdviseIspStatus"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "check_firewall",
                Title = "Review host firewall outbound",
                Description = "Local firewall or security suite may block ICMP/HTTP probes.",
                Risk = RiskLevel.Medium,
                Score = 72,
                ActionIds = ["ReportFirewallProfiles"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "trace_path",
                Title = "Run traceroute toward public IP",
                Description = "Identify where packets stop (Tools → Traceroute).",
                Risk = RiskLevel.Low,
                Score = 80,
                ActionIds = ["AdviseTraceroute"]
            });
            if (facts.ProxyEnabled)
            {
                solutions.Add(new SolutionOffer
                {
                    Id = "review_proxy",
                    Title = "Review proxy / PAC",
                    Description = "Proxy may force all traffic through a dead upstream.",
                    Risk = RiskLevel.Medium,
                    Score = 68,
                    ActionIds = ["ReportProxyOnly"]
                });
            }
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "WAN partial failure",
            Summary = matched
                ? "Gateway is reachable but public internet probe failed."
                : "WAN partial pattern not matched.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
