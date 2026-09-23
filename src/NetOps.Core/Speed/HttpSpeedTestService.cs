using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace NetOps.Core.Speed;

/// <summary>
/// Lightweight HTTP download throughput probe (not a full Ookla-style test).
/// Uses public CDN endpoints; measures application-layer MB/s.
/// </summary>
public sealed class HttpSpeedTestService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public static readonly (string Name, string Url)[] Presets =
    [
        ("Cloudflare 10MB", "https://speed.cloudflare.com/__down?bytes=10000000"),
        ("Cloudflare 25MB", "https://speed.cloudflare.com/__down?bytes=25000000"),
        ("ThinkBroadband 5MB", "http://ipv4.download.thinkbroadband.com/5MB.zip"),
    ];

    public async Task<string> RunAsync(string? url = null, CancellationToken ct = default)
    {
        url ??= Presets[0].Url;
        var sb = new StringBuilder();
        sb.AppendLine("=== HTTP DOWNLOAD SPEED ===");
        sb.AppendLine("URL: " + url);
        sb.AppendLine("(Application-layer estimate; path and CDN affect results.)");
        sb.AppendLine();

        try
        {
            var sw = Stopwatch.StartNew();
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buffer = new byte[64 * 1024];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                total += read;
            sw.Stop();

            var sec = Math.Max(sw.Elapsed.TotalSeconds, 0.001);
            var mb = total / (1024.0 * 1024.0);
            var mbps = (total * 8.0) / (sec * 1_000_000.0); // megabits/s
            var mBps = mb / sec; // mebibytes/s

            sb.AppendLine($"Bytes:     {total:N0}");
            sb.AppendLine($"Time:      {sw.Elapsed.TotalSeconds:F2}s");
            sb.AppendLine($"Throughput:{mBps:F2} MiB/s  (~{mbps:F1} Mbps)");
            sb.AppendLine();
            sb.AppendLine("OK");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }

        return sb.ToString();
    }
}
