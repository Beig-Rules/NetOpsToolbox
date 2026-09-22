namespace NetOps.Core.Facts;

public sealed class HostFacts
{
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<AdapterFact> Adapters { get; init; } = new();
    public List<string> DefaultGateways { get; init; } = new();
    public List<string> DnsServers { get; init; } = new();
    public bool ProxyEnabled { get; init; }
    public string? ProxyServer { get; init; }
    public List<RouteFact> Routes { get; init; } = new();
    public ConnectivityFact Connectivity { get; init; } = new();
    public List<string> CollectorNotes { get; init; } = new();
}

public sealed class AdapterFact
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Status { get; init; } = "";
    public bool IsUp { get; init; }
    public List<string> IPv4Addresses { get; init; } = new();
    public List<string> DnsServers { get; init; } = new();
    public int? InterfaceMetric { get; init; }
}

public sealed class RouteFact
{
    public string Destination { get; init; } = "";
    public string Gateway { get; init; } = "";
    public string Interface { get; init; } = "";
    public int Metric { get; init; }
}

public sealed class ConnectivityFact
{
    public bool? GatewayReachable { get; init; }
    public long? GatewayRttMs { get; init; }
    public bool? PublicDnsReachable { get; init; }
    public bool? NameResolutionWorks { get; init; }
    public string? ResolvedProbe { get; init; }
}
