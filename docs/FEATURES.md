# Feature map

| Area | Status |
|------|--------|
| Diagnose | **10 flows** |
| Tools | Full suite + SNMP v2c + **SNMPv3** + Firewall + Certs + Proxy + TLS |
| Devices SSH | MikroTik · Cisco · Ubiquiti · Juniper · Aruba · Fortinet · Palo Alto · Huawei |
| Jobs | Queue all vendors + **Scheduler** |
| Playbooks | Operational templates |
| Reports | TXT · CSV · HTML |
| Vault | DPAPI |
| CI | Catalog + xUnit + Windows publish |

## Diagnosis flows

| Id | Trigger |
|----|---------|
| DNS_FAIL | LAN up, name resolution fails |
| NET_NO_WAN | No internet path |
| ROUTE_BROKEN | Routing broken |
| NET_NO_LAN | No LAN |
| PROXY_ON | System proxy intercept |
| CAPTIVE_PORTAL | Captive portal hints |
| ADAPTER_ALL_DOWN | No NIC up with IPv4 |
| HIGH_LATENCY_GW | Gateway RTT ≥ 80 ms |
| NO_DNS_CONFIG | Up but zero DNS servers |
| WAN_PARTIAL_FAIL | Gateway OK, public probe fail |

## SNMP

| Mode | Tools input |
|------|-------------|
| v2c | `host` or `host\|community` or `host\|community\|oid` |
| v3 | `host\|user\|authPass\|privPass` or `+\|oid` (SHA+AES, read-only) |

## Scheduler (Jobs panel)

- Schedule MT / Cisco every 60 minutes from vault
- Min interval 5 minutes (enforced)
- Run due / List / Clear

## License

Proprietary exclusive — Beig-Rules.
