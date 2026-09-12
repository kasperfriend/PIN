using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Serilog;

namespace Shared.Common.Certificates;

/// <summary>
///     Issues, keeps and loads the certificate a PIN server serves its https endpoints with.
/// </summary>
/// <remarks>
///     <para>
///         TLS here is not a hardening option PIN could skip: the client checks the oracle URL it was handed
///         and refuses one that is not secure (<c>Oracle URL http://host:4402 not configured for HTTPS
///         (request must be secure)</c>), which is where a plain http server leaves a player - logged in,
///         character list open, world unreachable. And TLS over an <em>address</em>, the only thing a player
///         on another machine can dial, is where the ASP.NET Core development certificate runs out: it is
///         issued for <c>localhost</c>, so nothing that dials <c>26.1.2.3</c> accepts it.
///     </para>
///     <para>
///         So PIN issues what it needs. A self-signed certificate for the advertised address goes into
///         <c>certs</c> next to the binary - <c>pin-&lt;host&gt;.key</c> and <c>.crt</c> for people and tools,
///         <c>pin-&lt;host&gt;.pfx</c> as the container the hosts serve from, and <c>pin-&lt;host&gt;.cer</c>
///         for the players - in the shape the development certificate has (its own trust anchor, server
///         authentication, RSA 2048), plus <c>localhost</c> and the loopback addresses in the same subject
///         alternative name so one certificate covers a player's own ini entry point and the URLs the server
///         advertises. On disk rather than in memory is the load-bearing part: a certificate that changed at
///         every restart would make everybody reinstall it at every restart, so it is reused until it stops
///         naming the advertised address or runs out of validity, and an address change gets a second file
///         instead of invalidating the first.
///     </para>
///     <para>
///         The private key is never served and never leaves the machine; the public half is downloadable in
///         the clear (see <see cref="TlsCertificate.CerRoute"/>) because that download is what establishes the
///         trust the rest of the flow needs. Nothing here writes to a trust store -
///         <c>WebHostManager --trust-cert</c> does, when somebody asks for it. See <c>Docs/REMOTE_PLAY.md</c>.
///     </para>
/// </remarks>
public static class TlsCertificateStore
{
    /// <summary>RSA key size of an issued certificate - what the client's TLS stack has always been given.</summary>
    public const int RsaKeySize = 2048;

    /// <summary>How long an issued certificate stays valid; long enough that a VPN setup outlives it.</summary>
    public const int ValidityYears = 5;

    /// <summary>OID of the server authentication enhanced key usage a TLS server certificate has to carry.</summary>
    public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>OID of the subject alternative name extension - the list of names a certificate is valid for.</summary>
    public const string SubjectAlternativeNameOid = "2.5.29.17";

    /// <summary>Folder under the binary PIN keeps its own key and certificate in.</summary>
    public const string DefaultDirectoryName = "certs";

    /// <summary>Prefix of the certificate file names, so they read as what they are in a folder of build output.</summary>
    public const string FileNamePrefix = "pin-";

    /// <summary>Extension of the PKCS#12 container the hosts load their serving certificate from.</summary>
    private const string PfxExtension = ".pfx";

    /// <summary>
    ///     Password of the PKCS#12 the store keeps for its own certificates. Not a security boundary - the same
    ///     private key sits in the <c>.key</c> next to it - a PFX simply always carries one.
    /// </summary>
    public const string PfxPassword = "pin";

    /// <summary>An issued certificate is replaced once less of its validity is left than this.</summary>
    public const int RenewalPeriodDays = 30;

    /// <summary>How far in the past a new certificate starts, so a slightly slow clock still finds it valid.</summary>
    public const int NotBeforeSkewMinutes = 10;

    /// <summary>
    ///     Picks the certificate for a server: the configured <c>.pfx</c> if there is one, an issued or
    ///     reused one when the advertised address needs it, and nothing at all when it does not - which
    ///     leaves Kestrel's development certificate in charge, the setup every local server has always had.
    /// </summary>
    /// <remarks>
    ///     Never throws: a key that cannot be created (read-only output folder, no crypto provider) costs a
    ///     server its own certificate, not its hosts, so the failure is logged and the development
    ///     certificate serves instead.
    /// </remarks>
    /// <param name="advertiseHttps">Whether https is advertised at all.</param>
    /// <param name="autoIssue">Whether PIN is allowed to issue a certificate of its own.</param>
    /// <param name="configuredPath">Path of a <c>.pfx</c>/<c>.p12</c> to serve instead, if any.</param>
    /// <param name="configuredPassword">Its password, if it has one.</param>
    /// <param name="storePath">Where PIN's own key and certificate live; empty for <c>certs</c> under the binary.</param>
    /// <param name="advertisedHost">The address clients are told to dial.</param>
    /// <returns>The certificate to serve; <see cref="TlsCertificate.HasCertificate"/> is <c>false</c> when there is nothing of ours.</returns>
    public static TlsCertificate Resolve(bool advertiseHttps, bool autoIssue, string configuredPath, string configuredPassword, string storePath, string advertisedHost)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return LoadConfigured(configuredPath, configuredPassword, advertisedHost) ??
                   new TlsCertificate(null, advertisedHost, false, null, null);
        }

        if (!ShouldIssue(advertiseHttps, autoIssue, advertisedHost))
        {
            return new TlsCertificate(null, advertisedHost, false, null, null);
        }

        var directory = ResolveDirectory(storePath);
        try
        {
            return IssueOrReuse(advertisedHost, directory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not issue a TLS certificate for {Host} into {Directory} - serving the development certificate instead, which a client dialling that address cannot validate", advertisedHost, directory);
            return new TlsCertificate(null, advertisedHost, false, null, null);
        }
    }

    /// <summary>
    ///     Whether this server needs a certificate of its own: https is advertised, issuing is not switched
    ///     off, and the advertised address is not one that only ever means the machine the server runs on -
    ///     the development certificate already covers that, and has covered every local setup since forever.
    /// </summary>
    /// <param name="advertiseHttps">Whether https is advertised at all.</param>
    /// <param name="autoIssue">Whether PIN is allowed to issue a certificate of its own.</param>
    /// <param name="advertisedHost">The address clients are told to dial.</param>
    /// <returns><c>true</c> when <see cref="Resolve"/> issues or reuses a certificate.</returns>
    public static bool ShouldIssue(bool advertiseHttps, bool autoIssue, string advertisedHost) =>
        advertiseHttps && autoIssue && !ServerAddress.IsLoopback(advertisedHost);

    /// <summary>
    ///     Loads the <c>.pfx</c>/<c>.p12</c> a server was configured with.
    /// </summary>
    /// <param name="path">Path of the PKCS#12 file.</param>
    /// <param name="password">Its password, if it has one.</param>
    /// <param name="advertisedHost">The address clients dial, which the certificate has to name.</param>
    /// <returns>The certificate, or <c>null</c> when the file is missing or unreadable.</returns>
    public static TlsCertificate LoadConfigured(string path, string password, string advertisedHost)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Log.Error("Firefall:Certificate:Path points at {Path}, which does not exist - serving the development certificate instead", path);
            return null;
        }

        try
        {
            var loaded = X509CertificateLoader.LoadPkcs12FromFile(path, password ?? string.Empty);
            if (!Covers(loaded, advertisedHost))
            {
                // Not fatal: a client that reaches the server by another name, or that does not validate the
                // certificate at all, keeps working - the log says why the others do not.
                Log.Warning("The certificate at {Path} does not name {Host} in its subject alternative name, so a client dialling that address will refuse the TLS connection", path, advertisedHost);
            }

            return new TlsCertificate(loaded, advertisedHost, false, null, path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load the TLS certificate from {Path} - serving the development certificate instead", path);
            return null;
        }
    }

    /// <summary>
    ///     Creates a self-signed certificate for one address, in the shape of the ASP.NET Core development
    ///     certificate - self-signed, its own trust anchor, server authentication, RSA - so a client that
    ///     accepts that one on <c>localhost</c> accepts this one on the advertised address. The only
    ///     difference is the one that matters: this certificate names the address the client dialled.
    /// </summary>
    /// <param name="host">The address clients dial; <c>localhost</c> and the loopback addresses are added to it.</param>
    /// <returns>The fresh certificate, carrying its private key. The caller disposes it.</returns>
    public static X509Certificate2 CreateFor(string host)
    {
        using var key = RSA.Create(RsaKeySize);
        return CreateFor(host, key);
    }

    /// <summary>
    ///     The same, with a key the caller owns: the private half of that one is what gets written to disk, so
    ///     the file is the key the certificate was made from rather than one reached back through a certificate
    ///     context - whose exportability depends on how this platform chose to store it.
    /// </summary>
    /// <param name="host">The address clients dial.</param>
    /// <param name="key">The RSA key pair to build the certificate around; the caller keeps and disposes it.</param>
    /// <returns>The fresh certificate, carrying the private key.</returns>
    public static X509Certificate2 CreateFor(string host, RSA key)
    {
        var request = new CertificateRequest(new X500DistinguishedName($"CN={CommonNameFor(host)}"), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Everything a TLS server certificate has to claim, as extensions: the BasicConstraints of a trust
        // anchor of its own, KeyUsage for signing the handshake and for signing as that anchor
        // (KeyCertSign), and the enhanced key usage that says "this one is for server authentication".
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyCertSign, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(ServerAuthenticationOid) }, false));

        var alternativeNames = new SubjectAlternativeNameBuilder();
        foreach (var name in DnsNamesFor(host))
        {
            alternativeNames.AddDnsName(name);
        }

        foreach (var address in IpAddressesFor(host))
        {
            alternativeNames.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(alternativeNames.Build());

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-NotBeforeSkewMinutes);
        return request.CreateSelfSigned(notBefore, notBefore.AddYears(ValidityYears));
    }

    /// <summary>
    ///     Whether a certificate names an address: the check a client makes against whatever it dialled, and
    ///     therefore the check PIN makes before it trusts a certificate it found on disk or was handed as
    ///     configuration.
    /// </summary>
    /// <param name="certificate">The certificate to inspect.</param>
    /// <param name="host">The address clients dial.</param>
    /// <returns><c>true</c> when the subject alternative name - or the subject, when there is no SAN - names the host.</returns>
    public static bool Covers(X509Certificate2 certificate, string host)
    {
        if (certificate == null || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var address = ServerAddress.AddressHost(host);
        var alternativeNames = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        if (alternativeNames == null)
        {
            // A hand-made certificate with no SAN at all is matched on its subject, which is what a client
            // without SAN support falls back to as well.
            return certificate.Subject.Contains(address, StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            return IPAddress.TryParse(address, out var parsed)
                       ? alternativeNames.EnumerateIPAddresses().Any(certificateAddress => certificateAddress.Equals(parsed))
                       : alternativeNames.EnumerateDnsNames().Any(name => string.Equals(name, address, StringComparison.OrdinalIgnoreCase));
        }
        catch (CryptographicException)
        {
            // An extension that cannot be parsed names nothing.
            return false;
        }
    }

    /// <summary>Where PIN keeps its own certificates: the configured store, or <c>certs</c> under the binary.</summary>
    /// <param name="storePath">The configured path; empty for the default.</param>
    /// <returns>The directory the <c>pin-&lt;host&gt;.*</c> files live in.</returns>
    public static string ResolveDirectory(string storePath) =>
        string.IsNullOrWhiteSpace(storePath)
            ? Path.Combine(AppContext.BaseDirectory, DefaultDirectoryName)
            : storePath;

    /// <summary>
    ///     Loads the certificate PIN keeps in its store, issuing it when that store has nothing usable. The
    ///     files are reused as long as the certificate still names the advertised address and has most of its
    ///     validity left, so what players installed once survives a restart - and an address change writes a
    ///     second set instead of invalidating the first.
    /// </summary>
    private static TlsCertificate IssueOrReuse(string host, string directory)
    {
        if (!Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var stem = Path.Combine(directory, $"{FileNamePrefix}{ServerAddress.FileNameFor(host)}");
        var keyPath = stem + ".key";
        var certificatePath = stem + ".crt";
        var playerPath = stem + ".cer";
        var pfxPath = stem + PfxExtension;

        var reusable = TryLoadReusable(pfxPath, keyPath, certificatePath, host);
        if (reusable != null)
        {
            Log.Debug("Reusing the TLS certificate for {Host} at {Path}", host, certificatePath);
            return new TlsCertificate(reusable, host, true, keyPath, playerPath);
        }

        using var key = RSA.Create(RsaKeySize);
        using var created = CreateFor(host, key);
        File.WriteAllText(keyPath, Pem.Encode(Pem.PrivateKeyLabel, key.ExportPkcs8PrivateKey()));

        File.WriteAllBytes(playerPath, created.RawData);
        File.WriteAllText(certificatePath, Pem.Encode(Pem.CertificateLabel, created.RawData));

        // The .key/.crt pair is for people and tools; the hosts serve from the .pfx. That is the container
        // whose key Schannel on Windows can actually sign a handshake with - X509Certificate2.CreateFromPemFile
        // loads a key SChannel cannot use, so serving from the PEM pair would drop every TLS connection right
        // after the client hello (what the game shows as a red blink at the login box). See TryLoadReusable.
        File.WriteAllBytes(pfxPath, created.Export(X509ContentType.Pkcs12, PfxPassword));
        TryRestrictToThisUser(keyPath);
        TryRestrictToThisUser(pfxPath);

        // Issuing happens once per advertised address, and the one thing a server owner has to pass on to the
        // players is in it, so it goes to the level the default configuration shows.
        Log.Warning("Issued a TLS certificate for {Host} (valid until {NotAfter:yyyy-MM-dd}) into {Directory}. Players must trust its public half - {PlayerPath}, downloadable over plain http at {Route} - before their client accepts the https URLs this server advertises; WebHostManager --trust-cert does it on this machine", host, created.NotAfter, directory, playerPath, TlsCertificate.CerRoute);

        // Read back what is on disk rather than keeping the fresh object: the hosts then serve exactly the
        // certificate the next start will find again, and a file edited in between stays the source of truth.
        return new TlsCertificate(X509CertificateLoader.LoadPkcs12FromFile(pfxPath, PfxPassword), host, true, keyPath, playerPath);
    }

    /// <summary>
    ///     The certificate from a previous start, when it can still serve this address. Loaded from the PKCS#12
    ///     (the only container whose key Schannel on Windows accepts - see <see cref="IssueOrReuse"/>), with a
    ///     PEM pair written by an older version migrated into one when there is no PFX yet.
    /// </summary>
    private static X509Certificate2 TryLoadReusable(string pfxPath, string keyPath, string certificatePath, string host)
    {
        if (File.Exists(pfxPath))
        {
            try
            {
                return ValidateForServing(X509CertificateLoader.LoadPkcs12FromFile(pfxPath, PfxPassword), host, certificatePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "The certificate at {Path} could not be read; a new one is being issued", certificatePath);
                return null;
            }
        }

        // A store written before the PFX existed: reuse the very certificate, moved into the container the
        // TLS stack can serve from. Failing that (or a pair that no longer names the address), issue afresh.
        if (File.Exists(keyPath) && File.Exists(certificatePath))
        {
            try
            {
                using var pem = X509Certificate2.CreateFromPemFile(certificatePath, keyPath);
                if (ValidateForServing(pem, host, certificatePath) == null)
                {
                    return null;
                }

                File.WriteAllBytes(pfxPath, pem.Export(X509ContentType.Pkcs12, PfxPassword));
                TryRestrictToThisUser(pfxPath);

                // The checks already passed on the very certificate, and a PFX export cannot drop the key they
                // passed on; load the container the TLS stack serves from and return it directly.
                return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, PfxPassword);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "The certificate pair at {Path} could not be read; a new one is being issued", certificatePath);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     The checks every reused certificate has to pass, whatever container it was loaded from: it carries its
    ///     key, it has most of its validity left, and it names the address clients dial. A certificate that fails
    ///     one of these is disposed and <c>null</c> is returned so the caller issues a replacement.
    /// </summary>
    private static X509Certificate2 ValidateForServing(X509Certificate2 loaded, string host, string certificatePath)
    {
        if (!loaded.HasPrivateKey)
        {
            Log.Warning("The certificate at {Path} came without its key and cannot serve TLS; a new one is being issued", certificatePath);
            loaded.Dispose();
            return null;
        }

        if (loaded.NotAfter <= DateTime.UtcNow.AddDays(RenewalPeriodDays))
        {
            Log.Warning("The certificate for {Host} at {Path} expires on {NotAfter:yyyy-MM-dd}; a new one is being issued", host, certificatePath, loaded.NotAfter);
            loaded.Dispose();
            return null;
        }

        if (!Covers(loaded, host))
        {
            Log.Warning("The certificate at {Path} does not name {Host}; the advertised address changed, so a new one is being issued", certificatePath, host);
            loaded.Dispose();
            return null;
        }

        return loaded;
    }

    /// <summary>Names a client may dial this server by: the advertised address and <c>localhost</c>.</summary>
    private static IEnumerable<string> DnsNamesFor(string host)
    {
        yield return "localhost";
        var address = ServerAddress.AddressHost(host);
        if (address.Length > 0 && !IPAddress.TryParse(address, out _))
        {
            yield return address;
        }
    }

    /// <summary>Addresses a client may dial this server by: the advertised one, plus the loopback ones.</summary>
    private static IEnumerable<IPAddress> IpAddressesFor(string host)
    {
        yield return IPAddress.Loopback;
        yield return IPAddress.IPv6Loopback;
        if (IPAddress.TryParse(ServerAddress.AddressHost(host), out var address) && !IPAddress.IsLoopback(address))
        {
            yield return address;
        }
    }

    /// <summary>The certificate's common name: the address, kept to the characters a distinguished name may hold.</summary>
    private static string CommonNameFor(string host) => ServerAddress.FileNameFor(host).Replace('_', '-');

    /// <summary>Makes the private key unreadable to anybody but its owner, on systems that have that notion.</summary>
    private static void TryRestrictToThisUser(string path)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not restrict the file mode of {Path}", path);
        }
    }
}
