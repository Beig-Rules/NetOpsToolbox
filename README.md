# NetOps Toolbox

**Native Windows network specialist workbench** · Minimal Mono UI · Beig-Rules

[![CI](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/ci.yml/badge.svg)](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/ci.yml)
[![Pages](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/pages.yml/badge.svg)](https://github.com/Beig-Rules/NetOpsToolbox/actions/workflows/pages.yml)

> **License:** Proprietary · All Rights Reserved · Beig-Rules · **NOT open source**  
> Unauthorized use or fork may be pursued; liability for damages may apply.  
> See [LICENSE](./LICENSE) · [ABOUT.md](./ABOUT.md) · [LEGAL.md](./LEGAL.md)

---

## English

### What it is
A **modular, admin-required** Windows WPF app for network engineers and field techs:

| Panel | What you get |
|-------|----------------|
| **Diagnose** | Predefined flows (DNS fail, no WAN, no LAN, broken route) + safe actions (Flush DNS, Renew DHCP, Set public DNS) |
| **Tools** | Ping, DNS lookup, TCP port check, traceroute, subnet calc, **HTTP speed test**, public IP, ARP table |
| **Monitor** | Live multi-host ping + NIC throughput (Mbps) |
| **Scan** | Subnet port scan (/24 max), ping sweep, Wi-Fi interface / networks / profiles |
| **Devices** | Brand/model catalog, SSH to **MikroTik / Cisco / Ubiquiti**, config export, DPAPI credential vault |
| **Jobs** | Queue multi-device backups from vault |
| **Defaults** | Vendor default CPE credentials (authorized use only) |
| **Tweaks** | Windows TCP/netsh tweaks |
| **Firmware** | Catalog-based firmware diagnose hints |
| **Security** | LAN ARP scan, baseline save, new-host diff |
| **Playbooks** | Step templates for common incidents |
| **Reports** | TXT + CSV export |

UI language of the app: **English only**. This README is bilingual.

### Requirements
- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- **Administrator** rights (manifest `requireAdministrator`)
- Optional: outbound HTTPS for speed test / public IP

### Build & run

```bat
git clone https://github.com/Beig-Rules/NetOpsToolbox.git
cd NetOpsToolbox
dotnet restore NetOpsToolbox.sln
dotnet build NetOpsToolbox.sln -c Release
dotnet run --project src/NetOps.App -c Release
```

Or open `NetOpsToolbox.sln` in Visual Studio 2022+ and run **NetOps.App** as Administrator.

CI on `windows-latest` publishes artifact **NetOpsToolbox-win-x64** — download from Actions → CI → Artifacts.

### First-use path (5 minutes)
1. Launch as Admin → status bar should show catalog version.
2. **Diagnose** → Run diagnosis → review ranked solutions → Flush DNS / Renew DHCP if needed.
3. **Tools** → enter `1.1.1.1` → Ping; try **Speed** for download throughput.
4. **Monitor** → hosts `1.1.1.1, 8.8.8.8` → Start live.
5. **Devices** → pick brand/model/IP → enter SSH user/pass → Save vault → MT Test / Cisco Ver.
6. **Security** → Scan LAN → Save baseline → later Diff vs baseline.
7. **Reports** → Generate → Save TXT/CSV under Documents if prompted path logic applies.

### Safety notes
- Only scan/manage **networks and devices you own or are authorized to touch**.
- Default vendor credentials are for recovery of **your** equipment, not unauthorized access.
- SSH exports write under `%USERPROFILE%\Documents\NetOpsToolbox\backups`.
- Vault file is DPAPI-protected under `%LocalAppData%\NetOpsToolbox\`.
- Actions that change the host (DNS, DHCP, netsh) require confirmation dialogs.

### Architecture
```
src/NetOps.Core   → diagnosis, actions, collectors, SSH, jobs, reports, tools
src/NetOps.App    → WPF Minimal Mono shell
data/brands       → brand/model catalog
data/firmware     → firmware catalog hints
data/credentials  → vendor default map
```

### Preview (web shell only)
| Source | URL |
|--------|-----|
| GitHub Pages | https://beig-rules.github.io/NetOpsToolbox/ |
| Repo file | [index.html](./index.html) |

Web prototype is UI reference only; real power is the **native** app.

### Copyright
© 2025–2026 Beig-Rules (Beig) / Big Flow. All Rights Reserved.  
Proprietary exclusive license. Not MIT / Apache / GPL.

---

## فارسی

### این پروژه چیست؟
جعبه‌ابزار **بومی ویندوز** برای متخصص شبکه با رابط **Minimal Mono** (امضای Beig-Rules):

| پنل | کاربرد |
|-----|--------|
| **Diagnose** | فلوهای ازپیش‌تعریف‌شده (DNS، WAN، LAN، مسیر) + اکشن امن |
| **Tools** | پینگ، DNS، پورت، traceroute، سابنت، **تست سرعت**، IP عمومی، جدول ARP |
| **Monitor** | پینگ زنده + ترافیک کارت شبکه |
| **Scan** | اسکن پورت سابنت، ping sweep، اطلاعات وای‌فای |
| **Devices** | کاتالوگ برند، SSH به میکروتیک/سیسکو/یوبیکوئیتی، ولت رمز |
| **Jobs** | صف بک‌آپ چند دستگاه |
| **Security** | اسکن LAN و تشخیص میزبان جدید |
| **Reports** | خروجی TXT و CSV |

زبان رابط برنامه: **فقط انگلیسی**. این راهنما دو زبانه است.

### پیش‌نیاز
- ویندوز ۱۰/۱۱ شصت‌وچهاربیتی
- SDK دات‌نت ۸
- اجرای **با دسترسی Administrator**

### ساخت و اجرا

```bat
git clone https://github.com/Beig-Rules/NetOpsToolbox.git
cd NetOpsToolbox
dotnet build NetOpsToolbox.sln -c Release
dotnet run --project src/NetOps.App -c Release
```

### مسیر استفادهٔ اول (حدود ۵ دقیقه)
1. اجرا با Admin
2. Diagnose → Run → در صورت نیاز Flush DNS / Renew DHCP
3. Tools → Ping و Speed
4. Monitor → Start live
5. Devices → ذخیره ولت و تست SSH
6. Security → Scan و Save baseline
7. Reports → Generate و ذخیره

### نکات احتیاط
- فقط روی شبکه‌ها و تجهیزاتی که **مالک یا مجاز** هستید کار کنید.
- پسورد پیش‌فرض سازنده فقط برای بازیابی دستگاه خودتان است.
- بک‌آپ SSH در Documents\NetOpsToolbox\backups ذخیره می‌شود.
- ولت با DPAPI محافظت می‌شود.

### کپی‌رایت
© ۲۰۲۵–۲۰۲۶ متعلق به Beig-Rules (Beig) / Big Flow. تمامی حقوق محفوظ است.  
لایسنس اختصاصی و انحصاری — متن‌باز نیست.  
استفاده یا فورک غیرمجاز ممکن است پیگیری شود و متخلف مکلف به جبران خسارت باشد.

فایل‌های حقوقی: [LICENSE](./LICENSE) · [NOTICE](./NOTICE) · [LEGAL.md](./LEGAL.md) · [ABOUT.md](./ABOUT.md)
