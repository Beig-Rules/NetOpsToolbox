using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NetOps.Core.Security;

public sealed class LanHost
{
    public string Ip { get; init; } = "";
    public string Mac { get; init; } = "";
    public string Type { get; init; } = "";
}

public sealed class LanDiffResult
{
    public List<LanHost> Current { get; init; } = new();
    public List<LanHost> NewHosts { get; init; } = new();
    public List<LanHost> MissingHosts { get; init; } = new();
    public string Summary { get; init; } = "";
}

public sealed class LanBaselineService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task<List<LanHost>> ScanAsync(CancellationToken ct = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await ScanArpWindowsAsync(ct).ConfigureAwait(false);

        // Fallback: local addresses only
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(a => new LanHost { Ip = a.Address.ToString(), Mac = "", Type = "local" })
            .ToList();
    }

    private static async Task<List<LanHost>> ScanArpWindowsAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "arp",
            Arguments = "-a",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);

        var list = new List<LanHost>();
        // 192.168.1.1           00-11-22-33-44-55     dynamic
        var rx = new Regex(
            @"(\d+\.\d+\.\d+\.\d+)\s+([0-9a-fA-F\-:]{11,})\s+(\w+)",
            RegexOptions.Compiled);
        foreach (Match m in rx.Matches(output))
        {
            list.Add(new LanHost
            {
                Ip = m.Groups[1].Value,
                Mac = m.Groups[2].Value.ToLowerInvariant(),
                Type = m.Groups[3].Value.ToLowerInvariant()
            });
        }
        return list
            .GroupBy(h => h.Ip)
            .Select(g => g.First())
            .OrderBy(h => h.Ip)
            .ToList();
    }

    public void SaveBaseline(string path, IEnumerable<LanHost> hosts)
    {
        var json = JsonSerializer.Serialize(hosts.ToList(), JsonOpts);
        File.WriteAllText(path, json);
    }

    public List<LanHost> LoadBaseline(string path)
    {
        if (!File.Exists(path)) return new List<LanHost>();
        return JsonSerializer.Deserialize<List<LanHost>>(File.ReadAllText(path), JsonOpts) ?? new();
    }

    public LanDiffResult Diff(IEnumerable<LanHost> baseline, IEnumerable<LanHost> current)
    {
        var b = baseline.ToDictionary(h => h.Ip, StringComparer.Ordinal);
        var c = current.ToDictionary(h => h.Ip, StringComparer.Ordinal);
        var neu = c.Values.Where(h => !b.ContainsKey(h.Ip)).ToList();
        var missing = b.Values.Where(h => !c.ContainsKey(h.Ip)).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"Current hosts: {c.Count} | New: {neu.Count} | Missing: {missing.Count}");
        foreach (var h in neu) sb.AppendLine($"  NEW  {h.Ip}  {h.Mac}  {h.Type}");
        foreach (var h in missing) sb.AppendLine($"  GONE {h.Ip}  {h.Mac}");
        if (neu.Count == 0 && missing.Count == 0) sb.AppendLine("No changes vs baseline.");
        return new LanDiffResult
        {
            Current = c.Values.OrderBy(x => x.Ip).ToList(),
            NewHosts = neu,
            MissingHosts = missing,
            Summary = sb.ToString()
        };
    }
}
