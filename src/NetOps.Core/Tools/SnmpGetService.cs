using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>
/// Minimal SNMPv2c GET (UDP/161) for common system OIDs. Read-only; no SET.
/// Input format: host  or  host|community  or  host|community|oid
/// Default community public; default OID sysDescr.0
/// </summary>
public sealed class SnmpGetService
{
    public static readonly string SysDescr = "1.3.6.1.2.1.1.1.0";
    public static readonly string SysObjectId = "1.3.6.1.2.1.1.2.0";
    public static readonly string SysUpTime = "1.3.6.1.2.1.1.3.0";
    public static readonly string SysContact = "1.3.6.1.2.1.1.4.0";
    public static readonly string SysName = "1.3.6.1.2.1.1.5.0";
    public static readonly string SysLocation = "1.3.6.1.2.1.1.6.0";

    public async Task<string> RunAsync(string input, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SNMP v2c GET (read-only) ===");

        var parts = (input ?? "").Split('|', StringSplitOptions.TrimEntries);
        var host = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : "";
        // allow host from comma-first ToolsInput style: take first token before comma if no |
        if (host.Contains(','))
            host = host.Split(',')[0].Trim();

        var community = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : "public";
        var oid = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : SysDescr;

        if (string.IsNullOrWhiteSpace(host))
        {
            sb.AppendLine("Usage in Tools input:");
            sb.AppendLine("  192.168.1.1");
            sb.AppendLine("  192.168.1.1|public");
            sb.AppendLine("  192.168.1.1|public|1.3.6.1.2.1.1.5.0");
            sb.AppendLine("Default OID: sysDescr.0");
            return sb.ToString();
        }

        sb.AppendLine($"Host: {host}  Community: {community}  OID: {oid}");
        sb.AppendLine();

        try
        {
            var oidNums = ParseOid(oid);
            var request = BuildGetRequest(community, oidNums, requestId: 1);

            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 3000;
            udp.Client.SendTimeout = 3000;

            var endpoints = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            var ip = endpoints.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? endpoints.FirstOrDefault()
                      ?? throw new InvalidOperationException("No address for host");

            var ep = new IPEndPoint(ip, 161);
            await udp.SendAsync(request, request.Length, ep).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(3500);
            var result = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);

            sb.AppendLine(ParseResponse(result.Buffer));
            sb.AppendLine();
            sb.AppendLine("OK — authorized targets only; never scan public Internet with community strings.");
        }
        catch (OperationCanceledException)
        {
            sb.AppendLine("FAIL: timeout (no SNMP response on UDP/161).");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }

        return sb.ToString();
    }

    public async Task<string> RunSystemSummaryAsync(string host, string community = "public", CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SNMP system summary ===");
        foreach (var (label, oid) in new[]
                 {
                     ("sysDescr", SysDescr), ("sysName", SysName), ("sysUpTime", SysUpTime),
                     ("sysContact", SysContact), ("sysLocation", SysLocation)
                 })
        {
            var one = await RunAsync($"{host}|{community}|{oid}", ct).ConfigureAwait(false);
            var valLine = one.Split('\n').FirstOrDefault(l => l.StartsWith("VALUE:", StringComparison.Ordinal))
                          ?? one.Split('\n').FirstOrDefault(l => l.StartsWith("FAIL", StringComparison.Ordinal))
                          ?? "—";
            sb.AppendLine($"{label}: {valLine.Replace("VALUE: ", "")}");
        }
        return sb.ToString();
    }

    private static int[] ParseOid(string oid)
    {
        var s = oid.Trim().TrimStart('.');
        return s.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    private static byte[] BuildGetRequest(string community, int[] oid, int requestId)
    {
        // SNMPv2c GET: SEQUENCE { version=1, community, PDU=GetRequest }
        var oidBer = EncodeOid(oid);
        var varBind = Seq(Concat(oidBer, new byte[] { 0x05, 0x00 })); // NULL value
        var varBindList = Seq(varBind);

        var pduBody = Concat(
            EncodeInteger(requestId),
            EncodeInteger(0), // error-status
            EncodeInteger(0), // error-index
            varBindList
        );
        // GetRequest-PDU tag 0xA0
        var pdu = new byte[2 + pduBody.Length];
        pdu[0] = 0xA0;
        pdu[1] = (byte)pduBody.Length;
        Buffer.BlockCopy(pduBody, 0, pdu, 2, pduBody.Length);

        var version = EncodeInteger(1); // SNMPv2c
        var comm = EncodeOctetString(Encoding.ASCII.GetBytes(community));
        return Seq(Concat(version, comm, pdu));
    }

    private static string ParseResponse(byte[] buf)
    {
        // Best-effort extract: find printable OCTET STRING after OID, or INTEGER
        try
        {
            // Search for OCTET STRING (0x04)
            for (var i = 0; i < buf.Length - 2; i++)
            {
                if (buf[i] != 0x04) continue;
                var len = buf[i + 1];
                if (len == 0 || i + 2 + len > buf.Length) continue;
                // skip community string near start — prefer later occurrences
                if (i < 12) continue;
                var s = Encoding.UTF8.GetString(buf, i + 2, len);
                if (s.Length > 0 && s.Any(char.IsLetterOrDigit))
                    return "VALUE: " + s;
            }

            // INTEGER / Counter (0x02 or 0x41)
            for (var i = 0; i < buf.Length - 2; i++)
            {
                if (buf[i] is not (0x02 or 0x41 or 0x43 or 0x46)) continue;
                var len = buf[i + 1];
                if (len is < 1 or > 8 || i + 2 + len > buf.Length) continue;
                if (i < 8) continue;
                long v = 0;
                for (var j = 0; j < len; j++)
                    v = (v << 8) | buf[i + 2 + j];
                return "VALUE: " + v;
            }

            return "VALUE: (raw " + buf.Length + " bytes — parse limited)";
        }
        catch (Exception ex)
        {
            return "VALUE parse error: " + ex.Message;
        }
    }

    private static byte[] EncodeOid(int[] oid)
    {
        if (oid.Length < 2) throw new ArgumentException("OID too short");
        var body = new List<byte> { (byte)(40 * oid[0] + oid[1]) };
        for (var i = 2; i < oid.Length; i++)
            body.AddRange(EncodeBase128(oid[i]));
        var r = new byte[2 + body.Count];
        r[0] = 0x06;
        r[1] = (byte)body.Count;
        body.CopyTo(r, 2);
        return r;
    }

    private static IEnumerable<byte> EncodeBase128(int value)
    {
        if (value < 0) value = 0;
        var bytes = new Stack<byte>();
        bytes.Push((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0)
        {
            bytes.Push((byte)(0x80 | (value & 0x7F)));
            value >>= 7;
        }
        return bytes;
    }

    private static byte[] EncodeInteger(int value)
    {
        // simplified signed integer encoding
        if (value >= 0 && value <= 127)
            return new byte[] { 0x02, 0x01, (byte)value };
        if (value <= 0x7FFF)
            return new byte[] { 0x02, 0x02, (byte)(value >> 8), (byte)(value & 0xFF) };
        return new byte[] { 0x02, 0x04, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
    }

    private static byte[] EncodeOctetString(byte[] data)
    {
        var r = new byte[2 + data.Length];
        r[0] = 0x04;
        r[1] = (byte)data.Length;
        Buffer.BlockCopy(data, 0, r, 2, data.Length);
        return r;
    }

    private static byte[] Seq(byte[] body)
    {
        if (body.Length < 128)
        {
            var r = new byte[2 + body.Length];
            r[0] = 0x30;
            r[1] = (byte)body.Length;
            Buffer.BlockCopy(body, 0, r, 2, body.Length);
            return r;
        }
        // long form length (1 byte length-of-length)
        var r2 = new byte[3 + body.Length];
        r2[0] = 0x30;
        r2[1] = 0x81;
        r2[2] = (byte)body.Length;
        Buffer.BlockCopy(body, 0, r2, 3, body.Length);
        return r2;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var len = parts.Sum(p => p.Length);
        var r = new byte[len];
        var o = 0;
        foreach (var p in parts)
        {
            Buffer.BlockCopy(p, 0, r, o, p.Length);
            o += p.Length;
        }
        return r;
    }
}
