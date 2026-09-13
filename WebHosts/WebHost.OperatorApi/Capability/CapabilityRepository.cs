using System.Threading.Tasks;
using Shared.Web.Config;
using WebHost.OperatorApi.Exceptions;

namespace WebHost.OperatorApi.Capability;

public class CapabilityRepository : ICapabilityRepository
{
    private readonly PublicUrls _urls;

    public CapabilityRepository(Firefall config)
    {
        _urls = new PublicUrls(config);
    }

    public async Task<HostInformation> GetHostInformationAsync(string environment, int build)
    {
        // The hosts are advertised from Firefall:PublicHost (see PublicUrls) instead of a hardcoded
        // "localhost", so a player on another machine is sent to this server and not to himself. The
        // catch-all host still answers for every host PIN has not implemented - frontend, store, web,
        // market, web accounts and rhsigscan all point at it, exactly as they did before. Web assets do
        // not: they have a host of their own, and a client told to stream from the catch-all is told to
        // stream from a server that serves nothing - every chunk request there is a 404, which is how a
        // player ends up with blurry textures next to a server that has the files.
        return await Task.FromResult(new HostInformation
                                     {
                                         FrontendHost = _urls.Url(PublicUrls.CatchAllHost),
                                         StoreHost = _urls.Url(PublicUrls.CatchAllHost),
                                         ChatServer = _urls.Url(PublicUrls.ChatHost),
                                         ReplayHost = _urls.Url(PublicUrls.CatchAllHost, $"/{environment}-{build}"),
                                         WebHost = _urls.Url(PublicUrls.CatchAllHost),
                                         MarketHost = _urls.Url(PublicUrls.CatchAllHost),
                                         IngameHost = _urls.Url(PublicUrls.InGameApiHost),
                                         ClientapiHost = _urls.Url(PublicUrls.ClientApiHost),
                                         WebAssetHost = _urls.Url(PublicUrls.WebAssetHost),
                                         WebAccountsHost = _urls.Url(PublicUrls.CatchAllHost),
                                         RhsigscanHost = _urls.Url(PublicUrls.CatchAllHost)
                                     });
    }

    public async Task<ProductInformation> GetProductInformationAsync(string productName)
    {
        if (productName != "Firefall_Beta")
        {
            throw new NotFoundException($"Product '{productName}' is unknown");
        }

        return await Task.FromResult(new ProductInformation { Build = "beta-1973", Environment = "production", Region = "NA", PatchLevel = 0 });
    }
}