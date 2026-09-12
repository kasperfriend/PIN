using System;
using System.Net;
using System.Text;

namespace Shared.Common;

/// <summary>
///     Judgements about the address a server is reached at. Kept out of the web layer because both halves
///     of PIN need the same answer - what a client dials decides which TLS certificate has to name it, and
///     which client can be served from the machine's own loopback.
/// </summary>
public static class ServerAddress
{
    /// <summary>
    ///     Whether a host name or address only ever resolves to the machine it is written on.
    /// </summary>
    /// <remarks>
    ///     This is the difference between "the client is this machine" and "the client is somebody else": an
    ///     address that means only this machine can be served by a certificate nobody else has to trust (the
    ///     ASP.NET Core development certificate, issued for <c>localhost</c> and trusted by the machine that
    ///     generated it), and anything else - a LAN or VPN address, a hostname - is an address a certificate
    ///     has to be issued <em>for</em>. See <see cref="Certificates.TlsCertificateStore"/>.
    /// </remarks>
    /// <param name="host">The address, as configured or as advertised. Empty means this machine.</param>
    /// <returns><c>true</c> for an empty address, <c>localhost</c>, <c>loopback</c>, <c>127.0.0.1</c> or <c>::1</c>.</returns>
    public static bool IsLoopback(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        return TryParse(host, out var address)
                   ? IPAddress.IsLoopback(address)
                   : string.Equals(AddressHost(host), "localhost", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(AddressHost(host), "loopback", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether an address is a literal IP address rather than a name.</summary>
    /// <param name="host">The address to look at.</param>
    /// <returns><c>true</c> when it parses as an IPv4 or IPv6 address.</returns>
    public static bool IsIpAddress(string host) => TryParse(host, out _);

    /// <summary>
    ///     The address without the dressing a URL or a hosts entry puts around it: brackets around an IPv6
    ///     address, the trailing dot a fully qualified name carries.
    /// </summary>
    /// <param name="host">The address as it was written.</param>
    /// <returns>The bare host name or address, lower-cased only where a name does not care.</returns>
    public static string AddressHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        host = host.Trim().TrimEnd('.');
        if (host.Length > 1 && host[0] == '[' && host[host.Length - 1] == ']')
        {
            host = host[1..^1];
        }

        return host;
    }

    /// <summary>Parses an address after stripping whatever a URL puts around it.</summary>
    private static bool TryParse(string host, out IPAddress address) =>
        IPAddress.TryParse(AddressHost(host), out address);

    /// <summary>
    ///     A file name for an address, so one artifact per advertised address sits side by side and a changed
    ///     VPN address leaves the certificate the old one installed alone.
    /// </summary>
    /// <param name="host">The address.</param>
    /// <param name="fallback">What to call an empty address.</param>
    /// <returns>The address, lower-cased, with everything a file name dislikes replaced by <c>_</c>.</returns>
    public static string FileNameFor(string host, string fallback = "localhost")
    {
        var address = AddressHost(host);
        if (address.Length == 0)
        {
            return fallback;
        }

        var builder = new StringBuilder(address.Length);
        foreach (var character in address.ToLowerInvariant())
        {
            _ = char.IsLetterOrDigit(character) || character is '.' or '-'
                    ? builder.Append(character)
                    : builder.Append('_');
        }

        return builder.ToString();
    }
}
