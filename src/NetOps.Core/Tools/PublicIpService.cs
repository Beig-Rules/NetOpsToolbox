using System.Net.Http;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Fetch public IPv4/IPv6 via lightweight HTTPS endpoints.</summary>
public sealed class PublicIpService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly string[] Endpoints =
    [
        "https://api.ipify.org",
        "https://ifconfig.me/ip",
        "https://icanhazip.com",
    ];

    public async Task<string> RunAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PUBLIC IP ===");
        foreach (var url in Endpoints)
        {
            try
            {
                using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
                var body = (await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim();
                sb.AppendLine($"{url}");
                sb.AppendLine("  → " + body);
                if (resp.IsSuccessStatusCode && body.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Primary: " + body);
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{url}");
                sb.AppendLine("  FAIL: " + ex.Message);
            }
        }
        sb.AppendLine();
        sb.AppendLine("No endpoint succeeded.");
        return sb.ToString();
    }
}
