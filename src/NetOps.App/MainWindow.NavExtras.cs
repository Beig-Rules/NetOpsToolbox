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
        if (tag == "Tools")
        {
            EnsureExtraToolButtons();
            EnsureEventLogButton();
            EnsureProxyButton();
            EnsureCertButton();
            EnsureNetExtraButtons();
        }
        if (tag == "Diagnose")
            EnsureDiagnoseNicPicker();
        if (tag == "Devices")
            EnsureDeviceExtraButtons();
        if (tag == "Jobs")
            EnsureJobExtraButtons();
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
        MonitorOutput.Text = "Stopped.\n" + (_monitor.FormatSnapshot());
        LogJob("Monitor", "OFF");
    }

    private async void ScanPorts_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var range = ScanRangeBox.Text.Trim();
        var ports = ScanPortsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x.Trim(), out var p) ? p : -1).Where(p => p > 0).ToArray();
        ScanOutput.Text = "Scanning…";
        try
        {
            ScanOutput.Text = await _scanner.ScanPortsAsync(range, ports, _scanCts.Token).ConfigureAwait(true);
            LogJob("PortScan", "OK");
        }
        catch (OperationCanceledException) { ScanOutput.Text = "Cancelled."; }
        catch (Exception ex) { ScanOutput.Text = ex.Message; LogJob("PortScan", "FAIL"); }
    }

    private async void ScanPingSweep_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        ScanOutput.Text = "Ping sweep…";
        try
        {
            ScanOutput.Text = await _scanner.PingSweepAsync(ScanRangeBox.Text.Trim()).ConfigureAwait(true);
            LogJob("PingSweep", "OK");
        }
        catch (Exception ex) { ScanOutput.Text = ex.Message; }
    }

    private async void WifiIface_Click(object sender, RoutedEventArgs e)
    {
        ScanOutput.Text = await _wifi.InterfaceAsync().ConfigureAwait(true);
        LogJob("WifiIf", "OK");
    }

    private async void WifiNets_Click(object sender, RoutedEventArgs e)
    {
        ScanOutput.Text = await _wifi.NetworksAsync().ConfigureAwait(true);
        LogJob("WifiNet", "OK");
    }

    private async void WifiProfiles_Click(object sender, RoutedEventArgs e)
    {
        ScanOutput.Text = await _wifi.ProfilesAsync().ConfigureAwait(true);
        LogJob("WifiProf", "OK");
    }

    private void DefaultsLookup_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        DefaultsOutput.Text = _defaults.Lookup(DefaultsBrandBox.Text.Trim());
        LogJob("Defaults", "OK");
    }

    private void DefaultsAll_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        DefaultsOutput.Text = _defaults.All();
        LogJob("DefaultsAll", "OK");
    }

    private async void TweakApply_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        var sel = TweakBox.SelectedItem?.ToString() ?? "";
        var id = sel.Split('—')[0].Trim();
        if (string.IsNullOrEmpty(id)) { TweaksOutput.Text = "Select tweak."; return; }
        if (MessageBox.Show("Apply " + id + "?", "Tweaks", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        TweaksOutput.Text = await _tweaks!.ApplyAsync(id).ConfigureAwait(true);
        LogJob("Tweak", id);
    }

    private void TweakList_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        TweaksOutput.Text = string.Join("\n", WindowsNetworkTweaks.Catalog.Select(t => t.Id + " — " + t.Title));
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _monitor.Stop();
        _scanCts?.Cancel();
    }
}
