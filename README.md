# NetOps Toolbox

Native Windows network specialist workbench with **Minimal Mono** UI signature (Beig-Rules).

[![CI](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/ci.yml/badge.svg)](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/ci.yml)
[![Pages](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/pages.yml/badge.svg)](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/pages.yml)

## UI
English only. Black/white minimal shell.

## Preview

| Source | URL |
|--------|-----|
| **GitHub Pages** | https://beig-rules.github.io/NetOpsToolbox/ |
| **Repo file** | [index.html](./index.html) |

## Native Windows (Phase 1)

| Project | Role |
|---------|------|
| `src/NetOps.Core` | Brand catalog + shared models |
| `src/NetOps.App` | WPF shell, `requireAdministrator`, Minimal Mono |

```bat
dotnet build NetOpsToolbox.sln -c Release
dotnet run --project src/NetOps.App
```

CI builds on **windows-latest** and uploads **NetOpsToolbox-win-x64** artifact. See `docs/NATIVE.md`.

## Data
`data/brands/catalog.v1.json` — Huawei, TP-Link, D-Link, ASUS, MikroTik, Cisco, …

## GitHub Actions

| Workflow | Purpose |
|----------|---------|
| **CI** | Catalog validate (Ubuntu) + **Native WPF build** (Windows) |
| **Pages** | Web prototype host |
| **Packages** | npm `@beig-rules/netops-toolbox` |
| **Dependabot** | Actions + npm |

## Status
Phase 1 — web preview live + native WPF shell + CI artifact. WinUI 3 host and device drivers next.
