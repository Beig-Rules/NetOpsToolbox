using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NetOps.Core.Actions;
using NetOps.Core.Audit;

namespace NetOps.Core.Tweaks;

public sealed class TweakDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public ActionRisk Risk { get; init; }
    public string NetshArgs { get; init; } = "";
    public string? VerifyHint { get; init; }
}

/// <summary>Safe-ish Windows TCP/IP tweaks via netsh (require admin).</summary>
public sealed class WindowsNetworkTweaks
{
    private readonly AuditLog _audit;

    public WindowsNetworkTweaks(AuditLog? audit = null) => _audit = audit ?? new AuditLog();

    public static IReadOnlyList<TweakDefinition> Catalog { get; } =
    [
        new() {
            Id = "tcp_autotune_normal", Title = "TCP autotuning = normal",
            Description = "Recommended default receive window autotuning.",
            Risk = ActionRisk.Low,
            NetshArgs = "int tcp set global autotuninglevel=normal",
            VerifyHint = "netsh int tcp show global"
        },
        new() {
            Id = "tcp_autotune_disabled", Title = "TCP autotuning = disabled",
            Description = "Sometimes helps broken middleboxes; can hurt throughput.",
            Risk = ActionRisk.Medium,
            NetshArgs = "int tcp set global autotuninglevel=disabled",
            VerifyHint = "netsh int tcp show global"
        },
        new() {
            Id = "rss_enable", Title = "RSS enable",
            Description = "Receive Side Scaling on.",
            Risk = ActionRisk.Low,
            NetshArgs = "int tcp set global rss=enabled",
            VerifyHint = "netsh int tcp show global"
        },
        new() {
            Id = "ecn_enable", Title = "ECN enable",
            Description = "Explicit Congestion Notification.",
            Risk = ActionRisk.Low,
            NetshArgs = "int tcp set global ecncapability=enabled",
            VerifyHint = "netsh int tcp show global"
        },
        new() {
            Id = "timestamps_enable", Title = "TCP timestamps enable",
            Description = "RFC1323 timestamps.",
            Risk = ActionRisk.Low,
            NetshArgs = "int tcp set global timestamps=enabled",
            VerifyHint = "netsh int tcp show global"
        },
        new() {
            Id = "show_tcp_global", Title = "Show TCP global (read-only)",
            Description = "Display current TCP stack settings.",
            Risk = ActionRisk.Low,
            NetshArgs = "int tcp show global",
            VerifyHint = null
        }
    ];

    public async Task<ActionResult> ApplyAsync(string tweakId, CancellationToken ct = default)
    {
        var def = Catalog.FirstOrDefault(t => t.Id == tweakId);
        if (def is null)
            return new ActionResult { ActionId = tweakId, Success = false, Message = "Unknown tweak." };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new ActionResult { ActionId = tweakId, Success = false, Message = "Windows only." };

        try
        {
            var (code, stdout, stderr) = await RunNetshAsync(def.NetshArgs, ct).ConfigureAwait(false);
            var ok = code == 0 || def.Id == "show_tcp_global";
            // show always "succeeds" if we got output
            if (def.Id == "show_tcp_global") ok = !string.IsNullOrWhiteSpace(stdout);

            var result = new ActionResult
            {
                ActionId = tweakId,
                Success = ok,
                Message = ok ? def.Title + " applied/shown." : $"netsh exit {code}",
                StdOut = stdout,
                StdErr = stderr,
                VerifyDetail = def.VerifyHint
            };
            _audit.Record(tweakId, ok ? "ok" : "fail", result.Message);
            return result;
        }
        catch (Exception ex)
        {
            var fail = new ActionResult { ActionId = tweakId, Success = false, Message = ex.Message };
            _audit.Record(tweakId, "fail", fail.Message);
            return fail;
        }
    }

    public static string CatalogText()
    {
        var sb = new StringBuilder();
        foreach (var t in Catalog)
            sb.AppendLine($"[{t.Risk}] {t.Id}\n  {t.Title}\n  {t.Description}\n");
        return sb.ToString();
    }

    private static async Task<(int code, string stdout, string stderr)> RunNetshAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(ct);
        var e = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return (p.ExitCode, (await o).Trim(), (await e).Trim());
    }
}
