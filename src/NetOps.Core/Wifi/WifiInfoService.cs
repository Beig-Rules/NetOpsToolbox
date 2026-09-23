using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NetOps.Core.Wifi;

/// <summary>Windows Wi-Fi status via netsh wlan (interfaces + nearby networks).</summary>
public sealed class WifiInfoService
{
    public async Task<string> GetInterfacesAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Wi-Fi info is Windows-only (netsh wlan).";

        var (code, stdout, stderr) = await RunAsync("netsh", "wlan show interfaces", ct).ConfigureAwait(false);
        if (code != 0 && string.IsNullOrWhiteSpace(stdout))
            return "netsh failed: " + stderr;

        return FormatInterfaces(stdout);
    }

    public async Task<string> GetNetworksAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Wi-Fi scan is Windows-only.";

        var (code, stdout, stderr) = await RunAsync("netsh", "wlan show networks mode=bssid", ct).ConfigureAwait(false);
        if (code != 0 && string.IsNullOrWhiteSpace(stdout))
            return "netsh failed: " + stderr;

        return FormatNetworks(stdout);
    }

    public async Task<string> GetProfilesAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Wi-Fi profiles are Windows-only.";

        var (code, stdout, stderr) = await RunAsync("netsh", "wlan show profiles", ct).ConfigureAwait(false);
        if (code != 0 && string.IsNullOrWhiteSpace(stdout))
            return "netsh failed: " + stderr;
        return stdout.Trim();
    }

    private static string FormatInterfaces(string raw)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== WIFI INTERFACES ===");
        // Keep key lines
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0) continue;
            if (Regex.IsMatch(t, @"^(Name|Description|State|SSID|BSSID|Signal|Radio type|Authentication|Cipher|Channel|Receive rate|Transmit rate)", RegexOptions.IgnoreCase))
                sb.AppendLine(t);
        }
        if (sb.Length < 40) return raw.Trim();
        return sb.ToString();
    }

    private static string FormatNetworks(string raw)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== VISIBLE NETWORKS ===");
        string? ssid = null;
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var t = line.Trim();
            var mSsid = Regex.Match(t, @"^SSID\s+\d+\s*:\s*(.*)$", RegexOptions.IgnoreCase);
            if (mSsid.Success)
            {
                ssid = mSsid.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(ssid)) ssid = "(hidden)";
                sb.AppendLine();
                sb.AppendLine("SSID: " + ssid);
                continue;
            }
            if (ssid is null) continue;
            if (Regex.IsMatch(t, @"^(Network type|Authentication|Encryption|BSSID|Signal|Radio type|Channel)", RegexOptions.IgnoreCase))
                sb.AppendLine("  " + t);
        }
        if (sb.Length < 50) return raw.Trim();
        return sb.ToString();
    }

    private static async Task<(int code, string stdout, string stderr)> RunAsync(
        string file, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(ct);
        var e = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return (p.ExitCode, await o, await e);
    }
}
