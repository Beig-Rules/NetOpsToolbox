using System.Text;
using NetOps.Core.Audit;
using NetOps.Core.Diagnosis;
using NetOps.Core.Facts;
using NetOps.Core.Security;

namespace NetOps.Core.Reports;

public static class ReportExporter
{
    public static string BuildText(
        HostFacts? facts,
        DiagnosisReport? diagnosis,
        LanDiffResult? lan,
        IEnumerable<AuditEntry>? audit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NetOps Toolbox Report");
        sb.AppendLine("Generated: " + DateTimeOffset.Now.ToString("u"));
        sb.AppendLine(new string('-', 48));

        if (facts is not null)
        {
            sb.AppendLine();
            sb.AppendLine("[FACTS]");
            sb.AppendLine($"Adapters: {facts.Adapters.Count}");
            sb.AppendLine("Gateways: " + string.Join(", ", facts.DefaultGateways));
            sb.AppendLine("DNS: " + string.Join(", ", facts.DnsServers));
            sb.AppendLine($"Proxy: {facts.ProxyEnabled} {facts.ProxyServer}");
            sb.AppendLine($"GW reachable: {facts.Connectivity.GatewayReachable} RTT={facts.Connectivity.GatewayRttMs}");
            sb.AppendLine($"Public probe: {facts.Connectivity.PublicDnsReachable}");
            sb.AppendLine($"DNS names: {facts.Connectivity.NameResolutionWorks}");
        }

        if (diagnosis is not null)
        {
            sb.AppendLine();
            sb.AppendLine("[DIAGNOSIS] " + diagnosis.Headline);
            foreach (var r in diagnosis.Results)
            {
                sb.AppendLine($"  {r.FlowId}: {r.Title}");
                foreach (var e in r.Evidence) sb.AppendLine("    - " + e);
            }
            foreach (var s in diagnosis.RankedSolutions)
                sb.AppendLine($"  solution #{s.Score} [{s.Risk}] {s.Title}");
        }

        if (lan is not null)
        {
            sb.AppendLine();
            sb.AppendLine("[LAN SECURITY]");
            sb.AppendLine(lan.Summary);
        }

        if (audit is not null)
        {
            sb.AppendLine();
            sb.AppendLine("[AUDIT]");
            foreach (var a in audit)
                sb.AppendLine($"{a.At:u} {a.ActionId} {a.Outcome} {a.Detail}");
        }

        return sb.ToString();
    }

    public static void WriteAll(string path, string content) => File.WriteAllText(path, content, Encoding.UTF8);
}
