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
| Cisco | show version / show run + **enable password** |
| Vault | DPAPI login + enable, **list / double-click load / delete** |

## Cisco enable

1. Enter login password in first PasswordBox
2. Enter enable secret in second PasswordBox (optional)
3. Save vault stores both (DPAPI)
4. show run sends `enable` then password when prompt detected

## Vault UI

- List shows `host:port user [vendor] +enable`
- Double-click or Load applies credentials to fields
- Delete removes selected entry
