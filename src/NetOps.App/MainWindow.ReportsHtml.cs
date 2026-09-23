using System.IO;
using System.Windows;
using NetOps.Core.Reports;

namespace NetOps.App;

public partial class MainWindow
{
    private string _lastReportHtml = "";

    private void ReportSaveHtml_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_lastReportHtml))
            {
                // regenerate if generate was skipped
                _lastReportHtml = ReportExporter.BuildHtml(_lastFacts, _lastReport, _lastLanDiff, null);
            }
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NetOpsToolbox", "reports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.html");
            ReportExporter.WriteAll(path, _lastReportHtml);
            ReportOutput.Text = (_lastReportText.Length > 0 ? _lastReportText + "\n\n" : "") +
                                "HTML saved: " + path;
            LogJob("ReportHtml", "OK");
        }
        catch (Exception ex)
        {
            ReportOutput.Text = "HTML save failed: " + ex.Message;
            LogJob("ReportHtml", "FAIL");
        }
    }
}
