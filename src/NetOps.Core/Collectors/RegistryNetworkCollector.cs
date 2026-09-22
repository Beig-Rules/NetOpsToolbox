using System.Runtime.InteropServices;
using Microsoft.Win32;
using NetOps.Core.Facts;

namespace NetOps.Core.Collectors;

/// <summary>
/// Read-only, scoped Windows registry access for network diagnosis.
/// </summary>
public static class RegistryNetworkCollector
{
    public static (bool Enabled, string? Server) ReadProxy()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (false, null);

        using var key = Registry.CurrentUser.OpenSubKey(
            "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
        if (key is null)
            return (false, null);

        var enable = key.GetValue("ProxyEnable") as int? ?? 0;
        var server = key.GetValue("ProxyServer") as string;
        return (enable == 1, server);
    }

    /// <summary>
    /// Reads a subset of TCP/IP parameters (HKLM) — domain, search list, hostname hints.
    /// </summary>
    public static RegistryTcpipFacts ReadTcpipParameters()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new RegistryTcpipFacts();

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                "SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters");
            if (key is null)
                return new RegistryTcpipFacts();

            return new RegistryTcpipFacts
            {
                Hostname = key.GetValue("Hostname") as string,
                Domain = key.GetValue("Domain") as string,
                SearchList = key.GetValue("SearchList") as string,
                NameServer = key.GetValue("NameServer") as string,
                EnableIcmpRedirect = AsInt(key.GetValue("EnableICMPRedirect")),
                DisableTaskOffload = AsInt(key.GetValue("DisableTaskOffload"))
            };
        }
        catch
        {
            return new RegistryTcpipFacts();
        }
    }

    private static int? AsInt(object? v) => v switch
    {
        int i => i,
        string s when int.TryParse(s, out var n) => n,
        _ => null
    };
}

public sealed class RegistryTcpipFacts
{
    public string? Hostname { get; init; }
    public string? Domain { get; init; }
    public string? SearchList { get; init; }
    public string? NameServer { get; init; }
    public int? EnableIcmpRedirect { get; init; }
    public int? DisableTaskOffload { get; init; }
}
