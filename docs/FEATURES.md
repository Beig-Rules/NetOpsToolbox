# Feature map

| Area | Status |
|------|--------|
| Diagnose | 4 flows + FlushDns / RenewDhcp / Set DNS |
| Tools | Ping · DNS · Port · Trace · Subnet · Speed · Public IP · ARP · **Route** · **Netstat** · **Interfaces** · **Hosts** |
| Monitor | Live ping + NIC Mbps |
| Scan | Subnet TCP ports, ping sweep, Wi-Fi |
| Defaults | Vendor CPE defaults |
| Tweaks | netsh TCP |
| Firmware | Catalog diagnose |
| Security | ARP baseline + new host diff |
| Playbooks | Templates |
| Reports | TXT · CSV · **HTML** (Minimal Mono) |
| SSH | MikroTik / Cisco / Ubiquiti |
| Vault | DPAPI |
| Jobs | Multi-device queue |

## Modular layout

Each tool lives under `NetOps.Core/Tools/*Service.cs` and is wired only via partial `MainWindow.*.cs` handlers — no cross-panel coupling.

## License

Proprietary exclusive — Beig-Rules. See LICENSE / ABOUT.md.
