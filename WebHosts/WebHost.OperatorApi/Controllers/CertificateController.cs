using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Common.Certificates;
using Shared.Web.Config;

namespace WebHost.OperatorApi.Controllers;

/// <summary>
///     Hands out the public half of the certificate the https hosts are served with.
/// </summary>
/// <remarks>
///     The client requires a secure oracle URL, so every player ends up dialling the TLS endpoints, and a TLS
///     endpoint whose certificate the machine does not trust is a login screen that blinks. The fix is one
///     file: the certificate <see cref="TlsCertificateStore"/> issued for <c>Firefall:PublicHost</c>,
///     installed in "Trusted Root Certification Authorities". It is served in the clear on purpose, from the
///     host the client already talks to without any trust (the ini's <c>OperatorHost</c>): a request that had
///     to be trusted first could not deliver the thing that establishes the trust. A <c>.cer</c> is a public
///     key plus a signature and nothing else - the private key stays in <c>certs\pin-&lt;host&gt;.key</c>
///     next to the binary and is never served. See <c>Docs/REMOTE_PLAY.md</c>.
/// </remarks>
[ApiController]
public class CertificateController : ControllerBase
{
    private readonly ILogger<CertificateController> _logger;
    private readonly TlsCertificate _certificate;
    private readonly PublicUrls _urls;

    public CertificateController(TlsCertificate certificate, Firefall config, ILogger<CertificateController> logger)
    {
        _certificate = certificate;
        _urls = new PublicUrls(config);
        _logger = logger;
    }

    /// <summary>
    ///     The certificate as DER - the format Windows installs from a double-click or from
    ///     <c>certutil -addstore -f Root pin.cer</c>.
    /// </summary>
    /// <returns>The public half of the certificate, or an empty 404 (with the reason in the log).</returns>
    [HttpGet(TlsCertificate.CerRoute)]
    public IActionResult Certificate()
    {
        var der = _certificate.PublicCertificateDer;
        return der == null
                   ? NothingToDownload()
                   : File(der, TlsCertificate.CerContentType, _certificate.DownloadFileName);
    }

    /// <summary>
    ///     The same certificate as a PEM document, for the tools that cannot read DER.
    /// </summary>
    /// <returns>The certificate in PEM form, or an empty 404 (with the reason in the log).</returns>
    [HttpGet(TlsCertificate.PemRoute)]
    public IActionResult CertificatePem()
    {
        var pem = _certificate.PublicCertificatePem;
        return pem == null
                   ? NothingToDownload()
                   : Content(pem, TlsCertificate.PemContentType);
    }

    /// <summary>
    ///     The answer for a server with no certificate of its own. Empty on purpose: the explanation is for
    ///     whoever configured the server, so it goes to the log - and a body on a 404 would be re-written by
    ///     the not-found recorder that runs the pipeline a second time.
    /// </summary>
    /// <returns>A 404 with no body.</returns>
    private StatusCodeResult NothingToDownload()
    {
        if (_urls.UseHttps)
        {
            _logger.LogWarning(
                "Nothing to serve at {Route}: this server advertises https on {Host} but has no certificate of its own, so it serves the ASP.NET Core development certificate - issued for localhost, and valid nowhere else. Turn Firefall:Certificate:AutoIssue back on to have PIN issue one for this address, or point Firefall:Certificate:Path at a certificate that names it.",
                TlsCertificate.CerRoute,
                _urls.Host);
        }
        else
        {
            _logger.LogWarning(
                "Nothing to serve at {Route}: this server advertises plain http, so there is no certificate to install. A client gets as far as its character list on http and no further, because it refuses an oracle URL that is not https; set Firefall:AdvertiseHttps to true and restart to have PIN issue a certificate for {Host}.",
                TlsCertificate.CerRoute,
                _urls.Host);
        }

        return StatusCode(StatusCodes.Status404NotFound);
    }
}
