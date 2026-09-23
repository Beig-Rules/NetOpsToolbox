using System.Globalization;
using System.Net;
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

    /// <summary>Minimal Mono-styled standalone HTML report.</summary>
    public static string BuildHtml(
        HostFacts? facts,
        DiagnosisReport? diagnosis,
        LanDiffResult? lan,
        IEnumerable<AuditEntry>? audit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>NetOps Toolbox Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,system-ui,sans-serif;background:#0a0a0a;color:#f5f5f5;margin:0;padding:24px;}");
        sb.AppendLine("h1{font-size:18px;font-weight:600;letter-spacing:.04em;margin:0 0 8px;}");
        sb.AppendLine("h2{font-size:12px;text-transform:uppercase;color:#888;margin:24px 0 8px;font-weight:600;}");
        sb.AppendLine(".meta{color:#888;font-size:12px;margin-bottom:16px;}");
        sb.AppendLine(".card{border:1px solid #333;padding:12px 16px;margin:0 0 12px;background:#111;}");
        sb.AppendLine("pre,code{font-family:Consolas,ui-monospace,monospace;font-size:12px;white-space:pre-wrap;}");
        sb.AppendLine("ul{margin:4px 0;padding-left:18px;} li{margin:2px 0;}");
        sb.AppendLine(".ok{color:#a3e635;} .warn{color:#fbbf24;} .bad{color:#f87171;}");
        sb.AppendLine("footer{margin-top:32px;font-size:11px;color:#555;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>NETOPS TOOLBOX REPORT</h1>");
        sb.AppendLine("<div class=\"meta\">Generated " + WebUtility.HtmlEncode(DateTimeOffset.Now.ToString("u")) + " · Proprietary · Beig-Rules</div>");

        if (facts is not null)
        {
            sb.AppendLine("<h2>Facts</h2><div class=\"card\"><pre>");
            sb.AppendLine("Adapters: " + facts.Adapters.Count);
            sb.AppendLine("Gateways: " + WebUtility.HtmlEncode(string.Join(", ", facts.DefaultGateways)));
            sb.AppendLine("DNS: " + WebUtility.HtmlEncode(string.Join(", ", facts.DnsServers)));
            sb.AppendLine("Proxy: " + facts.ProxyEnabled + " " + WebUtility.HtmlEncode(facts.ProxyServer ?? ""));
            sb.AppendLine("GW reachable: " + facts.Connectivity.GatewayReachable + " RTT=" + facts.Connectivity.GatewayRttMs);
            sb.AppendLine("Public probe: " + facts.Connectivity.PublicDnsReachable);
            sb.AppendLine("Name resolution: " + facts.Connectivity.NameResolutionWorks);
            sb.AppendLine("</pre></div>");
        }

        if (diagnosis is not null)
        {
            sb.AppendLine("<h2>Diagnosis</h2><div class=\"card\">");
            sb.AppendLine("<p><strong>" + WebUtility.HtmlEncode(diagnosis.Headline) + "</strong></p><ul>");
            foreach (var r in diagnosis.Results)
            {
                sb.AppendLine("<li><code>" + WebUtility.HtmlEncode(r.FlowId) + "</code> " +
                              WebUtility.HtmlEncode(r.Title) + "</li>");
            }
            sb.AppendLine("</ul><p>Solutions:</p><ul>");
            foreach (var s in diagnosis.RankedSolutions)
                sb.AppendLine("<li>#" + s.Score + " [" + WebUtility.HtmlEncode(s.Risk) + "] " +
                              WebUtility.HtmlEncode(s.Title) + "</li>");
            sb.AppendLine("</ul></div>");
        }

        if (lan is not null)
        {
            sb.AppendLine("<h2>LAN security</h2><div class=\"card\"><pre>");
            sb.AppendLine(WebUtility.HtmlEncode(lan.Summary));
            sb.AppendLine("</pre></div>");
        }

        if (audit is not null)
        {
            sb.AppendLine("<h2>Audit</h2><div class=\"card\"><pre>");
            foreach (var a in audit)
                sb.AppendLine(WebUtility.HtmlEncode($"{a.At:u} {a.ActionId} {a.Outcome} {a.Detail}"));
            sb.AppendLine("</pre></div>");
        }

        sb.AppendLine("<footer>© Beig-Rules / Big Flow · All Rights Reserved · NOT open source</footer>");
        sb.AppendLine("</body></html>");
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
