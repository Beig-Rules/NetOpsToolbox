using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

public sealed class NetNoWanFlow : IDiagnosisFlow
{
    public string Id => "NET_NO_WAN";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var lanUp = facts.Adapters.Any(a => a.IsUp && a.IPv4Addresses.Count > 0);
        var gatewayOk = facts.Connectivity.GatewayReachable == true;
        var wanProbeFail = facts.Connectivity.PublicDnsReachable == false;
        var matched = lanUp && gatewayOk && wanProbeFail;

        var evidence = new List<string>();
        if (lanUp) evidence.Add("LAN adapter is up with IPv4.");
        if (gatewayOk) evidence.Add($"Gateway reachable (RTT {facts.Connectivity.GatewayRttMs} ms).");
        if (wanProbeFail) evidence.Add("Probe beyond LAN (public DNS IP) failed.");
        if (facts.DefaultGateways.Count > 0)
            evidence.Add("Gateways: " + string.Join(", ", facts.DefaultGateways));

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "check_cpe_wan",
                Title = "Inspect modem/router WAN status",
                Description = "LAN works; failure is upstream of the gateway. Open CPE UI or SSH.",
                Risk = RiskLevel.Low,
                Score = 95,
                ActionIds = ["AdviseCpeWan"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "firmware_diagnose",
                Title = "Run firmware diagnosis on gateway device",
                Description = "Compare CPE firmware to catalog if device is in inventory.",
                Risk = RiskLevel.Low,
                Score = 75,
                ActionIds = ["FirmwareDiagnose"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "renew_dhcp",
                Title = "Renew DHCP on client",
                Description = "Rarely fixes pure WAN outages but clears stale leases.",
                Risk = RiskLevel.Medium,
                Score = 40,
                ActionIds = ["RenewDhcp"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "LAN up, WAN down",
            Summary = matched
                ? "Local network works; path past the gateway does not."
                : "WAN-down pattern not matched.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
