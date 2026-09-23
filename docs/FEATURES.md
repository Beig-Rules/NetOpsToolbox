# Feature map

| Area | Status |
|------|--------|
| Diagnose | 4 flows + FlushDns / RenewDhcp / **Set DNS with NIC picker** |
| Tools | Ping · DNS · Port · Trace · Subnet · Speed · Public IP · ARP · Route · Netstat · Interfaces · Hosts · TLS · **Event log** |
| Monitor | Live ping + NIC Mbps |
| Scan | Subnet TCP ports, ping sweep, Wi-Fi |
| Defaults | Vendor CPE defaults |
| Tweaks | netsh TCP |
| Firmware | Catalog **v1.1** (multi-vendor) |
| Security | ARP baseline + new host diff |
| Playbooks | Templates |
| Reports | TXT · CSV · HTML |
| SSH | MikroTik / Cisco / Ubiquiti |
| Vault | DPAPI |
| Jobs | Multi-device queue |

## Modular rule

New tools live in `NetOps.Core/Tools/*` and partial `MainWindow.*.cs` only — no cross-panel coupling.

## License

Proprietary exclusive — Beig-Rules. See LICENSE / ABOUT.md.
