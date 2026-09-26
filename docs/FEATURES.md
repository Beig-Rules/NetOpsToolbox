# Feature map

| Area | Status |
|------|--------|
| Diagnose | **10 flows** |
| Tools | Full suite + SNMP + Firewall + Certs + Proxy + TLS |
| Devices SSH | MikroTik · Cisco · Ubiquiti · Juniper · Aruba · Fortinet · Palo Alto · **Huawei VRP** |
| Jobs | Queue all vendors |
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

## SSH vendors

| Vendor | Actions |
|--------|---------|
| MikroTik | Test, Export |
| Cisco | Version, show run |
| Ubiquiti | Identity, Export |
| Juniper | Version, configuration |
| Aruba | Version, show running-config |
| Fortinet | system status, full-configuration |
| Palo Alto | system info, config running |
| Huawei | display version, current-configuration |

## License

Proprietary exclusive — Beig-Rules.
