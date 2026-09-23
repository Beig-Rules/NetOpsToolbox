using System.Windows;
using NetOps.Core.Speed;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly HttpSpeedTestService _speed = new();

    private async void ToolSpeed_Click(object sender, RoutedEventArgs e)
    {
        ToolsOutput.Text = "Running HTTP download speed test…";
        try
        {
            var result = await _speed.RunAsync().ConfigureAwait(true);
            ToolsOutput.Text = result;
            LogJob("SpeedTest", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("SpeedTest", "FAIL");
        }
    }
}
