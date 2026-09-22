namespace NetOps.Core.Diagnosis;

public enum RiskLevel { Low, Medium, High }

public sealed class DiagnosisResult
{
    public string FlowId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public bool Matched { get; init; }
    public List<string> Evidence { get; init; } = new();
    public List<SolutionOffer> Solutions { get; init; } = new();
}

public sealed class SolutionOffer
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public RiskLevel Risk { get; init; }
    public int Score { get; init; }
    public List<string> ActionIds { get; init; } = new();
    public bool RequiresConfirm { get; init; } = true;
}

public sealed class DiagnosisReport
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<DiagnosisResult> Results { get; init; } = new();
    public List<SolutionOffer> RankedSolutions { get; init; } = new();
    public string Headline { get; init; } = "No active network incidents matched.";
}
