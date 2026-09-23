# Completion roadmap (modular)

Policy: add modules only when they do not break hierarchy or create conflicts.

## Done
- Diagnosis engine + 4 flows
- Safe actions (DNS/DHCP) + **NIC picker for Set DNS**
- Tools suite (connectivity + system reads + **Event log** + TLS)
- Live monitor, scan, Wi-Fi
- SSH vendors + vault + jobs
- Security baseline, firmware **v1.1**, playbooks
- Reports TXT/CSV/HTML
- CI + Pages + proprietary license
- Bilingual README
- Expanded brand catalog

## Next modules
1. CI smoke test for Core unit helpers
2. Optional WinUI host shell (same Core)
3. Certificate store / proxy PAC summary (read-only)
4. More diagnosis flows (proxy-on, captive portal hints)

## Non-goals
- Remote exploit / unauthorized scanning
- Silent registry writes outside allow-list actions
