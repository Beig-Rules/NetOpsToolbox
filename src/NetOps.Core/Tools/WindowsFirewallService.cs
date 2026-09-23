using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Read-only Windows Firewall profiles and state via netsh.</summary>
public sealed class WindowsFirewallService
{
    public async Task<string> RunAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== WINDOWS FIREWALL (read-only) ===");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            sb.AppendLine("Windows only.");
            return sb.ToString();
        }

        try
        {
            var (c1, o1, e1) = await RunAsync("netsh", "advfirewall show allprofiles", ct).ConfigureAwait(false);
            sb.AppendLine(o1.TrimEnd());
            if (!string.IsNullOrWhiteSpace(e1)) sb.AppendLine(e1.Trim());
            if (c1 != 0) sb.AppendLine("exit " + c1);

            sb.AppendLine();
            sb.AppendLine("--- State ---");
            var (c2, o2, e2) = await RunAsync("netsh", "advfirewall show currentprofile", ct).ConfigureAwait(false);
            sb.AppendLine(o2.TrimEnd());
            if (!string.IsNullOrWhiteSpace(e2)) sb.AppendLine(e2.Trim());

            sb.AppendLine();
            sb.AppendLine("OK — no rules modified. Use Windows Security for changes.");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }

        return sb.ToString();
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
