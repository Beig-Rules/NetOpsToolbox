using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using NetOps.Core;
using NetOps.Core.Diagnosis;
using NetOps.Core.Facts;
using NetOps.Core.Firmware;
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
    private List<LanHost> _lastScan = new();
    private HostFacts? _lastFacts;
    private DiagnosisReport? _lastReport;
    private LanDiffResult? _lastLanDiff;
    private string _lastReportText = "";

    private string BaselinePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetOpsToolbox", "lan-baseline.json");

    public MainWindow()
    {
        InitializeComponent();
        LoadCatalog();
        LoadFirmwareCatalog();
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
            StatusText.Text = $"Catalog v{_catalog.Version} · {_catalog.Brands.Count} brands";
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
        catch { /* optional */ }
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
        ContentDiagnose.Visibility = V(tag, "Diagnose");
        ContentTools.Visibility = V(tag, "Tools");
        ContentFirmware.Visibility = V(tag, "Firmware");
        ContentSecurity.Visibility = V(tag, "Security");
        ContentPlaybooks.Visibility = V(tag, "Playbooks");
        ContentReports.Visibility = V(tag, "Reports");
        ContentOther.Visibility = tag is "Settings" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "Settings")
            ContentOther.Text = "Elevation required (manifest). Theme: Minimal Mono. Data under %LocalAppData%\\NetOpsToolbox";
    }

    private static Visibility V(string tag, string name) => tag == name ? Visibility.Visible : Visibility.Collapsed;

    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        DeviceList.Items.Add($"{BrandBox.SelectedItem} / {ModelBox.SelectedItem} @ {(string.IsNullOrWhiteSpace(IpBox.Text) ? "0.0.0.0" : IpBox.Text.Trim())}");
    }

    private async void RunDiagnose_Click(object sender, RoutedEventArgs e)
    {
        DiagnoseBusy.Text = "Collecting…";
        try
        {
            var (facts, report) = await _diagnosis.RunAsync().ConfigureAwait(true);
            _lastFacts = facts; _lastReport = report;
            var sb = new StringBuilder();
            sb.AppendLine(report.Headline);
            sb.AppendLine();
            sb.AppendLine("=== FACTS ===");
            sb.AppendLine($"GW: {string.Join(",", facts.DefaultGateways)} DNS: {string.Join(",", facts.DnsServers)}");
            sb.AppendLine($"Proxy: {facts.ProxyEnabled} {facts.ProxyServer}");
            sb.AppendLine($"GW ok={facts.Connectivity.GatewayReachable} public={facts.Connectivity.PublicDnsReachable} names={facts.Connectivity.NameResolutionWorks}");
            sb.AppendLine();
            sb.AppendLine("=== FLOWS ===");
            foreach (var r in report.Results)
            {
                sb.AppendLine($"[{r.FlowId}] {r.Title} — {r.Summary}");
                foreach (var ev in r.Evidence) sb.AppendLine("  · " + ev);
            }
            if (report.Results.Count == 0) sb.AppendLine("(none matched)");
            sb.AppendLine();
            sb.AppendLine("=== SOLUTIONS ===");
            foreach (var s in report.RankedSolutions)
                sb.AppendLine($"#{s.Score} [{s.Risk}] {s.Title}: {s.Description}");
            DiagnoseOutput.Text = sb.ToString();
            StatusText.Text = report.Headline;
            LogJob("Diagnose", "OK");
        }
        catch (Exception ex) { DiagnoseOutput.Text = ex.Message; }
        finally { DiagnoseBusy.Text = ""; }
    }

    private async void ApplyFlushDns_Click(object s, RoutedEventArgs e) => await RunAction("FlushDns", "Flush DNS cache?").ConfigureAwait(true);
    private async void ApplyRenewDhcp_Click(object s, RoutedEventArgs e) => await RunAction("RenewDhcp", "Release/renew DHCP? May drop briefly.").ConfigureAwait(true);

    private async void ApplySetDns_Click(object s, RoutedEventArgs e)
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.GetIPProperties().GatewayAddresses.Any());
        var name = nic?.Name ?? "";
        var msg = string.IsNullOrEmpty(name)
            ? "Set DNS 1.1.1.1 / 1.0.0.1 on primary interface?"
            : $"Set DNS 1.1.1.1 / 1.0.0.1 on interface '{name}'?\nRollback: netsh ... dhcp";
        _diagnosis.Executor.TargetInterfaceName = name;
        await RunAction("SetAdapterDnsPublic", msg).ConfigureAwait(true);
    }

    private async Task RunAction(string id, string prompt)
    {
        if (MessageBox.Show(prompt, id, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        DiagnoseBusy.Text = id + "…";
        try
        {
            var result = await _diagnosis.ExecuteActionAsync(id).ConfigureAwait(true);
            DiagnoseOutput.Text += $"\n\n=== {id} ===\n{(result.Success ? "SUCCESS" : "FAIL")}\n{result.Message}\nverify={result.VerifyOk} {result.VerifyDetail}";
            LogJob(id, result.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DiagnoseOutput.Text += "\n" + ex.Message; }
        finally { DiagnoseBusy.Text = ""; }
    }

    private async void ToolPing_Click(object s, RoutedEventArgs e)
    {
        var hosts = ToolsInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ToolsOutput.Text = "Pinging…";
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
        ToolsOutput.Text = "Tracing…";
        ToolsOutput.Text = await _tools.TracerouteAsync(ToolsInput.Text.Split(',')[0]).ConfigureAwait(true);
        LogJob("Trace", "OK");
    }

    private void ToolSubnet_Click(object s, RoutedEventArgs e)
    {
        ToolsOutput.Text = NetworkTools.SubnetCalculate(ToolsInput.Text);
        LogJob("Subnet", "OK");
    }

    private void FirmwareDiagnose_Click(object s, RoutedEventArgs e)
    {
        var brandName = BrandBox.SelectedItem?.ToString() ?? "";
        var brandId = _catalog?.Brands.FirstOrDefault(b => b.Name == brandName)?.Id ?? brandName.ToLowerInvariant();
        var model = ModelBox.SelectedItem?.ToString() ?? "";
        var ver = FwVersionBox.Text.StartsWith("(") ? null : FwVersionBox.Text.Trim();
        FirmwareOutput.Text = _fw.Diagnose(brandId, model, ver);
        LogJob("Firmware", "OK");
    }

    private async void SecurityScan_Click(object s, RoutedEventArgs e)
    {
        SecurityOutput.Text = "Scanning ARP…";
        _lastScan = await _lan.ScanAsync().ConfigureAwait(true);
        var sb = new StringBuilder();
        sb.AppendLine($"Hosts: {_lastScan.Count}");
        foreach (var h in _lastScan) sb.AppendLine($"  {h.Ip,-15} {h.Mac,-20} {h.Type}");
        SecurityOutput.Text = sb.ToString();
        LogJob("LanScan", "OK");
    }

    private void SecuritySave_Click(object s, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
        if (_lastScan.Count == 0) { SecurityOutput.Text = "Scan first."; return; }
        _lan.SaveBaseline(BaselinePath, _lastScan);
        SecurityOutput.Text += $"\nBaseline saved: {BaselinePath}";
        LogJob("Baseline", "OK");
    }

    private async void SecurityDiff_Click(object s, RoutedEventArgs e)
    {
        if (_lastScan.Count == 0) _lastScan = await _lan.ScanAsync().ConfigureAwait(true);
        var baseline = _lan.LoadBaseline(BaselinePath);
        if (baseline.Count == 0) { SecurityOutput.Text = "No baseline. Scan + Save first."; return; }
        _lastLanDiff = _lan.Diff(baseline, _lastScan);
        SecurityOutput.Text = _lastLanDiff.Summary;
        LogJob("LanDiff", _lastLanDiff.NewHosts.Count > 0 ? "NEW" : "OK");
    }

    private void PlaybookShow_Click(object s, RoutedEventArgs e)
    {
        var p = PlaybookEngine.All.ElementAtOrDefault(PlaybookBox.SelectedIndex);
        PlaybookOutput.Text = p is null ? "Select a playbook." : PlaybookEngine.Render(p);
    }

    private async void ReportGenerate_Click(object s, RoutedEventArgs e)
    {
        if (_lastFacts is null || _lastReport is null)
        {
            try { var t = await _diagnosis.RunAsync().ConfigureAwait(true); _lastFacts = t.Facts; _lastReport = t.Report; }
            catch { /* ignore */ }
        }
        _lastReportText = ReportExporter.BuildText(_lastFacts, _lastReport, _lastLanDiff, _diagnosis.Executor.Audit.Snapshot());
        ReportOutput.Text = _lastReportText;
        LogJob("Report", "OK");
    }

    private void ReportSave_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastReportText)) ReportGenerate_Click(s, e);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"NetOps-Report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        ReportExporter.WriteAll(path, _lastReportText);
        ReportOutput.Text += "\n\nSaved: " + path;
        LogJob("ReportSave", "OK");
    }

    private void LogJob(string name, string result)
        => JobsText.Text = DateTime.Now.ToString("HH:mm") + "  " + name.PadRight(12) + result + "\n" + JobsText.Text;
}
