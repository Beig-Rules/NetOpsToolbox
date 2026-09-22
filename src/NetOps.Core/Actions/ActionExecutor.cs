using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NetOps.Core.Audit;

namespace NetOps.Core.Actions;

public sealed class ActionExecutor
{
    private readonly AuditLog _audit;

    public ActionExecutor(AuditLog? audit = null)
    {
        _audit = audit ?? new AuditLog();
    }

    public AuditLog Audit => _audit;

    public async Task<ActionResult> ExecuteAsync(string actionId, CancellationToken ct = default)
    {
        var def = ActionCatalog.All.FirstOrDefault(a => a.Id == actionId);
        if (def is null)
            return Fail(actionId, "Unknown action id.");

        if (def.IsAdvisoryOnly)
        {
            var skipped = new ActionResult
            {
                ActionId = actionId,
                Success = true,
                Skipped = true,
                Message = "Advisory only — no host change. " + def.Description
            };
            _audit.Record(actionId, "skipped", skipped.Message);
            return skipped;
        }

        return actionId switch
        {
            "FlushDns" => await FlushDnsAsync(ct).ConfigureAwait(false),
            "RenewDhcp" => await RenewDhcpAsync(ct).ConfigureAwait(false),
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
                var fail = new ActionResult
                {
                    ActionId = "FlushDns", Success = false,
                    Message = $"ipconfig /flushdns exited {code}.",
                    StdOut = stdout, StdErr = stderr
                };
                _audit.Record("FlushDns", "fail", fail.Message);
                return fail;
            }

            var (verifyOk, verifyDetail) = await VerifyDnsAsync(ct).ConfigureAwait(false);
            var ok = new ActionResult
            {
                ActionId = "FlushDns", Success = true,
                Message = "DNS client cache flushed.",
                StdOut = stdout, VerifyOk = verifyOk, VerifyDetail = verifyDetail
            };
            _audit.Record("FlushDns", "ok", ok.Message + " | " + verifyDetail);
            return ok;
        }
        catch (Exception ex)
        {
            var fail = Fail("FlushDns", ex.Message);
            _audit.Record("FlushDns", "fail", fail.Message);
            return fail;
        }
    }

    private async Task<ActionResult> RenewDhcpAsync(CancellationToken ct)
    {
        if (!IsWindows()) return FailWin("RenewDhcp");

        try
        {
            // Capture gateways before for rollback guidance
            var beforeGw = GetGateways();

            var (c1, o1, e1) = await RunProcessAsync("ipconfig", "/release", ct).ConfigureAwait(false);
            var (c2, o2, e2) = await RunProcessAsync("ipconfig", "/renew", ct).ConfigureAwait(false);

            var stdout = (o1 + "\n" + o2).Trim();
            var stderr = (e1 + "\n" + e2).Trim();
            var okExit = c2 == 0; // renew is the critical step

            await Task.Delay(800, ct).ConfigureAwait(false);
            var afterGw = GetGateways();
            var (pingOk, rtt) = afterGw.Count > 0
                ? await PingOnceAsync(afterGw[0], 2500, ct).ConfigureAwait(false)
                : (false, (long?)null);

            var guidance =
                "Rollback guidance: if connectivity is worse, disconnect/reconnect the adapter " +
                "or run ipconfig /renew again. Previous gateways: " +
                (beforeGw.Count > 0 ? string.Join(", ", beforeGw) : "(none)") +
                ". Current gateways: " +
                (afterGw.Count > 0 ? string.Join(", ", afterGw) : "(none)") + ".";

            var result = new ActionResult
            {
                ActionId = "RenewDhcp",
                Success = okExit,
                Message = okExit
                    ? "DHCP release/renew completed. " + guidance
                    : $"DHCP renew exited {c2}. " + guidance,
                StdOut = stdout,
                StdErr = stderr,
                VerifyOk = pingOk,
                VerifyDetail = afterGw.Count > 0
                    ? $"gateway {afterGw[0]} reachable={pingOk} rtt={rtt}ms"
                    : "no gateway after renew"
            };
            _audit.Record("RenewDhcp", result.Success ? "ok" : "fail", result.Message);
            return result;
        }
        catch (Exception ex)
        {
            var fail = Fail("RenewDhcp", ex.Message + " Rollback: reconnect NIC or ipconfig /renew.");
            _audit.Record("RenewDhcp", "fail", fail.Message);
            return fail;
        }
    }

    private static List<string> GetGateways()
    {
        var list = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var g in nic.GetIPProperties().GatewayAddresses)
            {
                if (g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    list.Add(g.Address.ToString());
            }
        }
        return list.Distinct().ToList();
    }

    private static async Task<(bool ok, string detail)> VerifyDnsAsync(CancellationToken ct)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync("dns.google", ct).ConfigureAwait(false);
            if (entry.AddressList.Length > 0)
            {
                var addrs = string.Join(", ", entry.AddressList.Take(3).Select(a => a.ToString()));
                return (true, "resolve dns.google → " + addrs);
            }
            return (false, "resolve returned no addresses");
        }
        catch (Exception ex)
        {
            return (false, "resolve failed: " + ex.Message);
        }
    }

    private static async Task<(bool ok, long? rtt)> PingOnceAsync(string host, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var p = new Ping();
            var reply = await p.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return reply.Status == IPStatus.Success ? (true, reply.RoundtripTime) : (false, null);
        }
        catch { return (false, null); }
    }

    private static async Task<(int code, string stdout, string stderr)> RunProcessAsync(
        string fileName, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = new Process { StartInfo = psi };
        p.Start();
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return (p.ExitCode, (await stdoutTask).Trim(), (await stderrTask).Trim());
    }

    private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private ActionResult FailWin(string id)
    {
        var r = Fail(id, id + " is only supported on Windows.");
        _audit.Record(id, "fail", r.Message);
        return r;
    }

    private static ActionResult Fail(string id, string message) => new()
    {
        ActionId = id,
        Success = false,
        Message = message
    };
}
