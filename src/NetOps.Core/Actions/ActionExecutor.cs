using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NetOps.Core.Audit;

namespace NetOps.Core.Actions;

public sealed class ActionExecutor
{
    private readonly AuditLog _audit;

    public ActionExecutor(AuditLog? audit = null) => _audit = audit ?? new AuditLog();

    public AuditLog Audit => _audit;

    /// <summary>Optional interface name for SetAdapterDnsPublic (Windows netsh).</summary>
    public string? TargetInterfaceName { get; set; }

    public async Task<ActionResult> ExecuteAsync(string actionId, CancellationToken ct = default)
    {
        var def = ActionCatalog.All.FirstOrDefault(a => a.Id == actionId);
        if (def is null) return Fail(actionId, "Unknown action id.");

        if (def.IsAdvisoryOnly)
        {
            var skipped = new ActionResult
            {
                ActionId = actionId, Success = true, Skipped = true,
                Message = "Advisory only — no host change. " + def.Description
            };
            _audit.Record(actionId, "skipped", skipped.Message);
            return skipped;
        }

        return actionId switch
        {
            "FlushDns" => await FlushDnsAsync(ct).ConfigureAwait(false),
            "RenewDhcp" => await RenewDhcpAsync(ct).ConfigureAwait(false),
            "SetAdapterDnsPublic" => await SetDnsPublicAsync(ct).ConfigureAwait(false),
            _ => Fail(actionId, "Execute not implemented for this action yet.")
        };
    }

    private async Task<ActionResult> FlushDnsAsync(CancellationToken ct)
    {
        if (!IsWindows()) return FailWin("FlushDns");
        try
        {
            var (code, stdout, stderr) = await RunProcessAsync("ipconfig", "/flushdns", ct).ConfigureAwait(false);
            if (code != 0)
            {
                var fail = new ActionResult { ActionId = "FlushDns", Success = false, Message = $"exit {code}", StdOut = stdout, StdErr = stderr };
                _audit.Record("FlushDns", "fail", fail.Message);
                return fail;
            }
            var (vOk, vDetail) = await VerifyDnsAsync(ct).ConfigureAwait(false);
            var ok = new ActionResult { ActionId = "FlushDns", Success = true, Message = "DNS cache flushed.", StdOut = stdout, VerifyOk = vOk, VerifyDetail = vDetail };
            _audit.Record("FlushDns", "ok", ok.Message + " | " + vDetail);
            return ok;
        }
        catch (Exception ex) { var f = Fail("FlushDns", ex.Message); _audit.Record("FlushDns", "fail", f.Message); return f; }
    }

    private async Task<ActionResult> RenewDhcpAsync(CancellationToken ct)
    {
        if (!IsWindows()) return FailWin("RenewDhcp");
        try
        {
            var beforeGw = GetGateways();
            var (c1, o1, e1) = await RunProcessAsync("ipconfig", "/release", ct).ConfigureAwait(false);
            var (c2, o2, e2) = await RunProcessAsync("ipconfig", "/renew", ct).ConfigureAwait(false);
            await Task.Delay(800, ct).ConfigureAwait(false);
            var afterGw = GetGateways();
            var (pingOk, rtt) = afterGw.Count > 0 ? await PingOnceAsync(afterGw[0], 2500, ct).ConfigureAwait(false) : (false, (long?)null);
            var guidance = $"Rollback: reconnect NIC or ipconfig /renew. Before GW=[{string.Join(",", beforeGw)}] After=[{string.Join(",", afterGw)}]";
            var result = new ActionResult
            {
                ActionId = "RenewDhcp", Success = c2 == 0,
                Message = (c2 == 0 ? "DHCP renew completed. " : $"renew exit {c2}. ") + guidance,
                StdOut = (o1 + "\n" + o2).Trim(), StdErr = (e1 + "\n" + e2).Trim(),
                VerifyOk = pingOk,
                VerifyDetail = afterGw.Count > 0 ? $"gw {afterGw[0]} ok={pingOk} rtt={rtt}" : "no gateway"
            };
            _audit.Record("RenewDhcp", result.Success ? "ok" : "fail", result.Message);
            return result;
        }
        catch (Exception ex) { var f = Fail("RenewDhcp", ex.Message); _audit.Record("RenewDhcp", "fail", f.Message); return f; }
    }

    private async Task<ActionResult> SetDnsPublicAsync(CancellationToken ct)
    {
        if (!IsWindows()) return FailWin("SetAdapterDnsPublic");

        var ifName = TargetInterfaceName;
        if (string.IsNullOrWhiteSpace(ifName))
        {
            ifName = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && n.GetIPProperties().GatewayAddresses.Any())?.Name;
        }

        if (string.IsNullOrWhiteSpace(ifName))
        {
            var f = Fail("SetAdapterDnsPublic", "No interface name. Set TargetInterfaceName or ensure an up NIC with gateway.");
            _audit.Record("SetAdapterDnsPublic", "fail", f.Message);
            return f;
        }

        try
        {
            // Capture previous DNS for guidance
            var prev = string.Join(",", NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name == ifName)?
                .GetIPProperties().DnsAddresses.Select(a => a.ToString()) ?? Array.Empty<string>());

            var args1 = $"interface ip set dns name=\"{ifName}\" static 1.1.1.1 primary";
            var args2 = $"interface ip add dns name=\"{ifName}\" 1.0.0.1 index=2";
            var (c1, o1, e1) = await RunProcessAsync("netsh", args1, ct).ConfigureAwait(false);
            var (c2, o2, e2) = await RunProcessAsync("netsh", args2, ct).ConfigureAwait(false);
            await RunProcessAsync("ipconfig", "/flushdns", ct).ConfigureAwait(false);
            var (vOk, vDetail) = await VerifyDnsAsync(ct).ConfigureAwait(false);

            var ok = c1 == 0;
            var result = new ActionResult
            {
                ActionId = "SetAdapterDnsPublic",
                Success = ok,
                Message = ok
                    ? $"DNS set on '{ifName}' to 1.1.1.1 / 1.0.0.1. Previous: [{prev}]. Rollback: netsh interface ip set dns name=\"{ifName}\" dhcp  (or restore previous)."
                    : $"netsh failed exit={c1}/{c2}. Previous DNS was [{prev}].",
                StdOut = (o1 + "\n" + o2).Trim(),
                StdErr = (e1 + "\n" + e2).Trim(),
                VerifyOk = vOk,
                VerifyDetail = vDetail
            };
            _audit.Record("SetAdapterDnsPublic", result.Success ? "ok" : "fail", result.Message);
            return result;
        }
        catch (Exception ex)
        {
            var f = Fail("SetAdapterDnsPublic", ex.Message);
            _audit.Record("SetAdapterDnsPublic", "fail", f.Message);
            return f;
        }
    }

    private static List<string> GetGateways()
    {
        var list = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var g in nic.GetIPProperties().GatewayAddresses)
                if (g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    list.Add(g.Address.ToString());
        }
        return list.Distinct().ToList();
    }

    private static async Task<(bool ok, string detail)> VerifyDnsAsync(CancellationToken ct)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync("dns.google", ct).ConfigureAwait(false);
            return entry.AddressList.Length > 0
                ? (true, "resolve → " + string.Join(",", entry.AddressList.Take(3)))
                : (false, "no addresses");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static async Task<(bool ok, long? rtt)> PingOnceAsync(string host, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var p = new Ping();
            var reply = await p.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? (true, reply.RoundtripTime) : (false, null);
        }
        catch { return (false, null); }
    }

    private static async Task<(int code, string stdout, string stderr)> RunProcessAsync(string fileName, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName, Arguments = args, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(ct);
        var e = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return (p.ExitCode, (await o).Trim(), (await e).Trim());
    }

    private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private ActionResult FailWin(string id) { var r = Fail(id, id + " Windows-only."); _audit.Record(id, "fail", r.Message); return r; }
    private static ActionResult Fail(string id, string message) => new() { ActionId = id, Success = false, Message = message };
}
