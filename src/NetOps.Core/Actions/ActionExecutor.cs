using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using NetOps.Core.Audit;

namespace NetOps.Core.Actions;

/// <summary>
/// Runs allow-listed actions. Only low-risk host actions execute in this phase.
/// </summary>
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
        {
            return Fail(actionId, "Unknown action id.");
        }

        if (def.IsAdvisoryOnly)
        {
            var skipped = new ActionResult
            {
                ActionId = actionId,
                Success = true,
                Skipped = true,
                Message = "Advisory only — no host change performed. " + def.Description
            };
            _audit.Record(actionId, "skipped", skipped.Message);
            return skipped;
        }

        return actionId switch
        {
            "FlushDns" => await FlushDnsAsync(ct).ConfigureAwait(false),
            _ => Fail(actionId, "Execute not implemented for this action yet.")
        };
    }

    private async Task<ActionResult> FlushDnsAsync(CancellationToken ct)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var r = Fail("FlushDns", "FlushDns is only supported on Windows.");
            _audit.Record("FlushDns", "fail", r.Message);
            return r;
        }

        try
        {
            var (code, stdout, stderr) = await RunProcessAsync(
                "ipconfig", "/flushdns", ct).ConfigureAwait(false);

            if (code != 0)
            {
                var fail = new ActionResult
                {
                    ActionId = "FlushDns",
                    Success = false,
                    Message = $"ipconfig /flushdns exited with code {code}.",
                    StdOut = stdout,
                    StdErr = stderr
                };
                _audit.Record("FlushDns", "fail", fail.Message);
                return fail;
            }

            var (verifyOk, verifyDetail) = await VerifyDnsAsync(ct).ConfigureAwait(false);
            var ok = new ActionResult
            {
                ActionId = "FlushDns",
                Success = true,
                Message = "DNS client cache flushed.",
                StdOut = stdout,
                VerifyOk = verifyOk,
                VerifyDetail = verifyDetail
            };
            _audit.Record("FlushDns", "ok", ok.Message + " | verify=" + verifyDetail);
            return ok;
        }
        catch (Exception ex)
        {
            var fail = Fail("FlushDns", ex.Message);
            _audit.Record("FlushDns", "fail", fail.Message);
            return fail;
        }
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
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return (p.ExitCode, stdout.Trim(), stderr.Trim());
    }

    private static ActionResult Fail(string id, string message) => new()
    {
        ActionId = id,
        Success = false,
        Message = message
    };
}
