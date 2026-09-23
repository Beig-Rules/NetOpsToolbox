using System.Diagnostics;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Windows routing table via route print (read-only).</summary>
public sealed class RouteTableService
{
    public async Task<string> RunAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ROUTE TABLE (route print) ===");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "route",
                Arguments = "print",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start route");
            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stdout)) sb.AppendLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr)) sb.AppendLine("ERR: " + stderr.Trim());
            sb.AppendLine();
            sb.AppendLine(p.ExitCode == 0 ? "OK" : "Exit: " + p.ExitCode);
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }
        return sb.ToString();
    }
}
