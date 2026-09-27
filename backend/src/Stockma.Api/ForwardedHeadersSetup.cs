using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Stockma.Api;

public static class ForwardedHeadersSetup
{
    public const string SectionName = "ForwardedHeaders";

    public static void Configure(ForwardedHeadersOptions options, IConfiguration section)
    {
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        foreach (var proxy in section.GetSection("KnownProxies").Get<string[]>() ?? [])
        {
            options.KnownProxies.Add(ParseProxy(proxy));
        }

        foreach (var network in section.GetSection("KnownNetworks").Get<string[]>() ?? [])
        {
            options.KnownNetworks.Add(ParseNetwork(network));
        }

        options.ForwardedHeaders = options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0
            ? ForwardedHeaders.None
            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    }

    private static IPAddress ParseProxy(string proxy) =>
        IPAddress.TryParse(proxy, out var address)
            ? address
            : throw new FormatException($"ForwardedHeaders:KnownProxies: '{proxy}' no es una dirección IP.");

    private static IPNetwork ParseNetwork(string cidr)
    {
        var parts = cidr.Split('/', 2);

        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var length)
            || length < 0
            || length > prefix.GetAddressBytes().Length * 8)
        {
            throw new FormatException($"ForwardedHeaders:KnownNetworks: '{cidr}' no es una red en notación CIDR (p. ej. 10.0.0.0/8).");
        }

        return new IPNetwork(prefix, length);
    }
}
