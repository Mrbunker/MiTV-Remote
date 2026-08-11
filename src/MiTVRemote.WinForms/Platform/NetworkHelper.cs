using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MiTVRemote.Platform;

public static class NetworkHelper
{
    public static IReadOnlyList<string> LocalIPv4Prefixes()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            foreach (var address in adapter.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork ||
                    IPAddress.IsLoopback(address.Address))
                    continue;
                var octets = address.Address.ToString().Split('.');
                if (octets.Length == 4) result.Add(string.Join('.', octets[0], octets[1], octets[2]));
            }
        }
        return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
