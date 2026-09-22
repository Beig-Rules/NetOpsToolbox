namespace NetOps.Core.Actions;

public enum ActionRisk { Low, Medium, High }

public sealed class ActionDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public ActionRisk Risk { get; init; }
    public bool IsAdvisoryOnly { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>
/// Phase 1: catalog of actions. Mutating execute comes in a later phase with confirm + rollback.
/// </summary>
public static class ActionCatalog
{
    public static IReadOnlyList<ActionDefinition> All { get; } =
    [
        new() { Id = "FlushDns", Title = "Flush DNS", Risk = ActionRisk.Low, IsAdvisoryOnly = false,
            Description = "ipconfig /flushdns" },
        new() { Id = "SetAdapterDnsPublic", Title = "Set public DNS", Risk = ActionRisk.Medium, IsAdvisoryOnly = true,
            Description = "Preview only in Phase 1 — no write yet." },
        new() { Id = "RenewDhcp", Title = "Renew DHCP", Risk = ActionRisk.Medium, IsAdvisoryOnly = true,
            Description = "ipconfig /release + /renew (not auto-run yet)." },
        new() { Id = "ReportProxyOnly", Title = "Report proxy", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Show proxy facts only." },
        new() { Id = "AdviseRouterDns", Title = "Advise router DNS", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Operator guidance for CPE DNS." },
        new() { Id = "AdviseCpeWan", Title = "Advise CPE WAN", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Check ISP/WAN light and CPE status page." },
        new() { Id = "FirmwareDiagnose", Title = "Firmware diagnose", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Hook to Firmware Center." },
        new() { Id = "AdviseInterfaceMetric", Title = "Advise metrics", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Review VPN vs LAN metrics." },
        new() { Id = "AdviseGateway", Title = "Advise gateway", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Restore default gateway via DHCP." },
        new() { Id = "AdvisePhysical", Title = "Advise physical", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Cable / Wi-Fi / airplane mode." },
        new() { Id = "AdviseEnableAdapter", Title = "Advise enable NIC", Risk = ActionRisk.Medium, IsAdvisoryOnly = true,
            Description = "Enable disabled adapter (future execute)." },
    ];
}
