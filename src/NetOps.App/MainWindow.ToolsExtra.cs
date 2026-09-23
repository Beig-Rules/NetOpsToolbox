using System.Windows;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly PublicIpService _publicIp = new();
    private readonly ArpTableService _arp = new();

    private async void ToolPublicIp_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Fetching public IP…";
        try
        {
            ToolsOutput.Text = await _publicIp.RunAsync().ConfigureAwait(true);
            LogJob("PublicIP", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("PublicIP", "FAIL");
        }
    }

    private async void ToolArp_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Reading ARP table…";
        try
        {
            ToolsOutput.Text = await _arp.RunAsync().ConfigureAwait(true);
            LogJob("ARP", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("ARP", "FAIL");
        }
    }
}
