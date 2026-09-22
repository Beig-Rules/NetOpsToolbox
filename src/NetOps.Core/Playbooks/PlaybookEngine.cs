using System.Text;

namespace NetOps.Core.Playbooks;

public sealed class Playbook
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> Steps { get; init; } = new();
}

public static class PlaybookEngine
{
    public static IReadOnlyList<Playbook> All { get; } =
    [
        new Playbook
        {
            Id = "baseline_harden",
            Title = "Baseline management harden",
            Description = "SSH-only mindset, banners, idle timeout, disable telnet/http if possible.",
            Steps =
            [
                "Inventory device and take config backup",
                "Disable telnet / clear-text management where supported",
                "Prefer SSH key or strong unique password in vault",
                "Set idle timeout on VTY/API sessions",
                "Restrict management access to admin subnet/ACL",
                "Enable logging to remote syslog if available",
                "Verify still reachable from admin host only"
            ]
        },
        new Playbook
        {
            Id = "ntp_dns",
            Title = "NTP + DNS baseline",
            Description = "Consistent time and resolver settings on CPE and Windows host.",
            Steps =
            [
                "Pick internal or trusted NTP sources",
                "Configure CPE NTP; verify clock",
                "Align Windows time service / domain time",
                "Set DNS: internal first, optional public fallback",
                "Flush DNS client; test resolution",
                "Document settings in site runbook"
            ]
        },
        new Playbook
        {
            Id = "wan_outage",
            Title = "WAN outage triage",
            Description = "Structured path when LAN works but Internet does not.",
            Steps =
            [
                "Confirm LAN + gateway ping from NetOps Diagnose",
                "Check CPE WAN status / lights / ISP portal",
                "Note last change (firmware, cable, storm)",
                "Power-cycle CPE only after backup if recent change",
                "Test alternate path (phone tether) to isolate ISP",
                "Open ISP ticket with timestamps and probe results"
            ]
        }
    ];

    public static string Render(Playbook p)
    {
        var sb = new StringBuilder();
        sb.AppendLine(p.Title);
        sb.AppendLine(p.Description);
        sb.AppendLine();
        for (var i = 0; i < p.Steps.Count; i++)
            sb.AppendLine($"{i + 1}. {p.Steps[i]}");
        return sb.ToString();
    }
}
