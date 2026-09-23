using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>Matches when system proxy is enabled — advisory guidance only.</summary>
public sealed class ProxyOnFlow : IDiagnosisFlow
{
    public string Id => "PROXY_ON";

    public DiagnosisResult Evaluate(HostFacts facts)
    {
        var matched = facts.ProxyEnabled;
        var evidence = new List<string>();
        if (facts.ProxyEnabled)
            evidence.Add("System proxy enabled: " + (facts.ProxyServer ?? "(server unspecified)"));
        else
            evidence.Add("System proxy not flagged by collector.");

        var solutions = new List<SolutionOffer>();
        if (matched)
        {
            solutions.Add(new SolutionOffer
            {
                Id = "review_proxy",
                Title = "Review proxy / PAC settings",
                Description = "Confirm proxy is required. Misconfigured proxy causes DNS and HTTPS failures. Use Tools → Proxy for details.",
                Risk = RiskLevel.Medium,
                Score = 75,
                ActionIds = [],
                RequiresConfirm = false
            });
            solutions.Add(new SolutionOffer
            {
                Id = "test_direct",
                Title = "Test connectivity without proxy path",
                Description = "Temporarily verify direct DNS (1.1.1.1) and HTTPS to isolate proxy interception.",
                Risk = RiskLevel.Low,
                Score = 60,
                ActionIds = [],
                RequiresConfirm = false
            });
        }

        return new DiagnosisResult
        {
            FlowId = Id,
            Title = matched ? "System proxy is enabled" : "Proxy not enabled",
            Summary = matched
                ? "Proxy may alter DNS/HTTP paths. Review before changing adapter DNS."
                : "No proxy flag from host facts.",
            Matched = matched,
            Evidence = evidence,
            Solutions = solutions
        };
    }
}
