# Diagnosis engine

## Pipeline

```
Collect (HostNetwork + scoped Registry) → Facts → Flows → Ranked Solutions → Actions (advisory Phase 1)
```

## Flows implemented

| Id | Meaning |
|----|---------|
| `DNS_FAIL` | LAN/path up, name resolution fails |
| `NET_NO_WAN` | LAN + gateway OK, beyond-gateway probe fails |
| `ROUTE_BROKEN` | Missing or multiple default routes |
| `NET_NO_LAN` | No up adapter with IPv4 |

## Registry scope (read-only)

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings` proxy keys only

No registry writes in Phase 1.

## Actions

Catalog in `ActionCatalog`. Execute/rollback of mutating actions is deferred; UI shows advisory solutions only.
