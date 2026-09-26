using System.Text;
using System.Windows;

namespace NetOps.App;

/// <summary>
/// Only allow-listed actions are executable from diagnosis.
/// Advice-only ActionIds are shown but never silently faked as success.
/// </summary>
public partial class MainWindow
{
    private static readonly HashSet<string> RunnableActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "FlushDns",
        "RenewDhcp",
        "SetAdapterDnsPublic"
    };

    private async void ApplyTopFix_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReport is null || _lastReport.RankedSolutions.Count == 0)
        {
            MessageBox.Show("Run diagnosis first.", "Apply top fix");
            return;
        }

        foreach (var sol in _lastReport.RankedSolutions)
        {
            var id = sol.ActionIds.FirstOrDefault(a => RunnableActions.Contains(a));
            if (id is null) continue;

            if (id == "SetAdapterDnsPublic")
            {
                EnsureDiagnoseNicPicker();
                var nic = SelectedDnsNicName();
                _diagnosis.Executor.TargetInterfaceName = nic;
                await RunAction(id, $"{sol.Title}\n\nSet public DNS on '{nic}'?").ConfigureAwait(true);
            }
            else
            {
                await RunAction(id, $"{sol.Title}\n\nExecute {id}?").ConfigureAwait(true);
            }
            return;
        }

        MessageBox.Show(
            "Top solutions are advice-only (no safe automatic action).\n" +
            "Use Flush DNS / Renew DHCP / Set DNS buttons, or fix CPE manually.",
            "Apply top fix");
    }

    /// <summary>Called after RunDiagnose to annotate runnable vs advice.</summary>
    private static string FormatSolutionsAnnotated(NetOps.Core.Diagnosis.DiagnosisReport report)
    {
        var sb = new StringBuilder();
        foreach (var s in report.RankedSolutions)
        {
            var runnable = s.ActionIds.Where(a => RunnableActions.Contains(a)).ToList();
            var tag = runnable.Count > 0 ? "RUN:" + string.Join(",", runnable) : "advice";
            sb.AppendLine($"#{s.Score} [{s.Risk}] ({tag}) {s.Title}");
            if (!string.IsNullOrWhiteSpace(s.Description))
                sb.AppendLine("    " + s.Description);
        }
        return sb.ToString();
    }
}
