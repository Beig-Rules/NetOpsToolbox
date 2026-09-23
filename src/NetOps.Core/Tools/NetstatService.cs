using System.Diagnostics;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Listening / established connections via netstat -ano (read-only).</summary>
public sealed class NetstatService
{
    public async Task<string> RunAsync(bool listeningOnly = false, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(listeningOnly ? "=== NETSTAT LISTENING (-ano) ===" : "=== NETSTAT (-ano) ===");
        try
        {
            var args = listeningOnly ? "-ano -p tcp" : "-ano";
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start netstat");
            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            if (listeningOnly && !string.IsNullOrEmpty(stdout))
            {
                var lines = stdout.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Proto", StringComparison.Ordinal) ||
                        line.StartsWith("Active", StringComparison.Ordinal))
                        sb.AppendLine(line.TrimEnd('\r'));
                }
            }
            else if (!string.IsNullOrWhiteSpace(stdout))
            {
                // Cap output size for UI
                var trimmed = stdout.Length > 120_000 ? stdout[..120_000] + "\n…(truncated)" : stdout;
                sb.AppendLine(trimmed.TrimEnd());
            }

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
