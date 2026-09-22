using NetOps.Core.Facts;

namespace NetOps.Core.Diagnosis;

/// <summary>
/// Pure decision engine: HostFacts in → ranked solutions out.
/// No side effects; safe to unit-test on any OS.
/// </summary>
public sealed class DiagnosisEngine
{
    private readonly List<IDiagnosisFlow> _flows;

    public DiagnosisEngine()
    {
        _flows =
        [
            new DnsFailFlow(),
            new NetNoWanFlow(),
            new RouteBrokenFlow(),
            new NetNoLanFlow()
        ];
    }

    public DiagnosisReport Evaluate(HostFacts facts)
    {
        var results = new List<DiagnosisResult>();
        foreach (var flow in _flows)
        {
            var r = flow.Evaluate(facts);
            if (r.Matched)
                results.Add(r);
        }

        var ranked = results
            .SelectMany(r => r.Solutions.Select(s => (Flow: r.FlowId, Solution: s)))
            .OrderByDescending(x => x.Solution.Score)
            .ThenBy(x => x.Solution.Risk)
            .Select(x => x.Solution)
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .ToList();

        var headline = results.Count == 0
            ? "No active network incidents matched."
            : string.Join(" · ", results.Select(r => r.Title));

        return new DiagnosisReport
        {
            Results = results,
            RankedSolutions = ranked,
            Headline = headline
        };
    }
}

public interface IDiagnosisFlow
{
    string Id { get; }
    DiagnosisResult Evaluate(HostFacts facts);
}
