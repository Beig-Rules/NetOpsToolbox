using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetOps.Core.Tools;

namespace NetOps.App;

public partial class MainWindow
{
    private readonly CertificateStoreService _certs = new();
    private bool _certBtnWired;

    private void EnsureCertButton()
    {
        if (_certBtnWired) return;
        try
        {
            var panel = FindToolsWrapPanel(ContentTools);
            if (panel is null) return;
            if (panel.Children.OfType<Button>().Any(b => (b.Content as string) == "Certs"))
            {
                _certBtnWired = true;
                return;
            }
            var btn = new Button
            {
                Content = "Certs",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(Border.BorderBrushProperty, "TextPrimary");
            btn.Click += ToolCerts_Click;
            panel.Children.Add(btn);
            _certBtnWired = true;
        }
        catch { }
    }

    private void ToolCerts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ToolsOutput.Text = "Reading certificate stores…";
            ToolsOutput.Text = _certs.Run();
            LogJob("Certs", "OK");
        }
        catch (Exception ex)
        {
            ToolsOutput.Text = "Error: " + ex.Message;
            LogJob("Certs", "FAIL");
        }
    }
}
