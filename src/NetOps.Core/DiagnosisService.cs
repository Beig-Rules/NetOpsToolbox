using NetOps.Core.Collectors;
using NetOps.Core.Diagnosis;
using NetOps.Core.Facts;

namespace NetOps.Core;

public sealed class DiagnosisService
{
    private readonly HostNetworkCollector _collector = new();
    private readonly DiagnosisEngine _engine = new();

    public async Task<(HostFacts Facts, DiagnosisReport Report)> RunAsync(CancellationToken ct = default)
    {
        var facts = await _collector.CollectAsync(ct).ConfigureAwait(false);
        var report = _engine.Evaluate(facts);
        return (facts, report);
    }

    public DiagnosisReport EvaluateFacts(HostFacts facts) => _engine.Evaluate(facts);
}
