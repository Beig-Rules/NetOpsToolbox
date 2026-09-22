using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

public sealed class DnsFailFlow : IDiagnosisFlow
{
    public string Id => "DNS_FAIL";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var c = facts.Connectivity;
        var lanUp = facts.Adapters.Any(a => a.IsUp && a.IPv4Addresses.Count > 0);
        var gatewayOk = c.GatewayReachable == true;
        var dnsResolveFail = c.NameResolutionWorks == false;
        var hasDnsServers = facts.DnsServers.Count > 0 || facts.Adapters.Any(a => a.DnsServers.Count > 0);

        // IP path works-ish but names do not
        var matched = lanUp && dnsResolveFail && (gatewayOk || c.PublicDnsReachable == true);

        var evidence = new List<string>();
        if (lanUp) evidence.Add("At least one adapter has IPv4 and is up.");
        if (gatewayOk) evidence.Add("Default gateway responded to probe.");
        if (dnsResolveFail) evidence.Add("Name resolution probe failed.");
        if (!hasDnsServers) evidence.Add("No DNS servers configured on adapters.");
        if (facts.ProxyEnabled) evidence.Add($"System proxy enabled: {facts.ProxyServer ?? "(unspecified)"}.");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "flush_dns",
                Title = "Flush DNS client cache",
                Description = "Clears the Windows DNS resolver cache (ipconfig /flushdns).",
                Risk = RiskLevel.Low,
                Score = 90,
                ActionIds = ["FlushDns"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "set_public_dns",
                Title = "Set adapter DNS to public resolvers",
                Description = "Applies well-known public DNS on the active adapter (preview first).",
                Risk = RiskLevel.Medium,
                Score = 70,
                ActionIds = ["SetAdapterDnsPublic"]
            });
            if (facts.ProxyEnabled)
            {
                solutions.Add(new SolutionOffer
                {
                    Id = "review_proxy",
                    Title = "Review system proxy",
                    Description = "Proxy may intercept name resolution; inspect WinHTTP/user proxy settings.",
                    Risk = RiskLevel.Medium,
                    Score = 65,
                    ActionIds = ["ReportProxyOnly"]
                });
            }
            solutions.Add(new SolutionOffer
            {
                Id = "check_router_dns",
                Title = "Check DNS on gateway/router",
                Description = "If client DNS is DHCP-provided, fix DNS on the CPE instead of the PC.",
                Risk = RiskLevel.Low,
                Score = 60,
                ActionIds = ["AdviseRouterDns"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "DNS resolution failure",
            Summary = matched
                ? "Network path appears up but host name resolution is failing."
                : "DNS failure pattern not matched.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
