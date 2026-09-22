using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using NetOps.Core;
using NetOps.Core.Models;

namespace NetOps.App;

public partial class MainWindow : Window
{
    private BrandCatalog? _catalog;
    private readonly DiagnosisService _diagnosis = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadCatalog();
        JobsText.Text = "10:02  Backup     edge-mt  OK\n09:40  Ping sweep lan      OK";
    }

    private void LoadCatalog()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "catalog.v1.json");
            if (!File.Exists(path))
            {
                path = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "brands", "catalog.v1.json"));
            }

            _catalog = CatalogService.LoadFromFile(path);
            BrandBox.Items.Clear();
            foreach (var b in _catalog.Brands)
                BrandBox.Items.Add(b.Name);

            if (BrandBox.Items.Count > 0)
            {
                BrandBox.SelectedIndex = 0;
                BrandBox.SelectionChanged += (_, _) => FillModels();
                FillModels();
            }

            StatusText.Text =
                $"Catalog v{_catalog.Version} · {_catalog.Brands.Count} brands · {_catalog.Models.Count} models";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Catalog load failed: " + ex.Message;
        }
    }

    private void FillModels()
    {
        ModelBox.Items.Clear();
        if (_catalog is null || BrandBox.SelectedItem is null) return;

        var name = BrandBox.SelectedItem.ToString()!;
        var brand = _catalog.Brands.FirstOrDefault(b => b.Name == name);
        if (brand is null) return;

        foreach (var m in CatalogService.ModelsForBrand(_catalog, brand.Id))
            ModelBox.Items.Add(m.Model);

        if (ModelBox.Items.Count > 0)
            ModelBox.SelectedIndex = 0;
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;

        ContentDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ContentDevices.Visibility = tag == "Devices" ? Visibility.Visible : Visibility.Collapsed;
        ContentDiagnose.Visibility = tag == "Diagnose" ? Visibility.Visible : Visibility.Collapsed;
        ContentOther.Visibility = tag is "Dashboard" or "Devices" or "Diagnose"
            ? Visibility.Collapsed
            : Visibility.Visible;

        ContentOther.Text = tag switch
        {
            "Tools" => "Live tools: Ping · DNS · Traceroute · Port check · Subnet (Phase 1+)",
            "Firmware" => "Firmware Center: Diagnose · Download on PC · Upgrade · Downgrade",
            "Security" => "New host detection and config-change alerts.",
            "Playbooks" => "Baseline harden · NTP · DNS",
            "Reports" => "Export inventory and audit logs.",
            "Settings" => "Elevation required. Theme locked to Minimal Mono.",
            _ => tag
        };
    }

    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        var brand = BrandBox.SelectedItem?.ToString() ?? "?";
        var model = ModelBox.SelectedItem?.ToString() ?? "?";
        var ip = string.IsNullOrWhiteSpace(IpBox.Text) ? "0.0.0.0" : IpBox.Text.Trim();
        DeviceList.Items.Add($"{brand} / {model} @ {ip}");
    }

    private async void RunDiagnose_Click(object sender, RoutedEventArgs e)
    {
        RunDiagnoseBtn.IsEnabled = false;
        ApplyFlushDnsBtn.IsEnabled = false;
        DiagnoseBusy.Text = "Collecting facts…";
        DiagnoseOutput.Text = "";
        try
        {
            var (facts, report) = await _diagnosis.RunAsync().ConfigureAwait(true);
            var sb = new StringBuilder();
            sb.AppendLine(report.Headline);
            sb.AppendLine();
            sb.AppendLine("=== FACTS ===");
            sb.AppendLine($"Adapters: {facts.Adapters.Count} | Gateways: {string.Join(", ", facts.DefaultGateways)}");
            sb.AppendLine($"DNS: {string.Join(", ", facts.DnsServers)}");
            sb.AppendLine($"Proxy: {(facts.ProxyEnabled ? facts.ProxyServer ?? "on" : "off")}");
            if (facts.Registry is not null)
            {
                sb.AppendLine($"Registry host: {facts.Registry.Hostname} domain={facts.Registry.Domain}");
                if (!string.IsNullOrWhiteSpace(facts.Registry.StaticNameServer))
                    sb.AppendLine($"Registry NameServer: {facts.Registry.StaticNameServer}");
                if (!string.IsNullOrWhiteSpace(facts.Registry.SearchList))
                    sb.AppendLine($"SearchList: {facts.Registry.SearchList}");
            }
            sb.AppendLine($"Gateway reachable: {facts.Connectivity.GatewayReachable} RTT={facts.Connectivity.GatewayRttMs}ms");
            sb.AppendLine($"Public probe: {facts.Connectivity.PublicDnsReachable} | DNS name: {facts.Connectivity.NameResolutionWorks}");
            if (facts.Connectivity.ResolvedProbe is not null)
                sb.AppendLine($"Resolved: {facts.Connectivity.ResolvedProbe}");
            foreach (var n in facts.CollectorNotes)
                sb.AppendLine("note: " + n);

            sb.AppendLine();
            sb.AppendLine("=== MATCHED FLOWS ===");
            if (report.Results.Count == 0)
                sb.AppendLine("(none)");
            foreach (var r in report.Results)
            {
                sb.AppendLine($"[{r.FlowId}] {r.Title}");
                sb.AppendLine("  " + r.Summary);
                foreach (var ev in r.Evidence)
                    sb.AppendLine("  · " + ev);
            }

            sb.AppendLine();
            sb.AppendLine("=== RANKED SOLUTIONS ===");
            if (report.RankedSolutions.Count == 0)
                sb.AppendLine("(none — network looks consistent with probes)");
            foreach (var s in report.RankedSolutions)
            {
                sb.AppendLine($"#{s.Score} [{s.Risk}] {s.Title}");
                sb.AppendLine("  " + s.Description);
                sb.AppendLine("  actions: " + string.Join(", ", s.ActionIds));
            }

            sb.AppendLine();
            sb.AppendLine("Live execute available: Flush DNS (button above). Other actions remain advisory.");
            DiagnoseOutput.Text = sb.ToString();
            StatusText.Text = "Diagnosis complete · " + report.Headline;
        }
        catch (Exception ex)
        {
            DiagnoseOutput.Text = "Diagnosis failed: " + ex.Message;
            StatusText.Text = "Diagnosis error";
        }
        finally
        {
            DiagnoseBusy.Text = "";
            RunDiagnoseBtn.IsEnabled = true;
            ApplyFlushDnsBtn.IsEnabled = true;
        }
    }

    private async void ApplyFlushDns_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Run ipconfig /flushdns on this PC, then verify name resolution?\n\nThis is a low-risk local action.",
            "Confirm Flush DNS",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.OK)
            return;

        ApplyFlushDnsBtn.IsEnabled = false;
        RunDiagnoseBtn.IsEnabled = false;
        DiagnoseBusy.Text = "Executing FlushDns…";
        try
        {
            var result = await _diagnosis.ExecuteActionAsync("FlushDns").ConfigureAwait(true);
            var sb = new StringBuilder();
            sb.AppendLine(DiagnoseOutput.Text);
            sb.AppendLine();
            sb.AppendLine("=== ACTION: FlushDns ===");
            sb.AppendLine(result.Success ? "SUCCESS" : "FAILED");
            if (result.Skipped) sb.AppendLine("(skipped)");
            sb.AppendLine(result.Message);
            if (!string.IsNullOrWhiteSpace(result.StdOut)) sb.AppendLine("stdout: " + result.StdOut);
            if (!string.IsNullOrWhiteSpace(result.StdErr)) sb.AppendLine("stderr: " + result.StdErr);
            if (result.VerifyOk is not null)
                sb.AppendLine($"verify: {(result.VerifyOk == true ? "OK" : "FAIL")} — {result.VerifyDetail}");

            sb.AppendLine();
            sb.AppendLine("=== AUDIT ===");
            foreach (var a in _diagnosis.Executor.Audit.Snapshot().TakeLast(5))
                sb.AppendLine($"{a.At:HH:mm:ss} {a.ActionId} {a.Outcome} {a.Detail}");

            DiagnoseOutput.Text = sb.ToString();
            StatusText.Text = result.Success
                ? "FlushDns applied · verify=" + (result.VerifyOk == true ? "OK" : "check")
                : "FlushDns failed";
            JobsText.Text = DateTime.Now.ToString("HH:mm") + "  FlushDns   localhost  " +
                            (result.Success ? "OK" : "FAIL") + "\n" + JobsText.Text;
        }
        catch (Exception ex)
        {
            DiagnoseOutput.Text += "\n\nAction error: " + ex.Message;
            StatusText.Text = "Action error";
        }
        finally
        {
            DiagnoseBusy.Text = "";
            ApplyFlushDnsBtn.IsEnabled = true;
            RunDiagnoseBtn.IsEnabled = true;
        }
    }
}
