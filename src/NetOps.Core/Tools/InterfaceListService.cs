using System.Net.NetworkInformation;
using System.Text;

namespace NetOps.Core.Tools;

/// <summary>Detailed NIC inventory from NetworkInterface API.</summary>
public sealed class InterfaceListService
{
    public string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== NETWORK INTERFACES ===");
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .OrderByDescending(n => n.OperationalStatus == OperationalStatus.Up)
                         .ThenBy(n => n.Name))
            {
                var props = nic.GetIPProperties();
                sb.AppendLine();
                sb.AppendLine($"[{nic.OperationalStatus}] {nic.Name}");
                sb.AppendLine($"  Type:     {nic.NetworkInterfaceType}");
                sb.AppendLine($"  Desc:     {nic.Description}");
                sb.AppendLine($"  Speed:    {(nic.Speed > 0 ? (nic.Speed / 1_000_000) + " Mbps" : "n/a")}");
                sb.AppendLine($"  MAC:      {FormatMac(nic.GetPhysicalAddress())}");
                sb.AppendLine($"  DHCP:     {props.GetIPv4Properties()?.IsDhcpEnabled}");
                foreach (var ua in props.UnicastAddresses)
                    sb.AppendLine($"  IP:       {ua.Address} / {ua.PrefixLength}");
                foreach (var gw in props.GatewayAddresses)
                    sb.AppendLine($"  Gateway:  {gw.Address}");
                foreach (var dns in props.DnsAddresses)
                    sb.AppendLine($"  DNS:      {dns}");
            }
            sb.AppendLine();
            sb.AppendLine("OK");
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL: " + ex.Message);
        }
        return sb.ToString();
    }

    private static string FormatMac(PhysicalAddress mac)
    {
        var b = mac.GetAddressBytes();
        return b.Length == 0 ? "—" : string.Join(':', b.Select(x => x.ToString("X2")));
    }
}
