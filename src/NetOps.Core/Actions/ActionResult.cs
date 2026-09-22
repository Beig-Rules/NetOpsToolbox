namespace NetOps.Core.Actions;

public sealed class ActionResult
{
    public string ActionId { get; init; } = "";
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Message { get; init; } = "";
    public string? StdOut { get; init; }
    public string? StdErr { get; init; }
    public bool? VerifyOk { get; init; }
    public string? VerifyDetail { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}
