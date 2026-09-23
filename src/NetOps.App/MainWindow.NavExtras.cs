using System.Windows;
using System.Windows.Controls;
using NetOps.Core.Credentials;
using NetOps.Core.Monitor;
using NetOps.Core.Scan;
using NetOps.Core.Tweaks;
using NetOps.Core.Wifi;

namespace NetOps.App;

/// <summary>Extra panels + tool wiring (modular partial).</summary>
public partial class MainWindow
{
    private readonly LiveMonitorService _monitor = new();
    private readonly SubnetPortScanner _scanner = new();
    private readonly WifiInfoService _wifi = new();
    private readonly DefaultCredentialService _defaults = new();
    private readonly WindowsNetworkTweaks _tweaks = new();

    private void ShowExtraPanels(string tag)
    {
        ContentMonitor.Visibility = tag == "Monitor" ? Visibility.Visible : Visibility.Collapsed;
        ContentScan.Visibility = tag == "Scan" ? Visibility.Visible : Visibility.Collapsed;
        ContentDefaults.Visibility = tag == "Defaults" ? Visibility.Visible : Visibility.Collapsed;
        ContentTweaks.Visibility = tag == "Tweaks" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "Tools")
            EnsureExtraToolButtons();

        if (tag == "Tweaks" && TweakBox.Items.Count == 0)
        {
            foreach (var t in WindowsNetworkTweaks.List())
                TweakBox.Items.Add(t);
            if (TweakBox.Items.Count > 0) TweakBox.SelectedIndex = 0;
        }
    }

    private async void MonitorStart_Click(object s, RoutedEventArgs e)
    {
        var hosts = MonitorHostsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        MonitorOutput.Text = "Starting…";
        await _monitor.StartAsync(hosts, line => Dispatcher.Invoke(() =>
        {
            MonitorOutput.Text = line + "\n" + MonitorOutput.Text;
            if (MonitorOutput.Text.Length > 20000)
                MonitorOutput.Text = MonitorOutput.Text[..15000];
        })).ConfigureAwait(true);
        LogJob("Monitor", "ON");
    }

    private void MonitorStop_Click(object s, RoutedEventArgs e)
    {
        _monitor.Stop();
        MonitorOutput.Text = "Stopped.\n" + MonitorOutput.Text;
        LogJob("Monitor", "OFF");
    }

    private void Window_Closed(object? s, EventArgs e) => _monitor.Stop();

    private async void ScanPorts_Click(object s, RoutedEventArgs e)
    {
        ScanOutput.Text = "Scanning…";
        var ports = ScanPortsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x.Trim(), out var p) ? p : -1).Where(p => p > 0).ToArray();
        ScanOutput.Text = await _scanner.ScanPortsAsync(ScanRangeBox.Text.Trim(), ports).ConfigureAwait(true);
        LogJob("PortScan", "OK");
    }

    private async void ScanPingSweep_Click(object s, RoutedEventArgs e)
    {
        ScanOutput.Text = "Ping sweep…";
        ScanOutput.Text = await _scanner.PingSweepAsync(ScanRangeBox.Text.Trim()).ConfigureAwait(true);
        LogJob("PingSweep", "OK");
    }

    private async void WifiIface_Click(object s, RoutedEventArgs e)
    {
        ScanOutput.Text = await _wifi.InterfaceAsync().ConfigureAwait(true);
        LogJob("WifiIf", "OK");
    }

    private async void WifiNets_Click(object s, RoutedEventArgs e)
    {
        ScanOutput.Text = await _wifi.NetworksAsync().ConfigureAwait(true);
        LogJob("WifiNet", "OK");
    }

    private async void WifiProfiles_Click(object s, RoutedEventArgs e)
    {
        ScanOutput.Text = await _wifi.ProfilesAsync().ConfigureAwait(true);
        LogJob("WifiProf", "OK");
    }

    private void DefaultsLookup_Click(object s, RoutedEventArgs e)
    {
        DefaultsOutput.Text = _defaults.Lookup(DefaultsBrandBox.Text.Trim());
        LogJob("Defaults", "OK");
    }

    private void DefaultsAll_Click(object s, RoutedEventArgs e)
    {
        DefaultsOutput.Text = _defaults.All();
        LogJob("DefaultsAll", "OK");
    }

    private void TweakList_Click(object s, RoutedEventArgs e)
    {
        TweaksOutput.Text = string.Join("\n", WindowsNetworkTweaks.List());
    }

    private async void TweakApply_Click(object s, RoutedEventArgs e)
    {
        var id = TweakBox.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(id)) { TweaksOutput.Text = "Select tweak."; return; }
        if (MessageBox.Show("Apply " + id + "?", "Tweaks", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        TweaksOutput.Text = await _tweaks.ApplyAsync(id).ConfigureAwait(true);
        LogJob("Tweak", id);
    }
}
