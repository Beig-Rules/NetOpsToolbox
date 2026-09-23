using System.Windows;
using NetOps.Core.Tools;

namespace NetOps.App;

/// <summary>Modular system-read tools — Route, Netstat, Interfaces, Hosts.</summary>
public partial class MainWindow
{
    private readonly RouteTableService _routes = new();
    private readonly NetstatService _netstat = new();
    private readonly InterfaceListService _ifaces = new();
    private readonly HostsFileService _hosts = new();

    private async void ToolRoute_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Reading route table…";
        try
        {
            ToolsOutput.Text = await _routes.RunAsync().ConfigureAwait(true);
            LogJob("Route", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Route", "FAIL");
        }
    }

    private async void ToolNetstat_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Reading netstat (listening)…";
        try
        {
            ToolsOutput.Text = await _netstat.RunAsync(listeningOnly: true).ConfigureAwait(true);
            LogJob("Netstat", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Netstat", "FAIL");
        }
    }

    private void ToolIfaces_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ToolsOutput.Text = _ifaces.Run();
            LogJob("Ifaces", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Ifaces", "FAIL");
        }
    }

    private void ToolHosts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ToolsOutput.Text = _hosts.Run();
            LogJob("Hosts", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Hosts", "FAIL");
        }
    }
}
