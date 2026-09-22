using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

public sealed class RouteBrokenFlow : IDiagnosisFlow
{
    public string Id => "ROUTE_BROKEN";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var defaults = facts.Routes.Where(r =>
            r.Destination is "0.0.0.0/0" or "0.0.0.0" or "default").ToList();

        var multipleDefaults = defaults.Count > 1;
        var noDefault = defaults.Count == 0 && facts.DefaultGateways.Count == 0;
        var metricConflict = defaults.Select(d => d.Metric).Distinct().Count() > 1 && multipleDefaults;

        var matched = multipleDefaults || noDefault || metricConflict;

        var evidence = new List<string>();
        evidence.Add($"Default routes observed: {defaults.Count}");
        foreach (var d in defaults.Take(5))
            evidence.Add($"  → {d.Destination} via {d.Gateway} if={d.Interface} metric={d.Metric}");
        if (noDefault) evidence.Add("No default route / gateway detected.");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            if (multipleDefaults || metricConflict)
            {
                solutions.Add(new SolutionOffer
                {
                    Id = "fix_metrics",
                    Title = "Review interface metrics",
                    Description = "Multiple default routes often come from VPN + LAN. Prefer the correct interface metric.",
                    Risk = RiskLevel.Medium,
                    Score = 85,
                    ActionIds = ["AdviseInterfaceMetric"]
                });
            }
            if (noDefault)
            {
                solutions.Add(new SolutionOffer
                {
                    Id = "restore_gateway",
                    Title = "Restore default gateway",
                    Description = "Renew DHCP or set gateway on the primary adapter.",
                    Risk = RiskLevel.Medium,
                    Score = 90,
                    ActionIds = ["RenewDhcp", "AdviseGateway"]
                });
            }
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = "Routing anomaly",
            Summary = matched ? "Default route configuration looks inconsistent." : "Routing looks normal.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
