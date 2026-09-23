using System.IO;
using System.Windows;
using System.Windows.Controls;
using NetOps.Core.Credentials;
using NetOps.Core.Monitor;
using NetOps.Core.Scan;
using NetOps.Core.Tweaks;
using NetOps.Core.Wifi;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly LiveMonitorService _monitor = new();
    private readonly DefaultCredentialService _defaults = new();
    private readonly SubnetPortScanner _scanner = new();
    private readonly WifiInfoService _wifi = new();
    private WindowsNetworkTweaks? _tweaks;
    private bool _extrasLoaded;
    private CancellationTokenSource? _scanCts;

    private void EnsureExtras()
    {
        if (_extrasLoaded) return;
        _extrasLoaded = true;
        _tweaks = new WindowsNetworkTweaks(_diagnosis.Executor.Audit);
        _monitor.Updated += () => Dispatcher.Invoke(() =>
        {
            if (ContentMonitor.Visibility == Visibility.Visible)
                MonitorOutput.Text = _monitor.FormatSnapshot();
        });
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "defaults.v1.json");
            if (!File.Exists(path))
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "credentials", "defaults.v1.json"));
            if (File.Exists(path)) _defaults.Load(path);
        }
        catch { /* optional */ }

        TweakBox.Items.Clear();
        foreach (var t in WindowsNetworkTweaks.Catalog)
            TweakBox.Items.Add($"{t.Id} — {t.Title}");
        if (TweakBox.Items.Count > 0) TweakBox.SelectedIndex = 0;
    }

    private void ShowExtraPanels(string tag)
    {
        EnsureExtras();
        ContentMonitor.Visibility = V(tag, "Monitor");
        ContentScan.Visibility = V(tag, "Scan");
        ContentDefaults.Visibility = V(tag, "Defaults");
        ContentTweaks.Visibility = V(tag, "Tweaks");
    }

    private void MonitorStart_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        var hosts = MonitorHostsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0) { MonitorOutput.Text = "Enter hosts."; return; }
        _monitor.Start(hosts, 2000);
        MonitorOutput.Text = "Monitoring…\n" + _monitor.FormatSnapshot();
        LogJob("Monitor", "ON");
    }

    private void MonitorStop_Click(object sender, RoutedEventArgs e)
    {
        _monitor.Stop();
        MonitorOutput.Text = "Stopped.\n" + _monitor.FormatSnapshot();
        LogJob("Monitor", "OFF");
    }

    private async void ScanPorts_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        var range = ScanRangeBox.Text.Trim();
        if (string.IsNullOrEmpty(range))
        {
            ScanOutput.Text = "Enter range e.g. 192.168.1.0/24";
            return;
        }
        int[]? ports = null;
        var portText = ScanPortsBox.Text.Trim();
        if (!string.IsNullOrEmpty(portText))
        {
            ports = portText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.TryParse(p, out var n) ? n : -1)
                .Where(n => n is > 0 and < 65536)
                .ToArray();
            if (ports.Length == 0) ports = null;
        }

        if (MessageBox.Show(
                $"TCP port scan\nRange: {range}\nOnly scan networks you are authorized to test.",
                "Port scan", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        ScanOutput.Text = "Scanning…";
        try
        {
            var progress = new Progress<string>(msg =>
            {
                // light progress — avoid flooding UI
            });
            var result = await _scanner.ScanAsync(range, ports, 400, 256, 64, progress, _scanCts.Token)
                .ConfigureAwait(true);
            ScanOutput.Text = result;
            LogJob("PortScan", "OK");
        }
        catch (OperationCanceledException)
        {
            ScanOutput.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            ScanOutput.Text = "Error: " + ex.Message;
            LogJob("PortScan", "FAIL");
        }
    }

    private async void ScanPingSweep_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        var range = ScanRangeBox.Text.Trim();
        ScanOutput.Text = "Ping sweep…";
        try
        {
            var alive = await _scanner.AliveHostsAsync(range, 256, 600).ConfigureAwait(true);
            ScanOutput.Text = alive.Count == 0
                ? "No hosts answered ICMP."
                : $"Alive ({alive.Count}):\n" + string.Join("\n", alive.Select(ip => "  " + ip));
            LogJob("PingSweep", alive.Count.ToString());
        }
        catch (Exception ex)
        {
            ScanOutput.Text = ex.Message;
        }
    }

    private async void WifiIface_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        ScanOutput.Text = "Reading Wi-Fi interfaces…";
        ScanOutput.Text = await _wifi.GetInterfacesAsync().ConfigureAwait(true);
        LogJob("WifiIface", "OK");
    }

    private async void WifiNets_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        ScanOutput.Text = "Scanning Wi-Fi networks…";
        ScanOutput.Text = await _wifi.GetNetworksAsync().ConfigureAwait(true);
        LogJob("WifiNets", "OK");
    }

    private async void WifiProfiles_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        ScanOutput.Text = await _wifi.GetProfilesAsync().ConfigureAwait(true);
        LogJob("WifiProf", "OK");
    }

    private void DefaultsLookup_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        DefaultsOutput.Text = _defaults.Format(_defaults.ForBrand(DefaultsBrandBox.Text.Trim()));
        LogJob("Defaults", DefaultsBrandBox.Text.Trim());
    }

    private void DefaultsAll_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        DefaultsOutput.Text = _defaults.Format(_defaults.All);
        LogJob("Defaults", "ALL");
    }

    private async void TweakApply_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        if (TweakBox.SelectedIndex < 0 || _tweaks is null) return;
        var def = WindowsNetworkTweaks.Catalog[TweakBox.SelectedIndex];
        if (MessageBox.Show($"Apply '{def.Title}'?\n{def.Description}\nRisk: {def.Risk}",
                "Tweak", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        var result = await _tweaks.ApplyAsync(def.Id).ConfigureAwait(true);
        TweaksOutput.Text = (result.Success ? "OK\n" : "FAIL\n") + result.Message + "\n\n" +
                            result.StdOut + "\n" + result.StdErr;
        LogJob("Tweak", result.Success ? "OK" : "FAIL");
    }

    private void TweakList_Click(object sender, RoutedEventArgs e)
        => TweaksOutput.Text = WindowsNetworkTweaks.CatalogText();

    private void Window_Closed(object? sender, EventArgs e)
    {
        _monitor.Dispose();
        _scanCts?.Cancel();
    }
}
