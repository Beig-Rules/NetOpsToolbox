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

public static class ActionCatalog
{
    public static IReadOnlyList<ActionDefinition> All { get; } =
    [
        new() { Id = "FlushDns", Title = "Flush DNS", Risk = ActionRisk.Low, IsAdvisoryOnly = false,
            Description = "ipconfig /flushdns + resolve verify" },
        new() { Id = "RenewDhcp", Title = "Renew DHCP", Risk = ActionRisk.Medium, IsAdvisoryOnly = false,
            Description = "ipconfig /release then /renew + gateway verify" },
        new() { Id = "SetAdapterDnsPublic", Title = "Set public DNS", Risk = ActionRisk.Medium, IsAdvisoryOnly = false,
            Description = "netsh set DNS 1.1.1.1 / 1.0.0.1 on named interface" },
        new() { Id = "ReportProxyOnly", Title = "Report proxy", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Show proxy facts only." },
        new() { Id = "AdviseRouterDns", Title = "Advise router DNS", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Operator guidance for CPE DNS." },
        new() { Id = "AdviseCpeWan", Title = "Advise CPE WAN", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Check ISP/WAN light and CPE status page." },
        new() { Id = "FirmwareDiagnose", Title = "Firmware diagnose", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Firmware Center guidance." },
        new() { Id = "AdviseInterfaceMetric", Title = "Advise metrics", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Review VPN vs LAN metrics." },
        new() { Id = "AdviseGateway", Title = "Advise gateway", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Restore default gateway via DHCP." },
        new() { Id = "AdvisePhysical", Title = "Advise physical", Risk = ActionRisk.Low, IsAdvisoryOnly = true,
            Description = "Cable / Wi-Fi / airplane mode." },
        new() { Id = "AdviseEnableAdapter", Title = "Advise enable NIC", Risk = ActionRisk.Medium, IsAdvisoryOnly = true,
            Description = "Enable disabled adapter." },
    ];
}
