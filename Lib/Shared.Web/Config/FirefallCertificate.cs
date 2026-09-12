namespace Shared.Web.Config;

/// <summary>
///     The <c>Firefall:Certificate</c> configuration section: which TLS certificate the web hosts serve
///     their https URLs with, and where PIN keeps the one it issues itself.
/// </summary>
public class FirefallCertificate
{
    /// <summary>
    ///     A PKCS#12 file (.pfx/.p12) to serve instead of the certificate PIN issues itself. Leave it empty
    ///     and PIN handles the certificate for you (see <see cref="AutoIssue"/>); set it when you already
    ///     have one whose subject alternative name covers <see cref="Firefall.PublicHost"/>.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     Password of <see cref="Path"/>, if it has one.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     Folder PIN keeps its own <c>pin-&lt;host&gt;.key</c>/<c>.crt</c>/<c>.cer</c> in. Empty means
    ///     <c>certs</c> next to the host binary. The <c>.cer</c> is the half players install.
    /// </summary>
    public string StorePath { get; set; } = string.Empty;

    /// <summary>
    ///     Issue a certificate when nobody configured one and the advertised address is not
    ///     <c>localhost</c>. On by default: a client dialling an IP address can never validate the
    ///     ASP.NET Core development certificate, which is issued for <c>localhost</c>, so without this a
    ///     server that advertises anything else has no working https at all - and the client insists on
    ///     https for the oracle URL. Turn it off to fall back to the development certificate, or point
    ///     <see cref="Path"/> at a certificate of your own.
    /// </summary>
    public bool AutoIssue { get; set; } = true;
}
