using System.Net;
using System.Net.Sockets;

namespace EHub.Application.Features.Workspaces.CheckpointLinks;

public static class CheckpointLinkUrl
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        var candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length == 0 || candidate.Length > 1_000) return false;
        if (!candidate.Contains("://", StringComparison.Ordinal)) candidate = $"https://{candidate}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !IsPublicHost(uri)) return false;

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Host = uri.IdnHost.ToLowerInvariant()
        };
        if (uri.IsDefaultPort) builder.Port = -1;
        normalized = builder.Uri.AbsoluteUri;
        if (builder.Uri.AbsolutePath == "/" && string.IsNullOrEmpty(builder.Uri.Query) && string.IsNullOrEmpty(builder.Uri.Fragment))
        {
            normalized = normalized.TrimEnd('/');
        }
        return normalized.Length <= 1_000;
    }

    private static bool IsPublicHost(Uri uri)
    {
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (uri.IsLoopback || host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) ||
            host.EndsWith(".local", StringComparison.Ordinal) || host.EndsWith(".internal", StringComparison.Ordinal) ||
            host.EndsWith(".lan", StringComparison.Ordinal)) return false;
        if (IPAddress.TryParse(host, out var address)) return IsPublicAddress(address);
        return Uri.CheckHostName(host) == UriHostNameType.Dns && host.Contains('.');
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) || address.IsIPv6LinkLocal || address.IsIPv6Multicast ||
            address.IsIPv6SiteLocal) return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) != 0xFC;
        }

        var first = bytes[0];
        var second = bytes[1];
        return first != 0 && first != 10 && first != 127 && first < 224 &&
            !(first == 100 && second is >= 64 and <= 127) &&
            !(first == 169 && second == 254) &&
            !(first == 172 && second is >= 16 and <= 31) &&
            !(first == 192 && second == 168) &&
            !(first == 198 && second is 18 or 19);
    }
}
