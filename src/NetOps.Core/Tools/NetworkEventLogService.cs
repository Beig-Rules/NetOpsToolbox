using System.Diagnostics;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>
/// Read recent Windows network-related events (Application/System) via wevtutil.
/// Read-only; does not clear logs.
/// </summary>
public sealed class NetworkEventLogService
{
    public async Task<string> RunAsync(int maxEvents = 40, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== NETWORK EVENT LOG (recent) ===");
        sb.AppendLine("Sources: System (Tcpip, Netwtw, Dhcp-Client, DNS-Client, Ndis)");
        sb.AppendLine();

        // XPath-ish filter via wevtutil; keep bounded for UI
        var query =
            "*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= 86400000]]]";

        try
        {
            var args =
                $"qe System /q:\"{query}\" /c:{Math.Clamp(maxEvents, 5, 100)} /f:text /rd:true";
            var (code, stdout, stderr) = await RunAsync("wevtutil", args, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                var filtered = FilterNetworkLines(stdout);
                sb.AppendLine(filtered.Length > 0 ? filtered : "(no matching network lines in last 24h sample)");
            }
            else
                sb.AppendLine("(empty query result)");

            if (!string.IsNullOrWhiteSpace(stderr))
                sb.AppendLine("ERR: " + stderr.Trim());

            sb.AppendLine();
            sb.AppendLine(code == 0 ? "OK" : "Exit: " + code);
            sb.AppendLine("Tip: Event Viewer → Windows Logs → System for full detail.");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
            sb.AppendLine("Requires wevtutil (Windows) and permission to read System log.");
        }

        return sb.ToString();
    }

    private static string FilterNetworkLines(string raw)
    {
        var keys = new[]
        {
            "Tcpip", "TCPIP", "Netwtw", "DHCP", "DnsClient", "DNS Client",
            "Ndis", "Network", "WLAN", "Wi-Fi", "Winsock", "e1dexpress", "rt640x64"
        };
        var blocks = raw.Split(new[] { "Event[" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        var kept = 0;
        foreach (var block in blocks)
        {
            if (block.Length < 20) continue;
            if (!keys.Any(k => block.Contains(k, StringComparison.OrdinalIgnoreCase)))
                continue;
            sb.AppendLine("Event[" + block.Trim());
            sb.AppendLine(new string('-', 40));
            kept++;
            if (kept >= 25) break;
        }
        return sb.ToString().Trim();
    }

    private static async Task<(int code, string stdout, string stderr)> RunAsync(
        string file, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start " + file);
        var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return (p.ExitCode, stdout, stderr);
    }
}
