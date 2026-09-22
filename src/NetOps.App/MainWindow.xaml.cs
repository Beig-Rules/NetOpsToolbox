using System.IO;
using System.Windows;
using System.Windows.Controls;
using NetOps.Core;
using NetOps.Core.Models;

namespace NetOps.App;

public partial class MainWindow : Window
{
    private BrandCatalog? _catalog;

    public MainWindow()
    {
        Resources["NavButton"] = CreateNavStyle();
        InitializeComponent();
        LoadCatalog();
        JobsText.Text = "10:02  Backup     edge-mt  OK\n09:40  Ping sweep lan      OK";
    }

    private static Style CreateNavStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B6B6B")!)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 16.0));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0, 6, 0, 6)));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
        return style;
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
            foreach (var b in _catalog.Brands)
                BrandBox.Items.Add(b.Name);
            if (BrandBox.Items.Count > 0)
            {
                BrandBox.SelectedIndex = 0;
                BrandBox.SelectionChanged += (_, _) => FillModels();
                FillModels();
            }
            StatusText.Text = $"Catalog v{_catalog.Version} · {_catalog.Brands.Count} brands · {_catalog.Models.Count} models";
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
        ContentOther.Visibility = tag is "Dashboard" or "Devices" ? Visibility.Collapsed : Visibility.Visible;
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
}
