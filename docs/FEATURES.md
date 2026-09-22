# Feature map (current)

| Area | Status |
|------|--------|
| Diagnose flows | DNS_FAIL, NET_NO_WAN, ROUTE_BROKEN, NET_NO_LAN |
| Actions live | FlushDns, RenewDhcp, SetAdapterDnsPublic (netsh) |
| Tools | Ping multi, DNS, Port, Traceroute, Subnet |
| Firmware | Catalog diagnose + offline flash guidance (no auto-brick flash) |
| Security | ARP scan, baseline save, new-host diff |
| Playbooks | Harden, NTP/DNS, WAN triage (step lists) |
| Reports | Text report to Desktop |
| Registry | Proxy + Tcpip Parameters read-only |

Firmware **auto-flash is intentionally not executed** — diagnosis and safe procedure only.
