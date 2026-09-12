using System;
using System.Text;

namespace Shared.Common.Certificates;

/// <summary>
///     The PEM documents PIN keeps its certificate and key in: base64 under banner lines, the format
///     <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPemFile(string, string)"/>
///     reads and the one every other TLS tool expects. Written here by hand rather than through a
///     convenience API, so a file produced by <c>openssl</c>, by an older runtime, or by the operator with
///     their own key, is byte-for-byte the same kind of file PIN writes and can read back.
/// </summary>
public static class Pem
{
    /// <summary>PEM label of a PKCS#8 private key.</summary>
    public const string PrivateKeyLabel = "PRIVATE KEY";

    /// <summary>PEM label of a certificate.</summary>
    public const string CertificateLabel = "CERTIFICATE";

    /// <summary>Base64 line length PEM mandates (RFC 7468 allows longer; every tool in the world writes 64).</summary>
    private const int LineLength = 64;

    /// <summary>
    ///     Wraps DER bytes in a PEM document with the given label, ending in a newline so a text editor and
    ///     <c>openssl</c> both consider it complete.
    /// </summary>
    /// <param name="label">One of <see cref="PrivateKeyLabel"/> or <see cref="CertificateLabel"/>.</param>
    /// <param name="der">The encoded key or certificate.</param>
    /// <returns>The PEM document.</returns>
    public static string Encode(string label, byte[] der)
    {
        if (label == null)
        {
            throw new ArgumentNullException(nameof(label));
        }

        if (der == null || der.Length == 0)
        {
            throw new ArgumentException("There is nothing to encode.", nameof(der));
        }

        var body = Convert.ToBase64String(der);
        var builder = new StringBuilder(body.Length + ((body.Length / LineLength) * (LineLength + 2)) + (2 * label.Length) + 40);
        _ = builder.Append("-----BEGIN ").Append(label).AppendLine("-----");
        for (var offset = 0; offset < body.Length; offset += LineLength)
        {
            _ = builder.Append(body, offset, Math.Min(LineLength, body.Length - offset)).AppendLine();
        }

        _ = builder.Append("-----END ").Append(label).AppendLine("-----");
        return builder.ToString();
    }
}
