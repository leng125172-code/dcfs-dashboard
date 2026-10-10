using System.Net;
using System.Net.NetworkInformation;

internal static class LocalEndpointPolicy
{
    private static readonly string[] DeniedInterfacePrefixes =
        ["docker0", "br-", "veth", "virbr", "cni", "flannel", "tun", "tap"];

    public static bool IsAllowed(IPAddress? localAddress)
    {
        if (localAddress is null)
        {
            return false;
        }

        var normalizedAddress = localAddress.IsIPv4MappedToIPv6
            ? localAddress.MapToIPv4()
            : localAddress;
        if (IPAddress.IsLoopback(normalizedAddress))
        {
            return true;
        }

        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                !DeniedInterfacePrefixes.Any(prefix =>
                    networkInterface.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address.IsIPv4MappedToIPv6 ? address.Address.MapToIPv4() : address.Address)
            .Any(address => address.Equals(normalizedAddress));
    }
}
