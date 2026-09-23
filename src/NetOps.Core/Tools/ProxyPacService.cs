using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace NetOps.Core.Tools;

/// <summary>Read-only Windows user/system proxy and PAC URL summary.</summary>
public sealed class ProxyPacService
{
    public string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PROXY / PAC (read-only) ===");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            sb.AppendLine("Windows only.");
            return sb.ToString();
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
            if (key is null)
            {
                sb.AppendLine("Internet Settings key not found.");
                return sb.ToString();
            }

            var enable = key.GetValue("ProxyEnable");
            var server = key.GetValue("ProxyServer") as string;
            var override_ = key.GetValue("ProxyOverride") as string;
            var pac = key.GetValue("AutoConfigURL") as string;
            var autoDetect = key.GetValue("AutoDetect");

            sb.AppendLine("User Internet Settings:");
            sb.AppendLine("  ProxyEnable:   " + (enable?.ToString() ?? "—"));
            sb.AppendLine("  ProxyServer:   " + (string.IsNullOrWhiteSpace(server) ? "—" : server));
            sb.AppendLine("  ProxyOverride: " + (string.IsNullOrWhiteSpace(override_) ? "—" : override_));
            sb.AppendLine("  AutoConfigURL: " + (string.IsNullOrWhiteSpace(pac) ? "—" : pac));
            sb.AppendLine("  AutoDetect:    " + (autoDetect?.ToString() ?? "—"));
            sb.AppendLine();

            if (enable is int i && i == 1)
                sb.AppendLine("Status: manual proxy ON");
            else if (!string.IsNullOrWhiteSpace(pac))
                sb.AppendLine("Status: PAC / auto-config URL present");
            else
                sb.AppendLine("Status: no user manual proxy (may still use WinHTTP/machine policy)");

            sb.AppendLine();
            sb.AppendLine("WinHTTP (netsh winhttp show proxy):");
            try
            {
                var (code, stdout, stderr) = RunProcess("netsh", "winhttp show proxy");
                if (!string.IsNullOrWhiteSpace(stdout)) sb.AppendLine(stdout.TrimEnd());
                if (!string.IsNullOrWhiteSpace(stderr)) sb.AppendLine(stderr.Trim());
                if (code != 0) sb.AppendLine("winhttp exit " + code);
            }
            catch (Exception ex)
            {
                sb.AppendLine("winhttp probe failed: " + ex.Message);
            }

            sb.AppendLine();
            sb.AppendLine("OK (read-only — no settings changed)");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }

        return sb.ToString();
    }

    private static (int code, string stdout, string stderr) RunProcess(string file, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start " + file);
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(8000);
        return (p.ExitCode, stdout, stderr);
    }
}
