using System.Net;

using PaperRoute.Config;

namespace PaperRoute.Smtp;

/// <summary>Decides which client addresses may connect.</summary>
public sealed class ClientFilter
{
    private readonly IPNetwork[] _networks;

    /// <summary>Initializes a new instance of the <see cref="ClientFilter"/> class.</summary>
    /// <param name="cidrs">Allowed ranges such as <c>127.0.0.1/32</c>.</param>
    /// <exception cref="ConfigException">A range is not valid CIDR notation.</exception>
    public ClientFilter(IEnumerable<string> cidrs)
    {
        ArgumentNullException.ThrowIfNull(cidrs);
        _networks = [.. cidrs.Select(Parse)];
    }

    /// <summary>Returns whether <paramref name="address"/> is inside an allowed range.</summary>
    /// <param name="address">Remote address of the client.</param>
    /// <returns><see langword="true"/> when the client may connect.</returns>
    public bool IsAllowed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return _networks.Any(network => network.Contains(normalized));
    }

    private static IPNetwork Parse(string cidr) =>
        IPNetwork.TryParse(cidr, out var network)
            ? network
            : throw new ConfigException($"'{cidr}' in allowedClients is not a valid network - use for example 127.0.0.1/32");
}
