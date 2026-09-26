using System.Net;
using System.Text;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;

namespace NetOps.Core.Tools;

/// <summary>
/// SNMPv3 GET (read-only) using SharpSnmpLib.
/// Input: host|user|authPass|privPass|oid
///        host|user|authPass|privPass
/// Auth = SHA, Priv = AES by default. No SET.
/// </summary>
public sealed class SnmpV3GetService
{
    public static readonly string SysDescr = "1.3.6.1.2.1.1.1.0";
    public static readonly string SysName = "1.3.6.1.2.1.1.5.0";

    public async Task<string> RunAsync(string input, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SNMP v3 GET (read-only, SHA+AES) ===");

        var parts = (input ?? "").Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]))
        {
            sb.AppendLine("Usage in Tools input:");
            sb.AppendLine("  192.168.1.1|snmpuser|AuthPass|PrivPass");
            sb.AppendLine("  192.168.1.1|snmpuser|AuthPass|PrivPass|1.3.6.1.2.1.1.5.0");
            sb.AppendLine("Auth protocol: SHA   Privacy: AES");
            return sb.ToString();
        }

        var host = parts[0];
        if (host.Contains(',')) host = host.Split(',')[0].Trim();
        var user = parts[1];
        var authPass = parts.Length > 2 ? parts[2] : "";
        var privPass = parts.Length > 3 ? parts[3] : authPass;
        var oidStr = parts.Length > 4 && parts[4].Length > 0 ? parts[4] : SysDescr;

        sb.AppendLine($"Host: {host}  User: {user}  OID: {oidStr}");
        sb.AppendLine();

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            var ip = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                      ?? addresses.FirstOrDefault()
                      ?? throw new InvalidOperationException("No address for host");

            var endpoint = new IPEndPoint(ip, 161);
            var oid = new ObjectIdentifier(oidStr);

            IAuthenticationProvider auth = new SHA1AuthenticationProvider(new OctetString(authPass));
            IPrivacyProvider priv = new AESPrivacyProvider(new OctetString(privPass), auth);

            var discovery = Messenger.GetNextDiscovery(SnmpType.GetRequestPdu);
            var report = discovery.GetResponse(3500, endpoint);

            var request = new GetRequestMessage(
                VersionCode.V3,
                Messenger.NextMessageId,
                Messenger.NextRequestId,
                new OctetString(user),
                new List<Variable> { new Variable(oid) },
                priv,
                Messenger.MaxMessageSize,
                report);

            var reply = await Task.Run(() => request.GetResponse(4000, endpoint), ct).ConfigureAwait(false);
            var pdu = reply.Pdu();
            if (pdu.ErrorStatus.ToInt32() != 0)
            {
                sb.AppendLine($"FAIL: SNMP error status={pdu.ErrorStatus} index={pdu.ErrorIndex}");
                return sb.ToString();
            }

            foreach (var v in pdu.Variables)
                sb.AppendLine($"{v.Id} = {v.Data}");

            sb.AppendLine();
            sb.AppendLine("OK — SNMPv3 authPriv. Authorized targets only.");
        }
        catch (OperationCanceledException)
        {
            sb.AppendLine("FAIL: cancelled / timeout.");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
            sb.AppendLine("Hint: confirm user, auth/priv passwords, and that the agent allows this engine.");
        }

        return sb.ToString();
    }

    public Task<string> RunSystemNameAsync(string host, string user, string authPass, string privPass, CancellationToken ct = default)
        => RunAsync($"{host}|{user}|{authPass}|{privPass}|{SysName}", ct);
}
