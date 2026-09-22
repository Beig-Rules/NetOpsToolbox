# Native Windows build

## Stack (Phase 1)

| Layer | Tech |
|-------|------|
| UI | **WPF** .NET 8 — Minimal Mono shell (same tokens as web prototype) |
| Core | `NetOps.Core` — brand catalog models + loader |
| Elevation | `app.manifest` → `requireAdministrator` |
| Next | Optional WinUI 3 host with same Core |

WPF is used first so CI on `windows-latest` is reliable. Visual language matches the HTML prototype (black KPI cards, text nav, off-white canvas).

## Local build

```bat
dotnet restore NetOpsToolbox.sln
dotnet build NetOpsToolbox.sln -c Release
dotnet run --project src/NetOps.App/NetOps.App.csproj
```

Run as Administrator (manifest requested).

## CI

Job **Build Native Windows (WPF)** publishes `NetOpsToolbox-win-x64` artifact on every main push.
