using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>No operational NIC with IPv4 — LAN/WAN both unreachable from host view.</summary>
public sealed class AdapterAllDownFlow : IDiagnosisFlow
{
    public string Id => "ADAPTER_ALL_DOWN";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var any = facts.Adapters.Count > 0;
        var upWithIp = facts.Adapters.Any(a => a.IsUp && a.IPv4Addresses.Count > 0);
        var anyUp = facts.Adapters.Any(a => a.IsUp);
        var matched = any && !upWithIp;

        var evidence = new List<string>
        {
            $"Adapters seen: {facts.Adapters.Count}",
            $"Any operational: {anyUp}",
            $"Up with IPv4: {upWithIp}"
        };
        foreach (var a in facts.Adapters.Take(6))
            evidence.Add($"{a.Name}: {a.Status} ip=[{string.Join(",", a.IPv4Addresses)}]");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "enable_adapter",
                Title = "Enable / reconnect network adapter",
                Description = "Check cable, Wi-Fi radio, airplane mode, and that the NIC is enabled in Windows.",
                Risk = RiskLevel.Low,
                Score = 95,
                ActionIds = ["AdviseEnableAdapter"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "renew_dhcp",
                Title = "Renew DHCP lease",
                Description = "Forces DHCP renew on adapters (safe, allow-listed).",
                Risk = RiskLevel.Low,
                Score = 80,
                ActionIds = ["RenewDhcp"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "check_driver",
                Title = "Check NIC driver / Device Manager",
                Description = "Yellow bang or disabled device often means missing driver after Windows update.",
                Risk = RiskLevel.Low,
                Score = 70,
                ActionIds = ["AdviseNicDriver"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "No active adapter with IPv4",
            Summary = matched
                ? "Host has adapters but none are up with an IPv4 address."
                : "At least one adapter is up with IPv4 (or no adapters).",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
