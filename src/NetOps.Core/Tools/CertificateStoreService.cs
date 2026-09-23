using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Read-only summary of local certificate stores (no private keys exported).</summary>
public sealed class CertificateStoreService
{
    public string Run(int maxPerStore = 25)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CERTIFICATE STORES (read-only) ===");
        sb.AppendLine("Private keys are never exported. Counts and metadata only.");
        sb.AppendLine();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            sb.AppendLine("Full store enumeration is optimized for Windows.");
            sb.AppendLine("Partial: CurrentUser\\My if available.");
        }

        var stores = new (StoreName name, StoreLocation loc)[]
        {
            (StoreName.My, StoreLocation.CurrentUser),
            (StoreName.Root, StoreLocation.CurrentUser),
            (StoreName.CertificateAuthority, StoreLocation.CurrentUser),
            (StoreName.My, StoreLocation.LocalMachine),
            (StoreName.Root, StoreLocation.LocalMachine),
            (StoreName.CertificateAuthority, StoreLocation.LocalMachine),
        };

        var now = DateTime.UtcNow;
        foreach (var (name, loc) in stores)
        {
            try
            {
                using var store = new X509Store(name, loc);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var certs = store.Certificates.Cast<X509Certificate2>().ToList();
                var expired = certs.Count(c => c.NotAfter.ToUniversalTime() < now);
                var soon = certs.Count(c =>
                {
                    var n = c.NotAfter.ToUniversalTime();
                    return n >= now && n < now.AddDays(30);
                });

                sb.AppendLine($"{loc}\\{name}: total={certs.Count} expired={expired} expiring<30d={soon}");

                var sample = certs
                    .OrderBy(c => c.NotAfter)
                    .Take(Math.Clamp(maxPerStore, 5, 50))
                    .ToList();

                foreach (var c in sample)
                {
                    var subj = Truncate(c.Subject, 60);
                    var flag = c.NotAfter.ToUniversalTime() < now ? "EXPIRED"
                        : c.NotAfter.ToUniversalTime() < now.AddDays(30) ? "SOON"
                        : "ok";
                    sb.AppendLine($"  [{flag}] {c.NotAfter:yyyy-MM-dd}  {subj}");
                }

                if (certs.Count > sample.Count)
                    sb.AppendLine($"  … +{certs.Count - sample.Count} more");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{loc}\\{name}: inaccessible ({ex.GetType().Name}: {ex.Message})");
                sb.AppendLine();
            }
        }

        sb.AppendLine("Tips:");
        sb.AppendLine("  • Expired root/intermediate CAs can break TLS to internal sites.");
        sb.AppendLine("  • Use Tools → TLS to probe a specific host:443.");
        sb.AppendLine("  • Machine store may require elevation for full view.");
        sb.AppendLine("OK");
        return sb.ToString();
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "—";
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
