# Feature map

| Area | Status |
|------|--------|
| Diagnose | 4 flows |
| Actions | FlushDns, RenewDhcp, Set DNS |
| Tools | Ping DNS Port Trace Subnet |
| Firmware | Catalog diagnose |
| Security | ARP baseline |
| Playbooks | 3 templates |
| Reports | TXT + CSV |
| MikroTik | SSH identity + export |
| Cisco | show version / run + enable |
| **Ubiquiti** | EdgeOS `show configuration commands` + UniFi fallbacks |
| Vault | DPAPI list / load / delete |
| **Jobs** | Multi-device queue, concurrency 2 |

## Ubiquiti

- Identity: `show version` then Linux fallbacks
- Export: `show configuration commands` → `/config/config.boot` → UniFi JSON head
- Files: `Documents\\NetOpsToolbox\\backups\\ubnt-*.txt`

## Job queue

1. Save devices to vault (correct Vendor brand)
2. Open **Jobs**
3. Queue MT / Cisco / UBNT for matching vault entries
4. Watch status panel; backups land in Documents folder

Passwords cleared from job objects after run.
