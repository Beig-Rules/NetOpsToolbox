# Feature map

| Area | Status |
|------|--------|
| Diagnose | 4 flows + ranked solutions |
| Actions | FlushDns, RenewDhcp, SetAdapterDnsPublic |
| Tools | Ping, DNS, Port, Traceroute, Subnet |
| Firmware | Catalog diagnose (no auto-flash) |
| Security | ARP baseline + new host diff |
| Playbooks | Harden, NTP/DNS, WAN triage |
| Reports | TXT + CSV |
| MikroTik SSH | Identity + `/export` |
| **Cisco SSH** | `show version`, `show running-config` → file |
| **Vault** | DPAPI CurrentUser (`vault.json`) |

## Vault

- Path: `%LocalAppData%\NetOpsToolbox\vault.json`
- Password: `ProtectedData` + entropy `NetOpsToolbox.Vault.v1`
- Scope: same Windows user only

## Cisco notes

- Shell stream + `terminal length 0`
- Privilege 15 / AAA may be required for `show running-config`
- Enable password flows not automated in this build (use user with privilege)
