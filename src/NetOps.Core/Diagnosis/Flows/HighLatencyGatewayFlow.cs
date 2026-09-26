using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>Gateway answers but RTT is abnormally high — congestion or wireless issues.</summary>
public sealed class HighLatencyGatewayFlow : IDiagnosisFlow
{
    public const long ThresholdMs = 80;

    public string Id => "HIGH_LATENCY_GW";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var c = facts.Connectivity;
        var matched = c.GatewayReachable == true
                      && c.GatewayRttMs is long rtt
                      && rtt >= ThresholdMs;

        var evidence = new List<string>();
        if (c.GatewayReachable == true)
            evidence.Add("Gateway probe succeeded.");
        if (c.GatewayRttMs is long ms)
            evidence.Add($"Gateway RTT: {ms} ms (threshold {ThresholdMs} ms).");
        if (facts.DefaultGateways.Count > 0)
            evidence.Add("Gateways: " + string.Join(", ", facts.DefaultGateways));

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "check_wifi_signal",
                Title = "Check Wi-Fi signal / interference",
                Description = "High RTT on wireless often means weak signal, DFS, or channel congestion.",
                Risk = RiskLevel.Low,
                Score = 85,
                ActionIds = ["AdviseWifiSignal"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "reboot_cpe",
                Title = "Power-cycle CPE / access point",
                Description = "Router CPU or bufferbloat can inflate LAN latency without full outage.",
                Risk = RiskLevel.Low,
                Score = 75,
                ActionIds = ["AdviseRebootCpe"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "wired_test",
                Title = "Test with wired Ethernet",
                Description = "Isolate wireless vs CPE by using a cable temporarily.",
                Risk = RiskLevel.Low,
                Score = 70,
                ActionIds = ["AdviseWiredTest"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "High gateway latency",
            Summary = matched
                ? $"Gateway is reachable but RTT is {c.GatewayRttMs} ms (≥ {ThresholdMs})."
                : "Gateway latency within normal bounds or not measured.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
