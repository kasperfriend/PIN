using Shared.Web.Config;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for what a client is told (<see cref="PublicUrls"/>) - the addresses the capability response and
///     the oracle ticket hand out, and the warning that says when a configuration cannot work.
///
///     The warning matters as much as the URLs: the client's refusal of a plain http oracle URL reads like a
///     broken server, and the whole point of <c>Firefall:AdvertiseHttps</c> looked like a way to avoid
///     certificates. Pinning the text of the port resolution is what keeps a remote player's
///     <c>https://&lt;advertised host&gt;:&lt;the port actually bound&gt;</c> from drifting back into a
///     hardcoded literal.
/// </summary>
public class PublicUrlsTests
{
    private const string RemoteHost = "26.84.248.2";

    [Fact]
    public void AdvertisedUrlsFollowTheSchemeAndTakeTheirPortsFromTheBindConfiguration()
    {
        var urls = new PublicUrls(Config(RemoteHost, advertiseHttps: true));

        Assert.Equal($"https://{RemoteHost}:44302", urls.Url(PublicUrls.ClientApiHost));
        Assert.Equal($"https://{RemoteHost}:44303", urls.Url(PublicUrls.InGameApiHost));
        Assert.Equal($"https://{RemoteHost}:44307", urls.Url(PublicUrls.ChatHost));

        // The plain http port is still worth naming in a log line - the hosts answer on it either way, which
        // is how a player fetches the certificate he needs before his client will accept the https one.
        Assert.Equal($"http://{RemoteHost}:4402", urls.PlainUrl(PublicUrls.ClientApiHost));
        Assert.Equal($"http://{RemoteHost}:4400", urls.PlainUrl(PublicUrls.OperatorApiHost));
    }

    [Fact]
    public void PlainHttpAdvertisingUsesTheHttpPortsAndStillTheAdvertisedHost()
    {
        var urls = new PublicUrls(Config(RemoteHost, advertiseHttps: false));

        Assert.Equal($"http://{RemoteHost}:4402", urls.Url(PublicUrls.ClientApiHost));
        Assert.Equal($"https://{RemoteHost}:44302", urls.Url(PublicUrls.ClientApiHost, string.Empty, "https"));
        Assert.Equal($"{RemoteHost}:25000", urls.MatrixAddress);
    }

    [Fact]
    public void AHostWithoutAUrlInTheAdvertisedSchemeFallsBackToTheDocumentedPort()
    {
        var config = Config(RemoteHost, advertiseHttps: true);
        _ = config.WebHosts.TryGetValue(PublicUrls.ClientApiHost, out var clientApi);
        clientApi.Urls = "http://*:4402"; // no TLS endpoint configured for this host at all

        var urls = new PublicUrls(config);

        // Advertising https on a host that only bound http is a server bug, but the answer is still the
        // documented TLS port rather than a zero or the http one: 44302 is where the other hosts listen.
        Assert.Equal(44302, (int)urls.Port(PublicUrls.ClientApiHost));
        Assert.Equal($"http://{RemoteHost}:4402", urls.PlainUrl(PublicUrls.ClientApiHost));
    }

    [Fact]
    public void AnEmptyPublicHostMeansThisMachine()
    {
        var config = Config(string.Empty, advertiseHttps: true);
        var urls = new PublicUrls(config);

        Assert.Equal("localhost", urls.Host);
        Assert.True(urls.IsLoopback);
        Assert.Null(urls.ClientWarning());

        Assert.False(new PublicUrls(Config(RemoteHost, advertiseHttps: true)).IsLoopback);
    }

    [Fact]
    public void AdvertisingPlainHttpIsWarnedAboutBecauseTheClientRefusesIt()
    {
        var insecure = new PublicUrls(Config(RemoteHost, advertiseHttps: false));
        var warning = insecure.ClientWarning();

        // The message has to name the URL the client will quote back, so the operator recognises the symptom.
        Assert.NotNull(warning);
        Assert.Contains($"http://{RemoteHost}:4402/api/v1/oracle/ticket", warning);
        Assert.Contains("not configured for HTTPS", warning);
        Assert.Contains("Firefall:AdvertiseHttps", warning);

        Assert.Null(new PublicUrls(Config(RemoteHost, advertiseHttps: true)).ClientWarning());
    }

    private static Firefall Config(string publicHost, bool advertiseHttps) =>
        new()
        {
            PublicHost = publicHost,
            AdvertiseHttps = advertiseHttps,
            MatrixPort = 25000,
            WebHosts =
            {
                [PublicUrls.OperatorApiHost] = new WebHost { Urls = "https://*:44300;http://*:4400" },
                [PublicUrls.ClientApiHost] = new WebHost { Urls = "https://*:44302;http://*:4402" },
                [PublicUrls.InGameApiHost] = new WebHost { Urls = "https://*:44303;http://*:4403" },
                [PublicUrls.ChatHost] = new WebHost { Urls = "https://*:44307;http://*:4407" },
            },
        };
}
