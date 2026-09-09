using System;
using System.Net;

namespace Shared.Common.Accounts;

/// <summary>
/// A parsed <c>X-Red5-Signature</c> request header.
///
/// The header value the Firefall client sends looks like
/// <c>Red5 &lt;token&gt; ver=2&amp;tc=1430591697&amp;nonce=...&amp;uid=...&amp;host=...&amp;path=...&amp;hbody=...&amp;cid=0 &lt;token2&gt;</c>
/// (captured from the live client against <c>clientapi-v01-ew1.firefallthegame.com</c>,
/// 2015-05-02, build beta-1869). The <see cref="Uid"/> identifies the account the
/// request is made for; the whole tail after the token is what gets signed.
/// </summary>
public sealed class Red5Signature
{
    /// <summary>The 40 character hex token the client computed for this request.</summary>
    public string Token { get; private init; }

    /// <summary>The optional trailing token (X-Red5-Signature2 payload); part of the signed data.</summary>
    public string Token2 { get; private init; }

    /// <summary>The raw, still URL-encoded query string part of the header.</summary>
    public string QueryString { get; private init; }

    /// <summary>Everything after "Red5 &lt;token&gt; " — exactly the bytes covered by the signature.</summary>
    public string HeaderData { get; private init; }

    /// <summary>URL-decoded account uid (<see cref="Red5Auth.GenerateUserId"/>), the account lookup key.</summary>
    public string Uid { get; private init; }

    public string Nonce { get; private init; }

    public string Host { get; private init; }

    public string Path { get; private init; }

    /// <summary>Hex SHA1 of the request body ("da39a3..." for an empty body).</summary>
    public string BodyHash { get; private init; }

    public uint TimeCode { get; private init; }

    public uint ClientId { get; private init; }

    public int Version { get; private init; }

    /// <summary>
    /// Parse a signature header value. Returns false for null, malformed or
    /// uid-less headers instead of throwing, so callers can treat an
    /// unauthenticated request as a plain login failure.
    /// </summary>
    public static bool TryParse(string header, out Red5Signature signature)
    {
        signature = null;

        if (string.IsNullOrEmpty(header) || !header.StartsWith(Red5Auth.SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = header.Substring(Red5Auth.SignaturePrefix.Length);
        var separator = remainder.IndexOf(' ');
        if (separator != Red5Auth.SecretLength)
        {
            // "Red5 " + a 40 character token + " " + data; anything else is malformed.
            return false;
        }

        var token = remainder.Substring(0, separator);
        var headerData = remainder.Substring(separator + 1);
        if (headerData.Length == 0)
        {
            return false;
        }

        // headerData is "<query string> <token2>". The query string is URL-encoded
        // so it never contains a literal space; token2 is optional.
        var queryEnd = headerData.IndexOf(' ');
        var queryString = queryEnd < 0 ? headerData : headerData.Substring(0, queryEnd);
        var token2 = queryEnd < 0 ? null : headerData.Substring(queryEnd + 1);

        string uid = null;
        string nonce = null;
        string host = null;
        string path = null;
        string bodyHash = null;
        uint timeCode = 0;
        uint clientId = 0;
        var version = 0;

        foreach (var pair in queryString.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = pair.Substring(0, equals);
            var value = pair.Substring(equals + 1);

            if (key.Equals("ver", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(value, out version);
            }
            else if (key.Equals("tc", StringComparison.OrdinalIgnoreCase))
            {
                uint.TryParse(value, out timeCode);
            }
            else if (key.Equals("nonce", StringComparison.OrdinalIgnoreCase))
            {
                nonce = value;
            }
            else if (key.Equals("uid", StringComparison.OrdinalIgnoreCase))
            {
                uid = WebUtility.UrlDecode(value);
            }
            else if (key.Equals("host", StringComparison.OrdinalIgnoreCase))
            {
                host = WebUtility.UrlDecode(value);
            }
            else if (key.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                path = WebUtility.UrlDecode(value);
            }
            else if (key.Equals("hbody", StringComparison.OrdinalIgnoreCase))
            {
                bodyHash = value;
            }
            else if (key.Equals("cid", StringComparison.OrdinalIgnoreCase))
            {
                uint.TryParse(value, out clientId);
            }
        }

        if (string.IsNullOrEmpty(uid))
        {
            return false;
        }

        signature = new Red5Signature
        {
            Token = token,
            Token2 = token2,
            QueryString = queryString,
            HeaderData = headerData,
            Uid = uid,
            Nonce = nonce,
            Host = host,
            Path = path,
            BodyHash = bodyHash,
            TimeCode = timeCode,
            ClientId = clientId,
            Version = version
        };

        return true;
    }
}
