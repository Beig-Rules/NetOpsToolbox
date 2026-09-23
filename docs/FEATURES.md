# Feature map

| Area | Status |
|------|--------|
| Diagnose | 6 flows |
| Tools | Ping · DNS · Port · Trace · Subnet · Speed · Public IP · ARP · Route · Netstat · Interfaces · Hosts · TLS · Event log · Proxy · Certs · **SNMP** · **Firewall** |
| Monitor / Scan | Live ping, NIC Mbps, port scan, Wi-Fi |
| Defaults / Tweaks | Vendor defaults, netsh TCP |
| Firmware / Security | Catalogs, ARP baseline |
| Playbooks | 10 templates |
| Reports | TXT · CSV · HTML |
| SSH / Vault / Jobs | MT · Cisco · UBNT · DPAPI · queue |
| CI | Catalog + xUnit + Windows publish |

## SNMP usage

Tools input examples:

```
192.168.1.1
192.168.1.1|public
192.168.1.1|public|1.3.6.1.2.1.1.5.0
```

Default: SNMPv2c GET sysDescr + system summary. **Authorized devices only.**

## License

Proprietary exclusive — Beig-Rules.
