# Diagnosis engine

## Pipeline

```
Collect (HostNetwork + scoped Registry)
  → Facts
  → Flows
  → Ranked Solutions
  → Actions (FlushDns executable; others advisory)
  → Verify
  → Audit
```

## Flows

| Id | Meaning |
|----|---------|
| `DNS_FAIL` | LAN/path up, name resolution fails |
| `NET_NO_WAN` | LAN + gateway OK, beyond-gateway probe fails |
| `ROUTE_BROKEN` | Missing or multiple default routes |
| `NET_NO_LAN` | No up adapter with IPv4 |

## Registry scope (read-only)

- `HKCU\...\Internet Settings` — ProxyEnable / ProxyServer
- `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters` — Hostname, Domain, SearchList, NameServer

No registry writes.

## Live actions

| Id | Status |
|----|--------|
| `FlushDns` | **Execute** + DNS verify (`dns.google`) + audit |
| Others | Advisory only |

Confirm dialog required before FlushDns.
