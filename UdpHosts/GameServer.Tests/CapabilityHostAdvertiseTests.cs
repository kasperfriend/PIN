using System.Threading.Tasks;
using Shared.Web.Config;
using WebHost.OperatorApi.Capability;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The host list a client builds its own URLs from. Everything PIN has not implemented may be pointed at
///     the catch-all, but the asset stream may not: a client told to fetch its high-resolution texture chunks
///     from a server that serves nothing renders the game blurry and the log fills with NotFound lines, which
///     is a configuration answer, not a data one. See <c>Docs/ASSETS.md</c>.
/// </summary>
public class CapabilityHostAdvertiseTests
{
    [Fact]
    public async Task WebAssetHost_IsAdvertisedAsTheAssetHostAndNotTheCatchAll()
    {
        var config = new Firefall();

        var hosts = await new CapabilityRepository(config).GetHostInformationAsync("prod", 1962);

        Assert.Equal("https://localhost:44301", hosts.WebAssetHost);
        Assert.NotEqual(hosts.WebHost, hosts.WebAssetHost);
    }

    [Fact]
    public async Task UnimplementedHosts_StillLandOnTheCatchAll()
    {
        var config = new Firefall();

        var hosts = await new CapabilityRepository(config).GetHostInformationAsync("prod", 1962);

        Assert.Equal("https://localhost:44399", hosts.MarketHost);
        Assert.Equal("https://localhost:44399", hosts.WebAccountsHost);
    }

    [Fact]
    public async Task AdvertisedPorts_FollowTheConfiguredUrls()
    {
        var config = new Firefall
                     {
                         PublicHost = "26.11.22.33",
                         AdvertiseHttps = false,
                     };

        config.WebHosts["WebHost.WebAsset"] = new WebHost { Urls = "https://*:44301;http://*:4401" };

        var hosts = await new CapabilityRepository(config).GetHostInformationAsync("prod", 1962);

        Assert.Equal("http://26.11.22.33:4401", hosts.WebAssetHost);
    }
}
