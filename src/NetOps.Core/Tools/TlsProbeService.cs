using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>TLS handshake probe for host:port (default 443). Read-only certificate info.</summary>
public sealed class TlsProbeService
{
    public async Task<string> RunAsync(string host, int port = 443, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TLS PROBE ===");
        sb.AppendLine($"Target: {host}:{port}");
        sb.AppendLine();
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            await using var ssl = new SslStream(client.GetStream(), false,
                (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                                      System.Security.Authentication.SslProtocols.Tls13
            }, timeoutCts.Token).ConfigureAwait(false);

            sb.AppendLine("Protocol:  " + ssl.SslProtocol);
            sb.AppendLine("Cipher:    " + ssl.NegotiatedCipherSuite);
            var cert = ssl.RemoteCertificate as X509Certificate2 ??
                       (ssl.RemoteCertificate is not null
                           ? new X509Certificate2(ssl.RemoteCertificate)
                           : null);
            if (cert is not null)
            {
                sb.AppendLine("Subject:   " + cert.Subject);
                sb.AppendLine("Issuer:    " + cert.Issuer);
                sb.AppendLine("NotBefore: " + cert.NotBefore.ToUniversalTime().ToString("u"));
                sb.AppendLine("NotAfter:  " + cert.NotAfter.ToUniversalTime().ToString("u"));
                sb.AppendLine("Thumbprint:" + cert.Thumbprint);
                var days = (cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;
                sb.AppendLine(days < 0 ? "Status:    EXPIRED" :
                    days < 30 ? $"Status:    EXPIRES SOON ({days:F0}d)" : $"Status:    OK ({days:F0}d left)");
            }
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
