using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using NetOps.Core;
using NetOps.Core.Models;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow : Window
{
    private BrandCatalog? _catalog;
    private readonly DiagnosisService _diagnosis = new();
    private readonly NetworkTools _tools = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadCatalog();
        JobsText.Text = "—";
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
        ContentTools.Visibility = tag == "Tools" ? Visibility.Visible : Visibility.Collapsed;
        ContentOther.Visibility = tag is "Dashboard" or "Devices" or "Diagnose" or "Tools"
            ? Visibility.Collapsed : Visibility.Visible;

        ContentOther.Text = tag switch
        {
            "Firmware" => "Firmware Center: catalog compare · download on PC · upgrade/downgrade (next).",
            "Security" => "New host detection and config-change alerts (next).",
            "Playbooks" => "Baseline harden · NTP · DNS templates (next).",
            "Reports" => "Export inventory and audit logs (next).",
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
        SetDiagnoseBusy(true, "Collecting facts…");
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
            }
            sb.AppendLine($"Gateway reachable: {facts.Connectivity.GatewayReachable} RTT={facts.Connectivity.GatewayRttMs}ms");
            sb.AppendLine($"Public probe: {facts.Connectivity.PublicDnsReachable} | DNS name: {facts.Connectivity.NameResolutionWorks}");
            foreach (var n in facts.CollectorNotes)
                sb.AppendLine("note: " + n);

            sb.AppendLine();
            sb.AppendLine("=== MATCHED FLOWS ===");
            if (report.Results.Count == 0) sb.AppendLine("(none)");
            foreach (var r in report.Results)
            {
                sb.AppendLine($"[{r.FlowId}] {r.Title}");
                sb.AppendLine("  " + r.Summary);
                foreach (var ev in r.Evidence) sb.AppendLine("  · " + ev);
            }

            sb.AppendLine();
            sb.AppendLine("=== RANKED SOLUTIONS ===");
            if (report.RankedSolutions.Count == 0)
                sb.AppendLine("(none)");
            foreach (var s in report.RankedSolutions)
            {
                sb.AppendLine($"#{s.Score} [{s.Risk}] {s.Title}");
                sb.AppendLine("  " + s.Description);
                sb.AppendLine("  actions: " + string.Join(", ", s.ActionIds));
            }

            sb.AppendLine();
            sb.AppendLine("Live Apply: Flush DNS · Renew DHCP");
            DiagnoseOutput.Text = sb.ToString();
            StatusText.Text = "Diagnosis complete · " + report.Headline;
        }
        catch (Exception ex)
        {
            DiagnoseOutput.Text = "Diagnosis failed: " + ex.Message;
        }
        finally { SetDiagnoseBusy(false); }
    }

    private async void ApplyFlushDns_Click(object sender, RoutedEventArgs e)
        => await RunActionWithConfirmAsync("FlushDns",
            "Run ipconfig /flushdns and verify DNS resolution?").ConfigureAwait(true);

    private async void ApplyRenewDhcp_Click(object sender, RoutedEventArgs e)
        => await RunActionWithConfirmAsync("RenewDhcp",
            "Run ipconfig /release and /renew?\n\nMay briefly drop connectivity. Rollback guidance will be shown.").ConfigureAwait(true);

    private async Task RunActionWithConfirmAsync(string actionId, string prompt)
    {
        if (MessageBox.Show(prompt, "Confirm " + actionId, MessageBoxButton.OKCancel, MessageBoxImage.Question)
            != MessageBoxResult.OK)
            return;

        SetDiagnoseBusy(true, "Executing " + actionId + "…");
        try
        {
            var result = await _diagnosis.ExecuteActionAsync(actionId).ConfigureAwait(true);
            var sb = new StringBuilder();
            sb.AppendLine(DiagnoseOutput.Text);
            sb.AppendLine();
            sb.AppendLine("=== ACTION: " + actionId + " ===");
            sb.AppendLine(result.Success ? "SUCCESS" : "FAILED");
            if (result.Skipped) sb.AppendLine("(skipped/advisory)");
            sb.AppendLine(result.Message);
            if (!string.IsNullOrWhiteSpace(result.StdOut)) sb.AppendLine("stdout: " + result.StdOut);
            if (!string.IsNullOrWhiteSpace(result.StdErr)) sb.AppendLine("stderr: " + result.StdErr);
            if (result.VerifyOk is not null)
                sb.AppendLine($"verify: {(result.VerifyOk == true ? "OK" : "FAIL")} — {result.VerifyDetail}");
            sb.AppendLine();
            sb.AppendLine("=== AUDIT (last) ===");
            foreach (var a in _diagnosis.Executor.Audit.Snapshot().TakeLast(8))
                sb.AppendLine($"{a.At:HH:mm:ss} {a.ActionId} {a.Outcome}");

            DiagnoseOutput.Text = sb.ToString();
            LogJob(actionId, result.Success ? "OK" : "FAIL");
            StatusText.Text = actionId + (result.Success ? " applied" : " failed");
        }
        catch (Exception ex)
        {
            DiagnoseOutput.Text += "\n\nAction error: " + ex.Message;
        }
        finally { SetDiagnoseBusy(false); }
    }

    private void SetDiagnoseBusy(bool busy, string? msg = null)
    {
        DiagnoseBusy.Text = msg ?? "";
        RunDiagnoseBtn.IsEnabled = !busy;
    }

    private void LogJob(string name, string result)
    {
        JobsText.Text = DateTime.Now.ToString("HH:mm") + "  " + name.PadRight(12) + "  " + result + "\n" + JobsText.Text;
    }

    private async void ToolPing_Click(object sender, RoutedEventArgs e)
    {
        var hosts = ToolsInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0) { ToolsOutput.Text = "Enter hosts separated by commas."; return; }
        ToolsOutput.Text = "Pinging…";
        try
        {
            var results = await _tools.PingManyAsync(hosts, count: 4).ConfigureAwait(true);
            var sb = new StringBuilder();
            foreach (var r in results)
                sb.AppendLine($"{r.Host}: {(r.Success ? "OK" : "FAIL")}  {r.Status}");
            ToolsOutput.Text = sb.ToString();
            LogJob("Ping", results.Any(x => x.Success) ? "OK" : "FAIL");
        }
        catch (Exception ex) { ToolsOutput.Text = ex.Message; }
    }

    private async void ToolDns_Click(object sender, RoutedEventArgs e)
    {
        var name = ToolsInput.Text.Split(',')[0].Trim();
        if (name.Length == 0) { ToolsOutput.Text = "Enter a hostname."; return; }
        ToolsOutput.Text = "Looking up…";
        ToolsOutput.Text = await _tools.DnsLookupAsync(name).ConfigureAwait(true);
        LogJob("DNS", "OK");
    }

    private async void ToolPort_Click(object sender, RoutedEventArgs e)
    {
        var raw = ToolsInput.Text.Trim();
        var parts = raw.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        {
            ToolsOutput.Text = "Usage: host:port  e.g. 1.1.1.1:443";
            return;
        }
        ToolsOutput.Text = "Checking…";
        ToolsOutput.Text = await _tools.PortCheckAsync(parts[0], port).ConfigureAwait(true);
        LogJob("Port", "OK");
    }

    private async void ToolTrace_Click(object sender, RoutedEventArgs e)
    {
        var host = ToolsInput.Text.Split(',')[0].Trim();
        if (host.Length == 0) { ToolsOutput.Text = "Enter a host."; return; }
        ToolsOutput.Text = "Tracing (may take a while)…";
        ToolsOutput.Text = await _tools.TracerouteAsync(host).ConfigureAwait(true);
        LogJob("Trace", "OK");
    }

    private void ToolSubnet_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = NetworkTools.SubnetCalculate(ToolsInput.Text);
        LogJob("Subnet", "OK");
    }
}
