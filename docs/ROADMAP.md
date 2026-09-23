# Completion roadmap

## Done (production baseline)
- Diagnosis engine (6 flows)
- Full Tools suite including Proxy/PAC, Certs, TLS, Event log
- NIC-aware Set DNS, live monitor, scan, Wi-Fi
- SSH multi-vendor + DPAPI vault + job queue
- Firmware/brands catalogs, security baseline
- **10 playbooks** (harden, NTP/DNS, WAN, captive, new host, TLS, DNS suspect, MikroTik, Cisco, firmware)
- Reports TXT/CSV/HTML
- CI tests + Windows artifact
- Proprietary license + bilingual README + Minimal Mono UI

## Optional future
- WinUI host shell (same Core)
- SNMP read-only helpers
- More vendor SSH profiles

## Non-goals
- Unauthorized scanning / exploits
- Silent registry writes outside allow-list
