# Feature map

| Area | Status |
|------|--------|
| Diagnose | 4 flows |
| Actions | FlushDns, RenewDhcp, Set DNS |
| Tools | Ping DNS Port Trace Subnet |
| **Monitor** | Live ping + NIC Mbps |
| **Defaults** | Vendor CPE default creds catalog |
| **Tweaks** | netsh TCP autotune / RSS / ECN |
| Firmware | Catalog diagnose |
| Security | ARP baseline |
| Playbooks | 3 templates |
| Reports | TXT + CSV |
| MikroTik / Cisco / Ubiquiti | SSH backup |
| Vault | DPAPI |
| Jobs | Multi-device queue |

## Monitor

- Hosts comma-separated
- Interval 2s
- NIC byte counters → approximate Mbps

## Defaults

- `data/credentials/defaults.v1.json`
- Public factory defaults only — authorized equipment only

## Tweaks

- Requires Administrator
- Confirm dialog before apply
- Read-only: Show TCP global
