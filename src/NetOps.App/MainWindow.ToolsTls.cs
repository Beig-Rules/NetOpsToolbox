using System.Windows;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly TlsProbeService _tls = new();

    private async void ToolTls_Click(object sender, RoutedEventArgs e)
    {
        var raw = ToolsInput.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        var host = raw;
        var port = 443;
        if (raw.Contains(':'))
        {
            var p = raw.Split(':');
            host = p[0];
            if (p.Length > 1 && int.TryParse(p[1], out var pr)) port = pr;
        }
        ToolsOutput.Text = $"TLS probe {host}:{port}…";
        try
        {
            ToolsOutput.Text = await _tls.RunAsync(host, port).ConfigureAwait(true);
            LogJob("TLS", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("TLS", "FAIL");
        }
    }
}
