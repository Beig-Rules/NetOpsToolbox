using System.IO;
using System.Windows;
using System.Windows.Controls;
using NetOps.Core.Credentials;
using NetOps.Core.Monitor;
using NetOps.Core.Tweaks;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly LiveMonitorService _monitor = new();
    private readonly DefaultCredentialService _defaults = new();
    private readonly WindowsNetworkTweaks _tweaks;
    private bool _extrasLoaded;

    private void EnsureExtras()
    {
        if (_extrasLoaded) return;
        _extrasLoaded = true;
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
        catch { }

        foreach (var t in WindowsNetworkTweaks.Catalog)
            TweakBox.Items.Add($"{t.Id} — {t.Title}");
        if (TweakBox.Items.Count > 0) TweakBox.SelectedIndex = 0;
    }

    // Called from constructor via partial — wire in Nav and Closed

    private void ShowExtraPanels(string tag)
    {
        EnsureExtras();
        ContentMonitor.Visibility = V(tag, "Monitor");
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

    private void DefaultsLookup_Click(object sender, RoutedEventArgs e)
    {
        EnsureExtras();
        var brand = DefaultsBrandBox.Text.Trim();
        DefaultsOutput.Text = _defaults.Format(_defaults.ForBrand(brand));
        LogJob("Defaults", brand);
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
        if (TweakBox.SelectedIndex < 0) return;
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
    {
        TweaksOutput.Text = WindowsNetworkTweaks.CatalogText();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _monitor.Dispose();
    }
}
