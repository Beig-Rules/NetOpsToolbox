using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Devices;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly JuniperSshService _junos = new();
    private readonly ArubaSshService _aruba = new();
    private readonly FortinetSshService _forti = new();
    private bool _deviceExtrasWired;

    private void EnsureDeviceExtraButtons()
    {
        if (_deviceExtrasWired) return;
        try
        {
            var panel = FindDevicesWrapPanel(ContentDevices);
            if (panel is null) return;

            void Add(string title, RoutedEventHandler handler)
            {
                if (panel.Children.OfType<Button>().Any(b => (b.Content as string) == title))
                    return;
                var btn = new Button
                {
                    Content = title,
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(0, 0, 4, 4),
                    BorderThickness = new Thickness(1),
                    Background = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btn.SetResourceReference(Border.BorderBrushProperty, "TextPrimary");
                btn.Click += handler;
                panel.Children.Add(btn);
            }

            Add("Junos Ver", JunosVersion_Click);
            Add("Junos Cfg", JunosConfig_Click);
            Add("Aruba Ver", ArubaVersion_Click);
            Add("Aruba Run", ArubaShowRun_Click);
            Add("Forti Status", FortiStatus_Click);
            Add("Forti Cfg", FortiConfig_Click);
            _deviceExtrasWired = true;
        }
        catch { }
    }

    private static WrapPanel? FindDevicesWrapPanel(DependencyObject? root)
    {
        if (root is null) return null;
        if (root is WrapPanel wp) return wp;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindDevicesWrapPanel(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        if (root is Panel p)
        {
            foreach (var c in p.Children)
            {
                if (c is WrapPanel w) return w;
                if (c is DependencyObject d)
                {
                    var f = FindDevicesWrapPanel(d);
                    if (f is not null) return f;
                }
            }
        }
        return null;
    }

    private (string host, string user, string pass, string? enable, int port) DeviceCreds()
    {
        var host = IpBox.Text.Trim();
        var user = SshUserBox.Text.Trim();
        var pass = SshPassBox.Password;
        var enable = string.IsNullOrEmpty(EnablePassBox.Password) ? null : EnablePassBox.Password;
        var port = int.TryParse(SshPortBox.Text.Trim(), out var pr) ? pr : 22;
        return (host, user, pass, enable, port);
    }

    private string BackupDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetOpsToolbox", "backups");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async void JunosVersion_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, _, port) = DeviceCreds();
        DeviceSshOutput.Text = "Junos version…";
        try
        {
            var r = await _junos.ShowVersionAsync(host, user, pass, port).ConfigureAwait(true);
            DeviceSshOutput.Text = (r.Success ? "OK\n" : "FAIL\n") + r.Message + "\n" + (r.Preview ?? "");
            LogJob("JunosVer", r.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private async void JunosConfig_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, _, port) = DeviceCreds();
        if (MessageBox.Show($"Junos show configuration {host}?", "Confirm", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            return;
        DeviceSshOutput.Text = "Junos config…";
        try
        {
            var r = await _junos.ShowConfigAsync(host, user, pass, BackupDir(), port).ConfigureAwait(true);
            DeviceSshOutput.Text = (r.Success ? "OK\n" : "FAIL\n") + r.Message + "\n" + (r.Preview ?? "");
            LogJob("JunosCfg", r.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private async void ArubaVersion_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, enable, port) = DeviceCreds();
        DeviceSshOutput.Text = "Aruba version…";
        try
        {
            var r = await _aruba.ShowVersionAsync(host, user, pass, port, enable).ConfigureAwait(true);
            DeviceSshOutput.Text = (r.Success ? "OK\n" : "FAIL\n") + r.Message + "\n" + (r.Preview ?? "");
            LogJob("ArubaVer", r.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private async void ArubaShowRun_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, enable, port) = DeviceCreds();
        if (MessageBox.Show($"Aruba show running-config {host}?", "Confirm", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            return;
        DeviceSshOutput.Text = "Aruba run…";
        try
        {
            var r = await _aruba.ShowRunningConfigAsync(host, user, pass, BackupDir(), port, enable).ConfigureAwait(true);
            DeviceSshOutput.Text = (r.Success ? "OK\n" : "FAIL\n") + r.Message + "\n" + (r.Preview ?? "");
            LogJob("ArubaRun", r.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private async void FortiStatus_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, _, port) = DeviceCreds();
        DeviceSshOutput.Text = "FortiGate status…";
        try
        {
            var r = await _forti.GetSystemStatusAsync(host, user, pass, port).ConfigureAwait(true);
            DeviceSshOutput.Text = (r.Success ? "OK\n" : "FAIL\n") + r.Message + "\n" + (r.Preview ?? "");
            LogJob("FortiStatus", r.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }

    private async void FortiConfig_Click(object sender, RoutedEventArgs e)
    {
        var (host, user, pass, _, port) = DeviceCreds();
        if (MessageBox.Show($"FortiGate show full-configuration {host}? Large output.", "Confirm", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            return;
        DeviceSshOutput.Text = "FortiGate config…";
        try
        {
            var r = await _forti.ShowFullConfigAsync(host, user, pass, BackupDir(), port).ConfigureAwait(true);
            DeviceSshOutput.Text = (r.Success ? "OK\n" : "FAIL\n") + r.Message + "\n" + (r.Preview ?? "");
            LogJob("FortiCfg", r.Success ? "OK" : "FAIL");
        }
        catch (Exception ex) { DeviceSshOutput.Text = ex.Message; }
    }
}
