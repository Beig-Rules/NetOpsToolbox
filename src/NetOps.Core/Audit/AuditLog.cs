using System.Collections.Concurrent;

namespace NetOps.Core.Audit;

public sealed class AuditEntry
{
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public string ActionId { get; init; } = "";
    public string Outcome { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class AuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public void Record(string actionId, string outcome, string detail)
    {
        _entries.Enqueue(new AuditEntry
        {
            ActionId = actionId,
            Outcome = outcome,
            Detail = detail
        });
    }

    public IReadOnlyList<AuditEntry> Snapshot()
        => _entries.ToArray();
}
