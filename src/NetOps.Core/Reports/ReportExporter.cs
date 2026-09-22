using System.Globalization;
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

    public static string BuildCsv(
        HostFacts? facts,
        DiagnosisReport? diagnosis,
        LanDiffResult? lan,
        IEnumerable<AuditEntry>? audit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("section,key,value");

        void Row(string section, string key, string? value)
            => sb.AppendLine(string.Join(',',
                Csv(section), Csv(key), Csv(value ?? "")));

        Row("meta", "generated", DateTimeOffset.Now.ToString("u"));

        if (facts is not null)
        {
            Row("facts", "adapter_count", facts.Adapters.Count.ToString(CultureInfo.InvariantCulture));
            Row("facts", "gateways", string.Join(';', facts.DefaultGateways));
            Row("facts", "dns", string.Join(';', facts.DnsServers));
            Row("facts", "proxy_enabled", facts.ProxyEnabled.ToString());
            Row("facts", "proxy_server", facts.ProxyServer);
            Row("facts", "gw_reachable", facts.Connectivity.GatewayReachable?.ToString());
            Row("facts", "gw_rtt_ms", facts.Connectivity.GatewayRttMs?.ToString(CultureInfo.InvariantCulture));
            Row("facts", "public_probe", facts.Connectivity.PublicDnsReachable?.ToString());
            Row("facts", "name_resolution", facts.Connectivity.NameResolutionWorks?.ToString());
            foreach (var a in facts.Adapters)
                Row("adapter", a.Name, $"{a.Status};up={a.IsUp};ip={string.Join('|', a.IPv4Addresses)}");
        }

        if (diagnosis is not null)
        {
            Row("diagnosis", "headline", diagnosis.Headline);
            foreach (var r in diagnosis.Results)
                Row("flow", r.FlowId, r.Title + " | " + r.Summary);
            foreach (var s in diagnosis.RankedSolutions)
                Row("solution", s.Id, $"#{s.Score};{s.Risk};{s.Title}");
        }

        if (lan is not null)
        {
            foreach (var h in lan.Current)
                Row("lan_current", h.Ip, h.Mac + ";" + h.Type);
            foreach (var h in lan.NewHosts)
                Row("lan_new", h.Ip, h.Mac);
            foreach (var h in lan.MissingHosts)
                Row("lan_missing", h.Ip, h.Mac);
        }

        if (audit is not null)
        {
            foreach (var a in audit)
                Row("audit", a.ActionId, $"{a.At:u};{a.Outcome};{a.Detail}");
        }

        return sb.ToString();
    }

    private static string Csv(string s)
    {
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    public static void WriteAll(string path, string content) => File.WriteAllText(path, content, Encoding.UTF8);
}
