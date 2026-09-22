using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NetOps.Core;
using NetOps.Core.Diagnosis;
using NetOps.Core.Devices;
using NetOps.Core.Facts;
using NetOps.Core.Firmware;
using NetOps.Core.Jobs;
using NetOps.Core.Models;
using NetOps.Core.Playbooks;
using NetOps.Core.Reports;
using NetOps.Core.Security;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow : Window
{
    private BrandCatalog? _catalog;
    private readonly DiagnosisService _diagnosis = new();
    private readonly NetworkTools _tools = new();
    private readonly LanBaselineService _lan = new();
    private readonly FirmwareService _fw = new();
    private readonly MikroTikSshService _mt = new();
    private readonly CiscoSshService _cisco = new();
    private readonly UbiquitiSshService _ubnt = new();
    private readonly CredentialVault _vault = new();
    private readonly JobQueue _jobs = new();
    private List<LanHost> _lastScan = new();
    private HostFacts? _lastFacts;
    private DiagnosisReport? _lastReport;
    private LanDiffResult? _lastLanDiff;
    private string _lastReportText = "";
    private string _lastReportCsv = "";

    private string BaselinePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetOpsToolbox", "lan-baseline.json");

    private string BackupDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NetOpsToolbox", "backups");

    public MainWindow()
    {
        InitializeComponent();
        _jobs.BackupDirectory = BackupDir;
        _jobs.MaxConcurrency = 2;
        _jobs.Changed += () => Dispatcher.Invoke(RefreshJobsPanel);
        LoadCatalog();
        LoadFirmwareCatalog();
        try { _vault.Load(); RefreshVaultList(); } catch { }
        foreach (var p in PlaybookEngine.All)
            PlaybookBox.Items.Add(p.Title);
        if (PlaybookBox.Items.Count > 0) PlaybookBox.SelectedIndex = 0;
        JobsText.Text = "—";
    }

    private void LoadCatalog()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "catalog.v1.json");
            if (!File.Exists(path))
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "brands", "catalog.v1.json"));
            _catalog = CatalogService.LoadFromFile(path);
            BrandBox.Items.Clear();
            foreach (var b in _catalog.Brands) BrandBox.Items.Add(b.Name);
            if (BrandBox.Items.Count > 0)
            {
                BrandBox.SelectedIndex = 0;
                BrandBox.SelectionChanged += (_, _) => FillModels();
                FillModels();
            }
            StatusText.Text = $"Catalog v{_catalog.Version} · vault={_vault.Entries.Count}";
        }
        catch (Exception ex) { StatusText.Text = "Catalog: " + ex.Message; }
    }

    private void LoadFirmwareCatalog()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "firmware-catalog.v1.json");
            if (!File.Exists(path))
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "firmware", "catalog.v1.json"));
            if (File.Exists(path)) _fw.Load(path);
        }
        catch { }
    }

    private void FillModels()
    {
        ModelBox.Items.Clear();
        if (_catalog is null || BrandBox.SelectedItem is null) return;
        var brand = _catalog.Brands.FirstOrDefault(b => b.Name == BrandBox.SelectedItem.ToString());
        if (brand is null) return;
        foreach (var m in CatalogService.ModelsForBrand(_catalog, brand.Id))
            ModelBox.Items.Add(m.Model);
        if (ModelBox.Items.Count > 0) ModelBox.SelectedIndex = 0;
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        ContentDashboard.Visibility = V(tag, "Dashboard");
        ContentDevices.Visibility = V(tag, "Devices");
        ContentJobs.Visibility = V(tag, "Jobs");
        ContentDiagnose.Visibility = V(tag, "Diagnose");
        ContentTools.Visibility = V(tag, "Tools");
        ContentFirmware.Visibility = V(tag, "Firmware");
        ContentSecurity.Visibility = V(tag, "Security");
        ContentPlaybooks.Visibility = V(tag, "Playbooks");
        ContentReports.Visibility = V(tag, "Reports");
        ContentOther.Visibility = tag is "Settings" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "Settings")
            ContentOther.Text =
                "Vault: " + _vault.Path + "\n" +
                "Backups: " + BackupDir + "\n" +
                "Job concurrency: " + _jobs.MaxConcurrency + "\n" +
                "Vendors SSH: MikroTik, Cisco, Ubiquiti (EdgeOS/UniFi).";
        if (tag == "Devices") RefreshVaultList();
        if (tag == "Jobs") RefreshJobsPanel();
    }

    private static Visibility V(string tag, string name) => tag == name ? Visibility.Visible : Visibility.Collapsed;

    private void AddDevice_Click(object sender, RoutedEventArgs e)
        => DeviceList.Items.Add($"{BrandBox.SelectedItem} / {ModelBox.SelectedItem} @ {IpBox.Text.Trim()}");

    private string VaultId() => IpBox.Text.Trim() + "|" + SshUserBox.Text.Trim() + "|" + SshPortBox.Text.Trim();

    private void RefreshVaultList()
    {
        VaultList.Items.Clear();
        foreach (var e in _vault.Entries)
            VaultList.Items.Add(new VaultListItem(e, _vault.DisplayLine(e)));
        StatusText.Text = $"Vault entries={_vault.Entries.Count}";
    }

    private void RefreshJobsPanel()
    {
        var sb = new StringBuilder();
        foreach (var j in _jobs.Snapshot().Take(40))
        {
            sb.AppendLine($"{j.CreatedAt:HH:mm:ss}  {j.Status,-10}  {j.Kind,-16}  {j.Host}  {j.Message}");
            if (!string.IsNullOrEmpty(j.LocalPath))
                sb.AppendLine("         → " + j.LocalPath);
        }
        JobsPanelText.Text = sb.Length == 0 ? "No jobs yet." : sb.ToString();
        var last = _jobs.Snapshot().Take(8);
        JobsText.Text = string.Join("\n", last.Select(j => $"{j.CreatedAt:HH:mm}  {j.Kind.ToString().PadRight(14)} {j.Status}"));
    }

    private void VaultRefresh_Click(object s, RoutedEventArgs e)
    {
        try { _vault.Load(); RefreshVaultList(); }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private void VaultSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var host = IpBox.Text.Trim();
            var user = SshUserBox.Text.Trim();
            var pass = SshPassBox.Password;
            var enable = EnablePassBox.Password;
            if (!int.TryParse(SshPortBox.Text.Trim(), out var port)) port = 22;
            if (string.IsNullOrEmpty(pass))
            {
                DeviceSshOutput.Text = "Login password required.";
                return;
            }
            var vendor = BrandBox.SelectedItem?.ToString() ?? "";
            _vault.Upsert(VaultId(), host, user, pass, port, vendor,
                string.IsNullOrEmpty(enable) ? null : enable);
            RefreshVaultList();
            DeviceSshOutput.Text = $"Vault saved: {host}" + (string.IsNullOrEmpty(enable) ? "" : " (+enable)");
            LogJob("VaultSave", "OK");
        }
        catch (Exception ex)
        {
            DeviceSshOutput.Text = "Vault save failed: " + ex.Message;
            LogJob("VaultSave", "FAIL");
        }
    }

    private void VaultLoad_Click(object sender, RoutedEventArgs e)
    {
        if (VaultList.SelectedItem is VaultListItem item)
            ApplyVaultEntry(item.Entry);
        else
            LoadVaultByHost();
    }

    private void VaultList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VaultList.SelectedItem is VaultListItem item)
            ApplyVaultEntry(item.Entry);
    }

    private void LoadVaultByHost()
    {
        try
        {
            _vault.Load();
            var host = IpBox.Text.Trim();
            var entry = _vault.Entries.FirstOrDefault(x => x.Host == host)
                        ?? _vault.Entries.FirstOrDefault(x => x.Id == VaultId());
            if (entry is null)
            {
                DeviceSshOutput.Text = "No vault entry for this host.";
                RefreshVaultList();
                return;
            }
            ApplyVaultEntry(entry);
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private void ApplyVaultEntry(VaultEntry entry)
    {
        IpBox.Text = entry.Host;
        SshUserBox.Text = entry.Username;
        SshPortBox.Text = entry.Port.ToString();
        var pass = _vault.UnprotectPassword(entry);
        var enable = _vault.UnprotectEnablePassword(entry);
        if (pass is null)
        {
            DeviceSshOutput.Text = "Decrypt failed (wrong Windows user?).";
            return;
        }
        SshPassBox.Password = pass;
        EnablePassBox.Password = enable ?? "";
        DeviceSshOutput.Text = $"Loaded {entry.Host} / {entry.Username}" +
            (enable is not null ? " (+enable)" : "") + $"  [{entry.Vendor}]";
        LogJob("VaultLoad", "OK");
    }

    private void VaultDelete_Click(object sender, RoutedEventArgs e)
    {
        if (VaultList.SelectedItem is not VaultListItem item)
        {
            DeviceSshOutput.Text = "Select a vault row.";
            return;
        }
        if (MessageBox.Show("Delete " + item.Entry.Host + "?", "Delete", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        _vault.Remove(item.Entry.Id);
        RefreshVaultList();
        DeviceSshOutput.Text = "Deleted " + item.Entry.Host;
        LogJob("VaultDel", "OK");
    }

    private (string host, string user, string pass, int port, string? enable) SshCreds()
    {
        if (!int.TryParse(SshPortBox.Text.Trim(), out var port)) port = 22;
        var enable = EnablePassBox.Password;
        return (IpBox.Text.Trim(), SshUserBox.Text.Trim(), SshPassBox.Password, port,
            string.IsNullOrEmpty(enable) ? null : enable);
    }

    private async void MikrotikTest_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, port, _) = SshCreds();
        DeviceSshOutput.Text = "MT…";
        var r = await _mt.IdentityAsync(host, user, pass, port).ConfigureAwait(true);
        DeviceSshOutput.Text = FormatResult(r);
        LogJob("MT-Test", r.Success ? "OK" : "FAIL");
    }

    private async void MikrotikBackup_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, port, _) = SshCreds();
        if (MessageBox.Show($"MT export {host}?", "Confirm", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        DeviceSshOutput.Text = "MT export…";
        var r = await _mt.ExportConfigAsync(host, user, pass, BackupDir, port).ConfigureAwait(true);
        DeviceSshOutput.Text = FormatResult(r);
        LogJob("MT-Export", r.Success ? "OK" : "FAIL");
    }

    private async void CiscoVersion_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, port, enable) = SshCreds();
        DeviceSshOutput.Text = "Cisco version…";
        var r = await _cisco.ShowVersionAsync(host, user, pass, port, enable).ConfigureAwait(true);
        DeviceSshOutput.Text = FormatResult(r);
        LogJob("IOS-Ver", r.Success ? "OK" : "FAIL");
    }

    private async void CiscoShowRun_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, port, enable) = SshCreds();
        if (MessageBox.Show($"Cisco show run {host}?", "Confirm", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        DeviceSshOutput.Text = "Cisco run…";
        var r = await _cisco.ShowRunningConfigAsync(host, user, pass, BackupDir, port, enable).ConfigureAwait(true);
        DeviceSshOutput.Text = FormatResult(r);
        LogJob("IOS-Run", r.Success ? "OK" : "FAIL");
    }

    private async void UbntId_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, port, _) = SshCreds();
        DeviceSshOutput.Text = "UBNT identity…";
        var r = await _ubnt.IdentityAsync(host, user, pass, port).ConfigureAwait(true);
        DeviceSshOutput.Text = FormatResult(r);
        LogJob("UBNT-ID", r.Success ? "OK" : "FAIL");
    }

    private async void UbntExport_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, port, _) = SshCreds();
        if (MessageBox.Show($"Ubiquiti export {host}?", "Confirm", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        DeviceSshOutput.Text = "UBNT export…";
        var r = await _ubnt.ExportConfigAsync(host, user, pass, BackupDir, port).ConfigureAwait(true);
        DeviceSshOutput.Text = FormatResult(r);
        LogJob("UBNT-Exp", r.Success ? "OK" : "FAIL");
    }

    private void QueueMtAll_Click(object s, RoutedEventArgs e) => QueueAll(JobKind.MikroTikExport, "MikroTik");
    private void QueueCiscoAll_Click(object s, RoutedEventArgs e) => QueueAll(JobKind.CiscoShowRun, "Cisco");
    private void QueueUbntAll_Click(object s, RoutedEventArgs e) => QueueAll(JobKind.UbiquitiExport, "Ubiquiti");

    private void QueueAll(JobKind kind, string vendorHint)
    {
        _vault.Load();
        var entries = _vault.Entries.ToList();
        if (entries.Count == 0)
        {
            MessageBox.Show("Vault is empty. Save devices first.");
            return;
        }

        // Prefer vendor match when possible, else queue all
        var filtered = entries.Where(x =>
            string.IsNullOrEmpty(x.Vendor) ||
            x.Vendor.Contains(vendorHint, StringComparison.OrdinalIgnoreCase) ||
            (vendorHint == "Ubiquiti" && x.Vendor.Contains("Ubiquiti", StringComparison.OrdinalIgnoreCase)) ||
            (vendorHint == "MikroTik" && x.Vendor.Contains("MikroTik", StringComparison.OrdinalIgnoreCase)) ||
            (vendorHint == "Cisco" && x.Vendor.Contains("Cisco", StringComparison.OrdinalIgnoreCase))
        ).ToList();
        if (filtered.Count == 0) filtered = entries;

        if (MessageBox.Show(
                $"Queue {filtered.Count} job(s) of type {kind}?\nBackups → {BackupDir}",
                "Job queue", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            return;

        _jobs.EnqueueFromVault(filtered, kind, _vault.UnprotectPassword, _vault.UnprotectEnablePassword);
        RefreshJobsPanel();
        LogJob("Queue", filtered.Count.ToString());
    }

    private void JobsRefresh_Click(object s, RoutedEventArgs e) => RefreshJobsPanel();

    private static string FormatResult(DeviceBackupResult r)
        => (r.Success ? "OK\n" : "FAIL\n") + r.Message +
           (r.LocalPath is not null ? "\nFile: " + r.LocalPath : "") +
           "\n\n" + (r.Preview ?? "");

    private async void RunDiagnose_Click(object sender, RoutedEventArgs e)
    {
        DiagnoseBusy.Text = "…";
        try
        {
            var (facts, report) = await _diagnosis.RunAsync().ConfigureAwait(true);
            _lastFacts = facts; _lastReport = report;
            var sb = new StringBuilder();
            sb.AppendLine(report.Headline);
            foreach (var r in report.Results)
            {
                sb.AppendLine($"[{r.FlowId}] {r.Title}");
                foreach (var ev in r.Evidence) sb.AppendLine("  · " + ev);
            }
            foreach (var s in report.RankedSolutions)
                sb.AppendLine($"#{s.Score} [{s.Risk}] {s.Title}");
            DiagnoseOutput.Text = sb.ToString();
            LogJob("Diagnose", "OK");
        }
        catch (Exception ex) { DiagnoseOutput.Text = ex.Message; }
        finally { DiagnoseBusy.Text = ""; }
    }

    private async void ApplyFlushDns_Click(object s, RoutedEventArgs e) => await RunAction("FlushDns", "Flush DNS?").ConfigureAwait(true);
    private async void ApplyRenewDhcp_Click(object s, RoutedEventArgs e) => await RunAction("RenewDhcp", "Renew DHCP?").ConfigureAwait(true);

    private async void ApplySetDns_Click(object s, RoutedEventArgs e)
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.GetIPProperties().GatewayAddresses.Any());
        _diagnosis.Executor.TargetInterfaceName = nic?.Name;
        await RunAction("SetAdapterDnsPublic", $"Set DNS on '{nic?.Name}'?").ConfigureAwait(true);
    }

    private async Task RunAction(string id, string prompt)
    {
        if (MessageBox.Show(prompt, id, MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        DiagnoseBusy.Text = id;
        try
        {
            var result = await _diagnosis.ExecuteActionAsync(id).ConfigureAwait(true);
            DiagnoseOutput.Text += $"\n\n=== {id} ===\n{(result.Success ? "OK" : "FAIL")}\n{result.Message}";
            LogJob(id, result.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DiagnoseOutput.Text += "\n" + ex.Message; }
        finally { DiagnoseBusy.Text = ""; }
    }

    private async void ToolPing_Click(object s, RoutedEventArgs e)
    {
        var hosts = ToolsInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = await _tools.PingManyAsync(hosts).ConfigureAwait(true);
        ToolsOutput.Text = string.Join("\n", results.Select(r => $"{r.Host}: {r.Status}"));
        LogJob("Ping", "OK");
    }

    private async void ToolDns_Click(object s, RoutedEventArgs e)
    {
        ToolsOutput.Text = await _tools.DnsLookupAsync(ToolsInput.Text.Split(',')[0]).ConfigureAwait(true);
        LogJob("DNS", "OK");
    }

    private async void ToolPort_Click(object s, RoutedEventArgs e)
    {
        var p = ToolsInput.Text.Trim().Split(':');
        if (p.Length != 2 || !int.TryParse(p[1], out var port)) { ToolsOutput.Text = "host:port"; return; }
        ToolsOutput.Text = await _tools.PortCheckAsync(p[0], port).ConfigureAwait(true);
        LogJob("Port", "OK");
    }

    private async void ToolTrace_Click(object s, RoutedEventArgs e)
    {
        ToolsOutput.Text = await _tools.TracerouteAsync(ToolsInput.Text.Split(',')[0]).ConfigureAwait(true);
        LogJob("Trace", "OK");
    }

    private void ToolSubnet_Click(object s, RoutedEventArgs e)
        => ToolsOutput.Text = NetworkTools.SubnetCalculate(ToolsInput.Text);

    private void FirmwareDiagnose_Click(object s, RoutedEventArgs e)
    {
        var brandName = BrandBox.SelectedItem?.ToString() ?? "";
        var brandId = _catalog?.Brands.FirstOrDefault(b => b.Name == brandName)?.Id ?? brandName.ToLowerInvariant();
        FirmwareOutput.Text = _fw.Diagnose(brandId, ModelBox.SelectedItem?.ToString() ?? "",
            FwVersionBox.Text.StartsWith("(") ? null : FwVersionBox.Text.Trim());
        LogJob("Firmware", "OK");
    }

    private async void SecurityScan_Click(object s, RoutedEventArgs e)
    {
        _lastScan = await _lan.ScanAsync().ConfigureAwait(true);
        SecurityOutput.Text = string.Join("\n", _lastScan.Select(h => $"{h.Ip,-15} {h.Mac,-18} {h.Type}"));
        LogJob("LanScan", "OK");
    }

    private void SecuritySave_Click(object s, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
        if (_lastScan.Count == 0) { SecurityOutput.Text = "Scan first."; return; }
        _lan.SaveBaseline(BaselinePath, _lastScan);
        SecurityOutput.Text += "\nSaved " + BaselinePath;
        LogJob("Baseline", "OK");
    }

    private async void SecurityDiff_Click(object s, RoutedEventArgs e)
    {
        if (_lastScan.Count == 0) _lastScan = await _lan.ScanAsync().ConfigureAwait(true);
        var baseline = _lan.LoadBaseline(BaselinePath);
        if (baseline.Count == 0) { SecurityOutput.Text = "No baseline."; return; }
        _lastLanDiff = _lan.Diff(baseline, _lastScan);
        SecurityOutput.Text = _lastLanDiff.Summary;
        LogJob("LanDiff", _lastLanDiff.NewHosts.Count > 0 ? "NEW" : "OK");
    }

    private void PlaybookShow_Click(object s, RoutedEventArgs e)
    {
        var p = PlaybookEngine.All.ElementAtOrDefault(PlaybookBox.SelectedIndex);
        PlaybookOutput.Text = p is null ? "—" : PlaybookEngine.Render(p);
    }

    private async void ReportGenerate_Click(object s, RoutedEventArgs e)
    {
        if (_lastFacts is null || _lastReport is null)
        {
            try { var t = await _diagnosis.RunAsync().ConfigureAwait(true); _lastFacts = t.Facts; _lastReport = t.Report; } catch { }
        }
        var audit = _diagnosis.Executor.Audit.Snapshot();
        _lastReportText = ReportExporter.BuildText(_lastFacts, _lastReport, _lastLanDiff, audit);
        _lastReportCsv = ReportExporter.BuildCsv(_lastFacts, _lastReport, _lastLanDiff, audit);
        ReportOutput.Text = _lastReportText;
        LogJob("Report", "OK");
    }

    private void ReportSave_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastReportText)) ReportGenerate_Click(s, e);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"NetOps-Report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        ReportExporter.WriteAll(path, _lastReportText);
        ReportOutput.Text += "\nSaved " + path;
        LogJob("ReportTXT", "OK");
    }

    private void ReportSaveCsv_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastReportCsv)) ReportGenerate_Click(s, e);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"NetOps-Report-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        ReportExporter.WriteAll(path, _lastReportCsv);
        ReportOutput.Text += "\nSaved " + path;
        LogJob("ReportCSV", "OK");
    }

    private void LogJob(string name, string result)
        => JobsText.Text = DateTime.Now.ToString("HH:mm") + "  " + name.PadRight(12) + result + "\n" + JobsText.Text;

    private sealed class VaultListItem
    {
        public VaultEntry Entry { get; }
        public string Display { get; }
        public VaultListItem(VaultEntry entry, string display) { Entry = entry; Display = display; }
        public override string ToString() => Display;
    }
}
