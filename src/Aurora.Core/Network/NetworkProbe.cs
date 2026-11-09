using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Aurora.Core.Network;

public sealed class NetworkAddress
{
    public string Address { get; init; } = string.Empty;
    public string Family { get; init; } = "IPv4";
    public string InterfaceName { get; init; } = string.Empty;

    public string Label => Family + " · " + InterfaceName;
    public string WithPort(int port) => Family == "IPv6" ? $"[{Address}]:{port}" : $"{Address}:{port}";
}

/// <summary>本机地址探测：给「联机」页提供可以发给朋友的直连地址。</summary>
public static class NetworkProbe
{
    public static List<NetworkAddress> GetAddresses()
    {
        var result = new List<NetworkAddress>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var info in nic.GetIPProperties().UnicastAddresses)
                {
                    var address = info.Address;
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (IsPrivate(address) || !IPAddress.IsLoopback(address))
                        {
                            result.Add(new NetworkAddress
                            {
                                Address = address.ToString(),
                                Family = "IPv4",
                                InterfaceName = nic.Name,
                            });
                        }
                    }
                    else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || IPAddress.IsLoopback(address))
                        {
                            continue;
                        }

                        // 只要公网段（2000::/3）的地址，这类才能被外网直连
                        var bytes = address.GetAddressBytes();
                        if ((bytes[0] & 0xE0) != 0x20)
                        {
                            continue;
                        }

                        result.Add(new NetworkAddress
                        {
                            Address = address.ToString(),
                            Family = "IPv6",
                            InterfaceName = nic.Name,
                        });
                    }
                }
            }
        }
        catch
        {
            // 拿不到网卡信息就返回空
        }

        return result
            .DistinctBy(a => a.Address)
            .OrderBy(a => a.Family)
            .ToList();
    }

    /// <summary>本机是否有可用的公网 IPv6（有的话联机可以完全不要穿透）。</summary>
    public static bool HasPublicIpv6() => GetAddresses().Any(a => a.Family == "IPv6");

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168) ||
               (bytes[0] == 169 && bytes[1] == 254);
    }
}
