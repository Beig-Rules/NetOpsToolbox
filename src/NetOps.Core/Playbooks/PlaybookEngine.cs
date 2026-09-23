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
        },
        new Playbook
        {
            Id = "captive_portal",
            Title = "Captive portal / guest Wi-Fi",
            Description = "When gateway works but public DNS/names fail (hotel, airport, guest SSID).",
            Steps =
            [
                "Run Diagnose — look for CAPTIVE_PORTAL match",
                "Open browser to http://neverssl.com or gateway IP",
                "Complete login / accept terms",
                "Flush DNS (Diagnose → Flush DNS)",
                "Re-test name resolution and HTTPS",
                "If still broken: renew DHCP, forget/rejoin Wi-Fi profile"
            ]
        },
        new Playbook
        {
            Id = "new_host_lan",
            Title = "New host on LAN alert",
            Description = "Respond when Security baseline shows a new MAC/IP.",
            Steps =
            [
                "Security → Diff vs baseline; note MAC + IP",
                "Identify device (ARP vendor OUI, switch port if available)",
                "Confirm expected (printer, phone, guest) vs unknown",
                "If unknown: isolate VLAN / block, escalate",
                "If expected: Save baseline to accept",
                "Document owner and purpose"
            ]
        },
        new Playbook
        {
            Id = "tls_break",
            Title = "TLS / certificate failure",
            Description = "Browser or API TLS errors to internal or public hosts.",
            Steps =
            [
                "Tools → TLS probe host:443; note chain errors",
                "Tools → Certs; check expired roots / intermediates",
                "Verify system clock (wrong time breaks TLS)",
                "Check proxy/SSL inspection (Tools → Proxy)",
                "Reinstall or update intermediate CA if internal PKI",
                "Retest with TLS probe after fix"
            ]
        },
        new Playbook
        {
            Id = "dns_poison_suspect",
            Title = "DNS misconfig / poison suspect",
            Description = "Names resolve to unexpected addresses or fail intermittently.",
            Steps =
            [
                "Tools → DNS for known good names; compare answers",
                "Check adapter DNS list (Tools → Interfaces)",
                "Diagnose → Set public DNS on selected NIC if authorized",
                "Flush DNS; retest",
                "Inspect hosts file (Tools → Hosts) for overrides",
                "If corporate: restore internal DNS + split-horizon docs"
            ]
        },
        new Playbook
        {
            Id = "mikrotik_backup",
            Title = "MikroTik backup + export",
            Description = "Safe routine before changes on RouterOS.",
            Steps =
            [
                "Devices → select host; load vault credentials",
                "MT Test connectivity",
                "MT Export — save output off-device",
                "Note RouterOS version (Firmware catalog)",
                "Apply planned change",
                "Verify routes, NAT, firewall; keep export dated"
            ]
        },
        new Playbook
        {
            Id = "cisco_change",
            Title = "Cisco change window",
            Description = "Minimal safe sequence for IOS/IOS-XE maintenance.",
            Steps =
            [
                "Vault credentials + enable secret",
                "Cisco Ver — confirm platform and image",
                "Cisco Run — save running-config offline",
                "Schedule maintenance; terminal length 0",
                "Apply change; verify show ip route / interfaces",
                "write memory only after verification"
            ]
        },
        new Playbook
        {
            Id = "firmware_cpe",
            Title = "CPE firmware update readiness",
            Description = "Before flashing consumer/SOHO or enterprise CPE.",
            Steps =
            [
                "Firmware panel: match brand/model exactly",
                "Confirm hardware revision on device label",
                "Download only from vendor or ISP-approved source",
                "Backup config; note current version",
                "Prefer wired management; stable power",
                "Flash; wait full reboot; restore config if needed",
                "Re-run Diagnose and document version"
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
