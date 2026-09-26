using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>Adapter up but no DNS servers assigned — names will fail even if IP works.</summary>
public sealed class NoDnsConfiguredFlow : IDiagnosisFlow
{
    public string Id => "NO_DNS_CONFIG";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var lanUp = facts.Adapters.Any(a => a.IsUp && a.IPv4Addresses.Count > 0);
        var hasDns = facts.DnsServers.Count > 0 || facts.Adapters.Any(a => a.DnsServers.Count > 0);
        var matched = lanUp && !hasDns;

        var evidence = new List<string>
        {
            $"LAN up with IPv4: {lanUp}",
            $"Host DNS list count: {facts.DnsServers.Count}"
        };
        foreach (var a in facts.Adapters.Where(a => a.IsUp).Take(4))
            evidence.Add($"{a.Name} DNS: [{string.Join(",", a.DnsServers)}]");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "set_public_dns",
                Title = "Set public DNS on active adapter",
                Description = "Apply Cloudflare/Google DNS via allow-listed SetDns action.",
                Risk = RiskLevel.Medium,
                Score = 92,
                ActionIds = ["SetAdapterDnsPublic"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "renew_dhcp",
                Title = "Renew DHCP (get DNS from router)",
                Description = "If DHCP should provide DNS, renew lease after fixing the CPE.",
                Risk = RiskLevel.Low,
                Score = 78,
                ActionIds = ["RenewDhcp"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "check_static_ip",
                Title = "Review static IP configuration",
                Description = "Static addressing without DNS entries is a common misconfig.",
                Risk = RiskLevel.Low,
                Score = 72,
                ActionIds = ["AdviseStaticIpDns"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "No DNS servers configured",
            Summary = matched
                ? "Interface is up but no DNS servers are configured."
                : "DNS servers present or LAN not up.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
