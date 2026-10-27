using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DockBuddy.Server.Network;

/// <param name="Mac">Hardware address, e.g. "A0-B1-C2-D3-E4-F5".</param>
/// <param name="Broadcast">The LAN's broadcast address a magic packet should be sent to.</param>
public record WakeAdapter(string Mac, string Ip, string Broadcast, string Type);

/// <summary>
/// What a phone needs to wake this PC later (Wake-on-LAN): the network cards' hardware addresses. The phone
/// stores them while the PC is on, then sends the magic packet itself when the PC is off.
/// </summary>
public static class WakeInfo
{
    public static List<WakeAdapter> Adapters()
    {
        var found = new List<(WakeAdapter A, bool HasGateway)>();
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up) continue;
                if (n.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.Wireless80211)) continue;
                var mac = n.GetPhysicalAddress().GetAddressBytes();
                if (mac.Length != 6 || mac.All(b => b == 0)) continue;
                var props = n.GetIPProperties();
                var v4 = props.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                if (v4 == null || v4.IPv4Mask == null) continue;
                var ip = v4.Address.GetAddressBytes();
                var mask = v4.IPv4Mask.GetAddressBytes();
                var bcast = ip.Select((b, i) => (byte)(b | ~mask[i])).ToArray();
                var type = n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "wifi" : "ethernet";
                var gateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any));
                found.Add((new WakeAdapter(string.Join('-', mac.Select(b => b.ToString("X2"))), v4.Address.ToString(),
                    new System.Net.IPAddress(bcast).ToString(), type), gateway));
            }
        }
        catch { /* no network information available */ }
        // The real LAN card first (it has a gateway), wired before Wi-Fi: Wake-on-LAN rarely works over Wi-Fi.
        return found.OrderByDescending(f => f.HasGateway).ThenBy(f => f.A.Type == "ethernet" ? 0 : 1).Select(f => f.A).ToList();
    }
}
