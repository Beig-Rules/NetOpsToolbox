# Feature map

| Area | Status |
|------|--------|
| Diagnose | 4 flows |
| Actions | FlushDns, RenewDhcp, Set DNS |
| Tools | Ping DNS Port Trace Subnet calc |
| Monitor | Live ping + NIC Mbps |
| **Scan** | Subnet TCP port scan, ping sweep, Wi-Fi |
| Defaults | Vendor CPE defaults |
| Tweaks | netsh TCP |
| Firmware | Catalog diagnose |
| Security | ARP baseline |
| Playbooks | 3 templates |
| Reports | TXT + CSV |
| SSH | MikroTik / Cisco / Ubiquiti |
| Vault | DPAPI |
| Jobs | Multi-device queue |

## Scan safety

- Max 256 hosts (prefix forced ≥ /24)
- Default common ports: 22,23,53,80,443,445,3389,8080,8443,8728,8729
- Confirm dialog before port scan
- Authorized networks only

## Wi-Fi

- `netsh wlan show interfaces`
- `netsh wlan show networks mode=bssid`
- `netsh wlan show profiles`
