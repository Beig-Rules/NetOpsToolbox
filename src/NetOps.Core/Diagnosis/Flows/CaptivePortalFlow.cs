using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>
/// Heuristic: LAN up, gateway OK, public IP path weak or name resolution fails
/// — often captive portal / hotel Wi-Fi / ISP redirect.
/// </summary>
public sealed class CaptivePortalFlow : IDiagnosisFlow
{
    public string Id => "CAPTIVE_PORTAL";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var c = facts.Connectivity;
        var lanUp = facts.Adapters.Any(a => a.IsUp && a.IPv4Addresses.Count > 0);
        var gatewayOk = c.GatewayReachable == true;
        var namesFail = c.NameResolutionWorks == false;
        var publicFail = c.PublicDnsReachable == false;

        // Classic captive pattern: local OK, internet identity broken
        var matched = lanUp && gatewayOk && (namesFail || publicFail);

        var evidence = new List<string>();
        if (lanUp) evidence.Add("Local adapter has IPv4 and is up.");
        if (gatewayOk) evidence.Add($"Gateway reachable (RTT={c.GatewayRttMs}ms).");
        if (namesFail) evidence.Add("Name resolution probe failed.");
        if (publicFail) evidence.Add("Public DNS/IP probe failed.");
        if (facts.ProxyEnabled) evidence.Add("Proxy also enabled — may compound portal issues.");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "open_portal",
                Title = "Complete captive portal login",
                Description = "Open a browser to http://neverssl.com or the gateway portal and accept terms / sign in.",
                Risk = RiskLevel.Low,
                Score = 95,
                ActionIds = [],
                RequiresConfirm = false
            });
            solutions.Add(new SolutionOffer
            {
                Id = "flush_after_portal",
                Title = "Flush DNS after portal login",
                Description = "After authenticating, flush DNS cache so names resolve correctly.",
                Risk = RiskLevel.Low,
                Score = 80,
                ActionIds = ["FlushDns"]
            });
            solutions.Add(new SolutionOffer
            {
                Id = "renew_dhcp_portal",
                Title = "Renew DHCP lease",
                Description = "Some portals require a fresh lease after login.",
                Risk = RiskLevel.Low,
                Score = 55,
                ActionIds = ["RenewDhcp"]
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = matched ? "Possible captive portal / restricted WAN" : "No captive-portal pattern",
            Summary = matched
                ? "Local network works but internet identity failed — check portal or ISP redirect."
                : "Connectivity pattern does not match captive portal heuristic.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
