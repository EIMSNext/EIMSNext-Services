using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace EIMSNext.Common;

/// <summary>
/// Validates outbound webhook destinations before the server makes a request.
/// </summary>
public static class WebhookUrlSafety
{
    public sealed record ResolvedDestination(Uri Uri, IPAddress Address);

    public static async Task<bool> IsAllowedAsync(
        string? value,
        IConfiguration? configuration,
        CancellationToken cancellationToken = default)
        => await ResolveAsync(value, configuration, cancellationToken).ConfigureAwait(false) is not null;

    public static async Task<ResolvedDestination?> ResolveAsync(
        string? value,
        IConfiguration? configuration,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.DnsSafeHost) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(uri.DnsSafeHost, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (addresses.Length == 0)
        {
            return null;
        }

        if (addresses.All(IsPublicAddress))
        {
            return new ResolvedDestination(uri, addresses[0]);
        }

        // Internal destinations are allowed only for the configured Service API origin
        // and the public HTTP hook trigger route.
        return IsEventFlowTriggerPath(uri.AbsolutePath) &&
               IsConfiguredServiceHost(uri, configuration) &&
               addresses.All(IsInternalAddress)
            ? new ResolvedDestination(uri, addresses[0])
            : null;
    }

    public static SocketsHttpHandler CreatePinnedHandler(ResolvedDestination destination)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var client = new TcpClient(destination.Address.AddressFamily);
                await client.ConnectAsync(destination.Address, destination.Uri.Port, cancellationToken).ConfigureAwait(false);
                return client.GetStream();
            }
        };
    }

    private static bool IsEventFlowTriggerPath(string path)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 6 ||
            !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[2], "tenant", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[4], "hook", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var version = segments[1];
        return version.Length > 1 &&
               (version[0] is 'v' or 'V') &&
               char.IsDigit(version[1]) &&
               version[1..].All(ch => char.IsDigit(ch) || ch == '.') &&
               segments[3].Length > 0 &&
               segments[5].Length > 0;
    }

    private static bool IsConfiguredServiceHost(Uri target, IConfiguration? configuration)
    {
        var configured = configuration?["ServiceHost:BaseUrl"];
        return Uri.TryCreate(configured, UriKind.Absolute, out var flowHost) &&
               string.Equals(target.Scheme, flowHost.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(target.DnsSafeHost, flowHost.DnsSafeHost, StringComparison.OrdinalIgnoreCase) &&
               target.Port == flowHost.Port;
    }

    private static bool IsInternalAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];
            return first == 0 || first == 10 || first == 127 ||
                   (first == 100 && second >= 64 && second <= 127) ||
                   (first == 169 && second == 254) ||
                   (first == 172 && second >= 16 && second <= 31) ||
                   (first == 192 && second == 0) ||
                   (first == 192 && second == 168) ||
                   (first == 198 && second >= 18 && second <= 19);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
               (address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.IPv6Loopback) ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                address.GetAddressBytes()[0] is >= 0xfc and <= 0xfd);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];

            return first != 0 &&
                   first != 10 &&
                   first != 127 &&
                   !(first == 100 && second >= 64 && second <= 127) &&
                   !(first == 169 && second == 254) &&
                   !(first == 172 && second >= 16 && second <= 31) &&
                   !(first == 192 && second == 0) &&
                   !(first == 192 && second == 168) &&
                   !(first == 198 && second >= 18 && second <= 19) &&
                   first < 224;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var ipv6 = address.GetAddressBytes();
        return !address.Equals(IPAddress.IPv6Any) &&
               !address.Equals(IPAddress.IPv6Loopback) &&
               !address.IsIPv6LinkLocal &&
               !address.IsIPv6SiteLocal &&
               !address.IsIPv6Multicast &&
               !(ipv6[0] is >= 0xfc and <= 0xfd);
    }
}
