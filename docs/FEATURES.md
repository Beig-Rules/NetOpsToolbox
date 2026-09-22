# Feature map

| Area | Status |
|------|--------|
| Diagnose flows | DNS_FAIL, NET_NO_WAN, ROUTE_BROKEN, NET_NO_LAN |
| Actions live | FlushDns, RenewDhcp, SetAdapterDnsPublic |
| Tools | Ping, DNS, Port, Traceroute, Subnet |
| Firmware | Catalog diagnose (no auto-flash) |
| Security | ARP scan, baseline, new-host diff |
| Playbooks | Harden, NTP/DNS, WAN triage |
| Reports | TXT + **CSV** to Desktop |
| Devices | **MikroTik SSH** identity + `/export` backup to Documents |
| Registry | Proxy + Tcpip read-only |

## MikroTik backup

- Package: SSH.NET
- Command: `/export show-sensitive=no terse`
- Output: `Documents\\NetOpsToolbox\\backups\\mikrotik-*.rsc`
- Password: in-memory only (PasswordBox); not written to audit in clear text
