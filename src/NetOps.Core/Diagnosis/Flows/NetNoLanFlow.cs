using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

public sealed class NetNoLanFlow : IDiagnosisFlow
{
    public string Id => "NET_NO_LAN";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var anyUp = facts.Adapters.Any(a => a.IsUp);
        var anyIp = facts.Adapters.Any(a => a.IPv4Addresses.Count > 0);
        var matched = !anyUp || !anyIp;

        var evidence = new List<string>();
        evidence.Add($"Adapters: {facts.Adapters.Count}, up={facts.Adapters.Count(a => a.IsUp)}");
        foreach (var a in facts.Adapters.Take(8))
            evidence.Add($"  · {a.Name}: {a.Status} ipv4={a.IPv4Addresses.Count}");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "check_cable_wifi",
                Title = "Check physical link / Wi-Fi association",
                Description = "No usable LAN IPv4. Verify cable, AP, airplane mode, disabled adapter.",
                Risk = RiskLevel.Low,
                Score = 95,
                ActionIds = ["AdvisePhysical"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "enable_adapter",
                Title = "Enable network adapter",
                Description = "If the NIC is disabled in Windows, enable it (requires elevation).",
                Risk = RiskLevel.Medium,
                Score = 80,
                ActionIds = ["AdviseEnableAdapter"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "No LAN connectivity",
            Summary = matched ? "No up adapter with IPv4." : "LAN appears present.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
