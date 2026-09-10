using System;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Common.Accounts;

/// <summary>
/// The Red5 request-signature scheme the Firefall client uses to authenticate
/// against the web API.
///
/// The client never sends the password. Instead it derives a per-account
/// <c>uid</c> from the email and a <c>secret</c> from email + password, and
/// signs every request with that secret. The server stores the same uid and
/// secret for the account and verifies the signature, which is why a login
/// either matches the stored credentials exactly or fails.
///
/// This is a faithful port of the scheme as reverse engineered by the
/// themeldingwars community (FauFau's <c>FauFau.Net.Web.Auth</c> and
/// RIN.WebAPI's copy of it): the same salts, the same 200-round SHA1 secret
/// derivation and the same HMAC construction, so signatures produced by the
/// original <c>netlib2</c> client verify here byte for byte.
/// </summary>
/// <remarks>
/// SHA1 is mandated by the original client protocol and is kept for protocol
/// fidelity; it is not a security boundary for a local game-server emulator.
/// </remarks>
#pragma warning disable CA5351 // Do not use insecure cryptographic algorithm SHA1 (required by the original client protocol)
public static class Red5Auth
{
    /// <summary>HTTP header carrying the request signature.</summary>
    public const string SignatureHeaderName = "X-Red5-Signature";

    /// <summary>HTTP header carrying the secondary signature parts (not needed for verification).</summary>
    public const string Signature2HeaderName = "X-Red5-Signature2";

    /// <summary>Prefix of the signature header value, e.g. "Red5 &lt;token&gt; ver=2&amp;...".</summary>
    public const string SignaturePrefix = "Red5 ";

    /// <summary>Salt appended to the lowercased email when deriving the account uid.</summary>
    public const string UserIdSalt = "-red5salt-2239nknn234j290j09rjdj28fh8fnj234k";

    /// <summary>Salt appended to "email-password" when deriving the account secret.</summary>
    public const string UserAuthSalt = "-red5salt-7nc9bsj4j734ughb8r8dhb8938h8by987c4f7h47b";

    /// <summary>Length of a hex-encoded SHA1 digest, and therefore of tokens and secrets.</summary>
    public const int SecretLength = 40;

    private const int HmacBlockSize = 64;
    private const int SecretRounds = 200;

    /// <summary>
    /// Derive the account uid the client puts in every signature: base64 of
    /// SHA1 over the lowercased email followed by <see cref="UserIdSalt"/>.
    /// </summary>
    public static string GenerateUserId(string email)
    {
        var emailBytes = LowercaseAsciiBytes(email);
        var saltBytes = Encoding.UTF8.GetBytes(UserIdSalt);
        var work = new byte[emailBytes.Length + saltBytes.Length];
        emailBytes.AsSpan().CopyTo(work);
        saltBytes.AsSpan().CopyTo(work.AsSpan(emailBytes.Length));

        return Convert.ToBase64String(SHA1.HashData(work));
    }

    /// <summary>
    /// Derive the per-account auth secret from email and password:
    /// SHA1 over "lowercased-email-&lt;password&gt;-&lt;UserAuthSalt&gt;", hex encoded.
    /// With <paramref name="v2"/> (the version the shipped client uses) the digest
    /// is re-hashed so the total is <see cref="SecretRounds"/> SHA1 rounds.
    /// </summary>
    public static string GenerateSecret(string email, string password, bool v2 = true)
    {
        var emailBytes = LowercaseAsciiBytes(email);
        var passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
        var saltBytes = Encoding.UTF8.GetBytes(UserAuthSalt);

        var work = new byte[emailBytes.Length + 1 + passwordBytes.Length + saltBytes.Length];
        var offset = 0;
        emailBytes.AsSpan().CopyTo(work);
        offset += emailBytes.Length;
        work[offset++] = (byte)'-';
        passwordBytes.AsSpan().CopyTo(work.AsSpan(offset));
        offset += passwordBytes.Length;
        saltBytes.AsSpan().CopyTo(work.AsSpan(offset));

        var hash = SHA1.HashData(work);
        if (v2)
        {
            for (var i = 1; i < SecretRounds; i++)
            {
                hash = SHA1.HashData(hash);
            }
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Sign <paramref name="headerData"/> with the account secret. This is the
    /// standard HMAC-SHA1 construction with the 40 character hex secret
    /// zero-padded to the 64 byte SHA1 block size, which is what the client does.
    /// </summary>
    public static string GenerateToken(string secret, string headerData)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(headerData);

        var key = new byte[HmacBlockSize];
        var secretBytes = Encoding.UTF8.GetBytes(secret);

        // The secret is a 40 character hex string; clamp defensively so a
        // hand-edited longer value cannot throw.
        secretBytes.AsSpan(0, Math.Min(secretBytes.Length, HmacBlockSize)).CopyTo(key);

        using var hmac = new HMACSHA1(key);
        var token = hmac.ComputeHash(Encoding.UTF8.GetBytes(headerData));

        return Convert.ToHexString(token).ToLowerInvariant();
    }

    /// <summary>
    /// Verify a full <c>X-Red5-Signature</c> header value against the stored
    /// secret of an account. The header looks like
    /// <c>Red5 &lt;token&gt; ver=2&amp;tc=...&amp;uid=...&amp;cid=0 &lt;token2&gt;</c>;
    /// everything after "Red5 &lt;token&gt; " is the signed data.
    /// </summary>
    public static bool Verify(string secret, string header)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header))
        {
            return false;
        }

        if (!header.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = header.Substring(SignaturePrefix.Length);
        var separator = remainder.IndexOf(' ');
        if (separator != SecretLength)
        {
            // The token is always a 40 character hex digest; anything else is not
            // a well-formed signature.
            return false;
        }

        var token = remainder.Substring(0, separator);
        var headerData = remainder.Substring(separator + 1);
        if (headerData.Length == 0)
        {
            return false;
        }

        var expected = GenerateToken(secret, headerData);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(token),
            Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>
    /// Lowercase a string the way the client does it: only ASCII 'A'-'Z' are
    /// folded, every other byte (including multi-byte UTF-8 sequences) is left
    /// untouched. <see cref="string.ToLowerInvariant"/> differs for non-ASCII
    /// input, which would desynchronize uid/secret derivation from the client.
    /// </summary>
    public static byte[] LowercaseAsciiBytes(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] > 64 && bytes[i] < 91)
            {
                bytes[i] += 32;
            }
        }

        return bytes;
    }
}
#pragma warning restore CA5351 // Do not use insecure cryptographic algorithm SHA1 (required by the original client protocol)
