# Diagnosis + Tools

## Pipeline

```
Collect → Facts → Flows → Solutions → Actions (FlushDns, RenewDhcp) → Verify → Audit
```

## Executable actions

| Id | Risk | Verify |
|----|------|--------|
| `FlushDns` | Low | resolve dns.google |
| `RenewDhcp` | Medium | gateway ping + rollback guidance |

## Advisory actions

SetAdapterDnsPublic, proxy report, CPE/WAN/firmware/physical guidance.

## Tools

| Tool | Input |
|------|--------|
| Ping | `1.1.1.1, 8.8.8.8` |
| DNS | hostname |
| Port | `host:443` |
| Traceroute | host (Windows tracert) |
| Subnet | `192.168.1.0/24` |

## Registry (read-only)

Proxy (HKCU) + Tcpip Parameters (HKLM).
