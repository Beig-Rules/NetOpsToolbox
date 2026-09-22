# NetOps Toolbox

Native Windows network specialist workbench with **Minimal Mono** UI signature (Beig-Rules).

[![CI](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/ci.yml/badge.svg)](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/ci.yml)
[![Pages](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/pages.yml/badge.svg)](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/pages.yml)

## UI
English only. Black/white minimal shell.

## Preview (open this first)

| Source | URL |
|--------|-----|
| **Repo file** | [index.html](./index.html) |
| **jsDelivr** | https://cdn.jsdelivr.net/gh/Beig-Rules/NetOpsToolbox@main/index.html |
| **GitHub Pages** | https://beig-rules.github.io/NetOpsToolbox/ *(after enabling Pages → GitHub Actions once)* |

Also: `prototype/index.html` (same shell).

## Data
`data/brands/catalog.v1.json` — brand/model registry (day one): Huawei, TP-Link, D-Link, ASUS, MikroTik, Cisco, and more.

## Docs
- `docs/DESIGN_SYSTEM.md` (add when present)
- `docs/SCREENS.md`
- `docs/PHASE0.md`
- `PACKAGES.md`

## GitHub Actions

| Workflow | Purpose |
|----------|---------|
| **CI** | Validate catalog JSON + required files + HTML markers |
| **Deploy Preview to GitHub Pages** | Host Minimal Mono shell |
| **Publish GitHub Packages** | npm `@beig-rules/netops-toolbox` |
| **Dependabot** | Weekly Actions + monthly npm updates |

### Enable Pages (one time)
1. Open **Settings → Pages**
2. Source: **GitHub Actions** (or Deploy from branch `main` / root)
3. Re-run **Deploy Preview to GitHub Pages** workflow if needed

## Status
Phase 0 — design lock + catalog + UI prototype + CI/Pages/packages. Native WinUI solution next.
