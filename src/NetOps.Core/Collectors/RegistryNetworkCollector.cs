using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NetOps.Core.Collectors;

/// <summary>
/// Read-only, scoped Windows registry access for network diagnosis.
/// Does not write. Non-Windows returns empty defaults.
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
}
