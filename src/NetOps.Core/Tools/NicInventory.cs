using System.Net.NetworkInformation;

namespace NetOps.Core.Tools;

/// <summary>Shared NIC list for Diagnose DNS picker and reports.</summary>
public static class NicInventory
{
    public sealed record NicItem(string Name, string Description, bool IsUp, bool HasGateway);

    public static IReadOnlyList<NicItem> ListUseful()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(n =>
            {
                var props = n.GetIPProperties();
                var gw = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return new NicItem(n.Name, n.Description,
                    n.OperationalStatus == OperationalStatus.Up, gw);
            })
            .OrderByDescending(n => n.IsUp)
            .ThenByDescending(n => n.HasGateway)
            .ThenBy(n => n.Name)
            .ToList();
    }

    public static string? PreferDefault()
        => ListUseful().FirstOrDefault(n => n.IsUp && n.HasGateway)?.Name
           ?? ListUseful().FirstOrDefault(n => n.IsUp)?.Name;
}
