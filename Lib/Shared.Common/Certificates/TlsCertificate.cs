using System;
using System.Security.Cryptography.X509Certificates;

namespace Shared.Common.Certificates;

/// <summary>
///     The TLS certificate a PIN server serves its https endpoints with, together with the files behind it:
///     the key that stays on the server and the public half every player has to trust.
/// </summary>
/// <remarks>
///     Three situations, and this type names all of them. A server that advertises <c>localhost</c> needs
///     nothing of its own - <see cref="HasCertificate"/> is <c>false</c> and Kestrel keeps serving the
///     ASP.NET Core development certificate, which that machine already trusts. A server that advertises an
///     address - a LAN or VPN one, a hostname - cannot use that certificate, because it is issued for
///     <c>localhost</c> and a client validating what it dialled refuses it, so <see cref="TlsCertificateStore"/>
///     issues one for the advertised address and keeps it in <c>certs</c> next to the binary
///     (<see cref="SelfIssued"/> is then <c>true</c>). And an operator who has a certificate of their own
///     configures it, which is the same object with the configured path in <see cref="CertificatePath"/>.
///     <para>
///         The public half is what a player installs, and it is handed out in the clear by the operator host
///         (<see cref="CerRoute"/>) - fetching the certificate that establishes trust is the one request that
///         cannot require that trust already. See <c>Docs/REMOTE_PLAY.md</c>.
///     </para>
/// </remarks>
public sealed class TlsCertificate
{
    /// <summary>Media type of a DER encoded certificate, what Windows expects a downloaded <c>.cer</c> to say.</summary>
    public const string CerContentType = "application/x-x509-ca-cert";

    /// <summary>Route the public half is served at by every web host - over plain http as well.</summary>
    public const string CerRoute = "/certificate.cer";

    /// <summary>Route the same certificate is served at in PEM form, for tools that cannot read DER.</summary>
    public const string PemRoute = "/certificate.pem";

    private readonly X509Certificate2 _certificate;
    private readonly string _advertisedHost;
    private readonly bool _selfIssued;
    private readonly string _privateKeyPath;
    private readonly string _certificatePath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TlsCertificate" /> class. Use
    ///     <see cref="TlsCertificateStore.Resolve"/> rather than calling this directly: that is where a
    ///     certificate is found on disk, issued, or declined for a <c>localhost</c> server.
    /// </summary>
    /// <param name="certificate">The certificate to serve; <c>null</c> keeps Kestrel's development certificate.</param>
    /// <param name="advertisedHost">The address clients dial, and the one the certificate names.</param>
    /// <param name="selfIssued">Whether PIN issued this certificate rather than being configured with one.</param>
    /// <param name="privateKeyPath">Path of the private key file, when PIN issued the certificate.</param>
    /// <param name="certificatePath">Path of the file players install, when there is one.</param>
    public TlsCertificate(X509Certificate2 certificate, string advertisedHost, bool selfIssued, string privateKeyPath, string certificatePath)
    {
        _certificate = certificate;
        _advertisedHost = advertisedHost;
        _selfIssued = selfIssued;
        _privateKeyPath = privateKeyPath;
        _certificatePath = certificatePath;
    }

    /// <summary>Whether there is a certificate of PIN's own; <c>false</c> leaves it to the development certificate.</summary>
    public bool HasCertificate => _certificate != null;

    /// <summary>Whether PIN issued this certificate, as opposed to the operator having configured one.</summary>
    public bool SelfIssued => _selfIssued;

    /// <summary>The address clients are told to dial, and the one the certificate has to name.</summary>
    public string AdvertisedHost => _advertisedHost;

    /// <summary>The certificate to hand to Kestrel, or <c>null</c> to keep the development certificate.</summary>
    public X509Certificate2 Certificate => _certificate;

    /// <summary>Path of the file players install: the public half PIN issued, or the configured <c>.pfx</c>.</summary>
    public string CertificatePath => _certificatePath;

    /// <summary>Path of the private key PIN issued with. Served to nobody, and written with owner-only permissions.</summary>
    public string PrivateKeyPath => _privateKeyPath;

    /// <summary>Suggested download name of the public half, e.g. <c>pin-26.1.2.3.cer</c>.</summary>
    public string DownloadFileName => $"{TlsCertificateStore.FileNamePrefix}{ServerAddress.FileNameFor(_advertisedHost)}.cer";

    /// <summary>The public half as DER, or <c>null</c> when there is no certificate of ours.</summary>
    public byte[] PublicCertificateDer => _certificate?.RawData;

    /// <summary>The public half as a PEM document, or <c>null</c> when there is no certificate of ours.</summary>
    public string PublicCertificatePem => _certificate == null ? null : Pem.Encode(Pem.CertificateLabel, _certificate.RawData);

    /// <summary>The certificate's thumbprint, the one string that identifies it in a store or an error.</summary>
    public string Thumbprint => _certificate?.Thumbprint;

    /// <summary>When this certificate stops being usable.</summary>
    public DateTime NotAfter => _certificate?.NotAfter ?? DateTime.MinValue;

    /// <summary>
    ///     What to tell the players about this certificate, as one log line - empty when there is nothing of
    ///     ours to hand out: a configured certificate is its owner's business, and a server that advertises
    ///     plain http has none.
    /// </summary>
    /// <param name="plainHttpBase">Base URL a host answers in the clear at, e.g. <c>http://26.1.2.3:4400</c>.</param>
    /// <returns>The instructions for the log.</returns>
    public string TrustInstructions(string plainHttpBase)
    {
        if (!HasCertificate || !_selfIssued)
        {
            return string.Empty;
        }

        return "Players have to trust PIN's own certificate before their client accepts the https URLs it advertises: " +
               $"WebHostManager --trust-cert on this machine, and on the others curl -o pin.cer {plainHttpBase}{CerRoute} " +
               $"followed by certutil -addstore -f Root pin.cer (the same file sits at {_certificatePath}).";
    }

    /// <summary>
    ///     Puts the public half of this certificate in the Windows certificate store of the machine the
    ///     server runs on - which is what lets a client on that same machine validate the https URLs PIN
    ///     advertises. Nothing writes to a trust store by itself; <c>WebHostManager --trust-cert</c> asks for
    ///     it explicitly, and a machine that only runs the client installs the downloaded <c>.cer</c> instead.
    /// </summary>
    /// <param name="localMachine">
    ///     Write the local machine store (every account on this box, needs administrator) rather than the
    ///     current user's.
    /// </param>
    /// <param name="message">What happened, phrased for whoever ran the command.</param>
    /// <returns><c>true</c> when the certificate is in the store now.</returns>
    public bool TryTrustOnThisMachine(bool localMachine, out string message)
    {
        if (!HasCertificate)
        {
            message = ServerAddress.IsLoopback(_advertisedHost)
                          ? "Nothing to trust: this server advertises localhost, where the ASP.NET Core development certificate does the job - run dotnet dev-certs https --trust."
                          : "Nothing to trust: this server advertises plain http, so there is no certificate - but the client refuses an http oracle URL and stops a player at character selection. Set Firefall:AdvertiseHttps to true and restart to have PIN issue one.";
            return false;
        }

        if (!_selfIssued)
        {
            message = $"The certificate at {_certificatePath} was configured, not issued, so PIN leaves it out of the trust store: export its public half and install that on every machine that runs a client (certutil -addstore -f Root pin.cer).";
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            message = $"Only Windows has a certificate store to install into; on this system the client trusts {_certificatePath} through its own store (on Linux: copy it to /usr/local/share/ca-certificates/pin.crt and run update-ca-certificates).";
            return false;
        }

        var location = localMachine ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        try
        {
            using var store = new X509Store(StoreName.Root, location);
            store.Open(OpenFlags.ReadWrite);
            var installed = store.Certificates.Find(X509FindType.FindByThumbprint, _certificate.Thumbprint, false);
            if (installed.Count > 0)
            {
                message = $"{DownloadFileName} is already in the {location} Trusted Root Certification Authorities store.";
                return true;
            }

            // Only the public half belongs in a trust store; the private key stays in the file next to the binary.
            using var publicHalf = X509CertificateLoader.LoadCertificate(_certificate.RawData);
            store.Add(publicHalf);
            message = $"Trusted the certificate for {_advertisedHost} (thumbprint {_certificate.Thumbprint}, valid until {_certificate.NotAfter:yyyy-MM-dd}) in the {location} Trusted Root Certification Authorities store. Machines that run a client but not this server need the same file: {_certificatePath}.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Could not write to the {location} certificate store ({ex.GetType().Name}: {ex.Message}). Run as administrator, or install it by hand: certutil -addstore -f Root \"{_certificatePath}\"";
            return false;
        }
    }
}
