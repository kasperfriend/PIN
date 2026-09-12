using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shared.Common;
using Shared.Common.Certificates;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the certificate PIN issues for the address it advertises
///     (<see cref="TlsCertificateStore"/>), and for the two judgements that decide whether it has to:
///     "does this address only mean this machine?" (<see cref="ServerAddress"/>) and "does this certificate
///     name the address a client dials?" (<see cref="TlsCertificateStore.Covers"/>).
///
///     Worth pinning down because the failure each one prevents is a client that simply will not connect, and
///     both look like a network problem from outside: the game client refuses an oracle URL that is not
///     https, and it refuses a TLS endpoint whose certificate does not name the address it dialled.
/// </summary>
public class TlsCertificateStoreTests : IDisposable
{
    /// <summary>An address in the range RadminVPN hands out - what a player on another machine dials.</summary>
    private const string RemoteHost = "26.84.248.2";

    private readonly string _storePath;

    public TlsCertificateStoreTests()
    {
        _storePath = Path.Combine(Path.GetTempPath(), "pin-tls-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storePath))
        {
            Directory.Delete(_storePath, true);
        }
    }

    [Theory]
    // Addresses that only ever mean "the machine the server runs on": ASP.NET Core's development certificate
    // already covers them, and issuing over the top of it would take a working local setup away.
    [InlineData("localhost", false)]
    [InlineData("LOCALHOST", false)]
    [InlineData("localhost.", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("[::1]", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    // Everything else is an address a certificate has to be issued for.
    [InlineData(RemoteHost, true)]
    [InlineData("192.168.1.50", true)]
    [InlineData("pin.example.com", true)]
    public void ShouldIssueFollowsTheAdvertisedAddress(string host, bool needsOwnCertificate)
    {
        Assert.Equal(needsOwnCertificate, TlsCertificateStore.ShouldIssue(advertiseHttps: true, autoIssue: true, advertisedHost: host));
    }

    [Fact]
    public void ShouldIssueStaysOutOfTheWayOfHttpAndOfAnOperatorSuppliedCertificate()
    {
        // Advertising plain http, there is no TLS to provide a certificate for - the client's own oracle
        // check is what stops the player, and PublicUrls.ClientWarning is what says so in the log.
        Assert.False(TlsCertificateStore.ShouldIssue(advertiseHttps: false, autoIssue: true, advertisedHost: RemoteHost));

        // Firefall:Certificate:AutoIssue = false is the escape hatch for somebody serving TLS through
        // something else entirely who does not want a key appearing next to the binary.
        Assert.False(TlsCertificateStore.ShouldIssue(advertiseHttps: true, autoIssue: false, advertisedHost: RemoteHost));
    }

    [Fact]
    public void ResolveLeavesALocalhostServerWithTheDevelopmentCertificate()
    {
        var certificate = Resolve("localhost");

        Assert.False(certificate.HasCertificate);
        Assert.False(certificate.SelfIssued);
        Assert.Equal(string.Empty, certificate.TrustInstructions("http://localhost:4400"));

        // Nothing was written anywhere: a local server does not get a key file it has no use for.
        Assert.False(Directory.Exists(_storePath));
    }

    [Fact]
    public void ResolveIssuesACertificateThatNamesTheAdvertisedAddress()
    {
        var certificate = Resolve();

        Assert.True(certificate.HasCertificate);
        Assert.True(certificate.SelfIssued);
        Assert.True(certificate.Certificate.HasPrivateKey, "a certificate Kestrel cannot sign with is not a TLS certificate");
        Assert.True(TlsCertificateStore.Covers(certificate.Certificate, RemoteHost));
        Assert.True(
            TlsCertificateStore.Covers(certificate.Certificate, "localhost"),
            "a player's own ini entry point has to be served by the same certificate, whatever the server advertises");
        Assert.False(TlsCertificateStore.Covers(certificate.Certificate, "10.0.0.5"), "a certificate names addresses, it is not a wildcard");

        // The shape of the ASP.NET Core development certificate: its own trust anchor, usable for server
        // authentication - so a machine that trusts the one can be pointed at the other.
        var enhancedKeyUsage = certificate.Certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
        Assert.Contains(enhancedKeyUsage.EnhancedKeyUsages.Cast<Oid>(), oid => oid.Value == TlsCertificateStore.ServerAuthenticationOid);
        Assert.True(certificate.Certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);

        // Three files, and only the public half of them is ever handed to a player.
        Assert.True(File.Exists(certificate.PrivateKeyPath));
        Assert.Equal($"pin-{RemoteHost}.cer", Path.GetFileName(certificate.CertificatePath));
        Assert.Equal($"pin-{RemoteHost}.cer", certificate.DownloadFileName);
        Assert.EndsWith(".key", certificate.PrivateKeyPath);
        Assert.Equal(certificate.Certificate.RawData, File.ReadAllBytes(certificate.CertificatePath));
    }

    [Fact]
    public void ResolveReusesTheCertificateOnDiskAcrossRestarts()
    {
        var first = Resolve();
        var second = Resolve();

        // Players installed a certificate; quietly replacing it at the next start would put every one of them
        // back at the login screen, so what is on disk decides, not what this process could make.
        Assert.Equal(first.Certificate.Thumbprint, second.Certificate.Thumbprint);
        Assert.True(second.SelfIssued);
    }

    [Fact]
    public void ResolveGivesAChangedAdvertisedAddressItsOwnCertificate()
    {
        var issued = Resolve();
        var other = Resolve("192.168.1.50");

        // The common case is a VPN that handed out a new address: the players of the old address keep the
        // certificate they already trust, because it is a different file.
        Assert.NotEqual(issued.Certificate.Thumbprint, other.Certificate.Thumbprint);
        Assert.True(TlsCertificateStore.Covers(other.Certificate, "192.168.1.50"));
        Assert.Equal(2, Directory.GetFiles(_storePath, "pin-*.crt").Length);
    }

    [Fact]
    public void ResolveRefusesToReuseACertificateThatNoLongerNamesTheAddress()
    {
        var forRemote = Resolve();
        var forOther = Resolve("10.11.12.13");

        // The pair sitting under 10.11.12.13's file names is the one for 26.84.248.2 - what a copied-in or
        // hand-edited certificate looks like. Reusing it would advertise https on an address the certificate
        // does not name, which every validating client refuses, so the store has to issue instead.
        var stem = Path.Combine(_storePath, $"pin-{ServerAddress.FileNameFor("10.11.12.13")}");
        File.Copy(Path.ChangeExtension(forRemote.CertificatePath, ".crt"), stem + ".crt", true);
        File.Copy(forRemote.PrivateKeyPath, stem + ".key", true);

        var resolved = Resolve("10.11.12.13");

        Assert.NotEqual(forOther.Certificate.Thumbprint, resolved.Certificate.Thumbprint);
        Assert.True(TlsCertificateStore.Covers(resolved.Certificate, "10.11.12.13"));
    }

    [Fact]
    public void AConfiguredCertificateIsServedAsItIs()
    {
        // A certificate the operator made for the advertised address: PIN serves it, does not claim to have
        // issued it, and does not touch a trust store for it.
        using var created = TlsCertificateStore.CreateFor(RemoteHost);
        _ = Directory.CreateDirectory(_storePath);
        var path = Path.Combine(_storePath, "operator.pfx");
        File.WriteAllBytes(path, created.Export(X509ContentType.Pkcs12, "operator-password"));

        var configured = TlsCertificateStore.LoadConfigured(path, "operator-password", RemoteHost);

        Assert.NotNull(configured);
        Assert.False(configured.SelfIssued);
        Assert.Equal(path, configured.CertificatePath);
        Assert.True(configured.Certificate.HasPrivateKey);
        Assert.True(TlsCertificateStore.Covers(configured.Certificate, RemoteHost));
        Assert.Contains("configured", Describe(configured));

        // The same file on a server advertising something else: usable, but not for a client that validates.
        using var forLocalhost = TlsCertificateStore.CreateFor("localhost");
        Assert.False(TlsCertificateStore.Covers(forLocalhost, RemoteHost));
    }

    [Fact]
    public void LoadConfiguredReportsAMissingFileInsteadOfThrowing()
    {
        Assert.Null(TlsCertificateStore.LoadConfigured(Path.Combine(_storePath, "nope.pfx"), string.Empty, RemoteHost));
        Assert.Null(TlsCertificateStore.LoadConfigured(string.Empty, string.Empty, RemoteHost));
    }

    [Fact]
    public void ResolveFallsBackToTheDevelopmentCertificateWhenAConfiguredFileIsGone()
    {
        var resolved = TlsCertificateStore.Resolve(
            advertiseHttps: true, autoIssue: true, configuredPath: Path.Combine(_storePath, "missing.pfx"), configuredPassword: string.Empty,
            storePath: _storePath, advertisedHost: RemoteHost);

        Assert.False(resolved.HasCertificate);
    }

    [Theory]
    [InlineData("26.84.248.2", "26.84.248.2", true)]
    [InlineData("26.84.248.2", "10.0.0.1", false)]
    [InlineData("26.84.248.2", "26.84.248.2.", true)]
    [InlineData("pin.example.com", "PIN.example.com", true)]
    [InlineData("pin.example.com", "pin.example.com.", true)]
    [InlineData("pin.example.com", "other.example.com", false)]
    [InlineData("pin.example.com", "localhost", true)]
    [InlineData("[2001:db8::1]", "2001:db8::1", true)]
    [InlineData("2001:db8::1", "[2001:db8::1]", true)]
    public void CoversAcceptsTheFormsACanBeDialledIn(string issuedFor, string dialed, bool covered)
    {
        using var certificate = TlsCertificateStore.CreateFor(issuedFor);

        Assert.Equal(covered, TlsCertificateStore.Covers(certificate, dialed));
    }

    [Fact]
    public void CoversNeedsBothACertificateAndAnAddress()
    {
        using var certificate = TlsCertificateStore.CreateFor(RemoteHost);

        Assert.False(TlsCertificateStore.Covers(null, RemoteHost));
        Assert.False(TlsCertificateStore.Covers(certificate, null));
        Assert.False(TlsCertificateStore.Covers(certificate, "  "));
    }

    [Fact]
    public void PemDocumentsAreBannersAroundSixtyFourCharacterLines()
    {
        using var certificate = TlsCertificateStore.CreateFor(RemoteHost);
        var pem = Pem.Encode(Pem.CertificateLabel, certificate.RawData);

        // Split on newlines and trim the carriage return: the file is written on Windows too, where
        // AppendLine means CRLF and a client-side reader has to live with it.
        var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
        Assert.Equal($"-----BEGIN {Pem.CertificateLabel}-----", lines[0]);
        Assert.Equal($"-----END {Pem.CertificateLabel}-----", lines[^1]);
        Assert.All(lines[1..^1], line => Assert.True(line.Length is > 0 and <= 64, $"PEM body line is {line.Length} characters: {line}"));

        // Reading it back is the point: the hosts load the pair with X509Certificate2.CreateFromPemFile, so a
        // document only one tool understands is a server that cannot start a second time.
        using var reloaded = X509Certificate2.CreateFromPem(pem);
        Assert.Equal(certificate.Thumbprint, reloaded.Thumbprint);
    }

    [Fact]
    public void EncodeRefusesToWriteAnEmptyDocument()
    {
        Assert.Throws<ArgumentException>(() => Pem.Encode(Pem.CertificateLabel, Array.Empty<byte>()));
        Assert.Throws<ArgumentNullException>(() => Pem.Encode(null, new byte[] { 1 }));
    }

    [Fact]
    public void InstructionsTellPlayersWhereToGetThePublicHalfAndNotTheKey()
    {
        var certificate = Resolve();

        var instructions = certificate.TrustInstructions($"http://{RemoteHost}:4400");
        Assert.Contains($"http://{RemoteHost}:4400{TlsCertificate.CerRoute}", instructions);
        Assert.DoesNotContain(certificate.PrivateKeyPath, instructions);

        Assert.NotEmpty(certificate.PublicCertificateDer);
        Assert.StartsWith("-----BEGIN CERTIFICATE-----", certificate.PublicCertificatePem);
        Assert.DoesNotContain("PRIVATE KEY", certificate.PublicCertificatePem);
    }

    [Fact]
    public void TrustingNothingExplainsWhatToChangeInsteadOfFailing()
    {
        Assert.False(new TlsCertificate(null, "localhost", false, null, null).TryTrustOnThisMachine(false, out var loopback));
        Assert.Contains("dev-certs", loopback);

        Assert.False(new TlsCertificate(null, RemoteHost, false, null, null).TryTrustOnThisMachine(false, out var plainHttp));
        Assert.Contains("AdvertiseHttps", plainHttp);

        // A configured certificate is its owner's business: PIN never writes a trust store for one it did
        // not issue, whatever platform this runs on.
        using var created = TlsCertificateStore.CreateFor(RemoteHost);
        var configured = new TlsCertificate(created, RemoteHost, selfIssued: false, null, "C:\\pin.pfx");
        Assert.False(configured.TryTrustOnThisMachine(false, out var notOurs));
        Assert.Contains("configured", notOurs);
    }

    [Fact]
    public void FileNamesAreLegalForTheAddressesTheyAreBuiltFrom()
    {
        Assert.Equal("26.84.248.2", ServerAddress.FileNameFor(RemoteHost));
        Assert.Equal("2001_db8__1", ServerAddress.FileNameFor("[2001:db8::1]"));
        Assert.Equal("localhost", ServerAddress.FileNameFor(null));
        Assert.Equal("pin.example.com", ServerAddress.FileNameFor(" pin.example.com. "));
    }

    private TlsCertificate Resolve() => Resolve(RemoteHost);

    /// <summary>The whole flow, against a throwaway <c>certs</c> folder in place of the one under the binary.</summary>
    private TlsCertificate Resolve(string advertisedHost) =>
        TlsCertificateStore.Resolve(
            advertiseHttps: true, autoIssue: true, configuredPath: string.Empty, configuredPassword: string.Empty,
            storePath: _storePath, advertisedHost: advertisedHost);

    /// <summary>What <c>--trust-cert</c> would answer about this certificate, without touching a trust store.</summary>
    private static string Describe(TlsCertificate certificate)
    {
        _ = certificate.TryTrustOnThisMachine(false, out var message);
        return message;
    }
}
