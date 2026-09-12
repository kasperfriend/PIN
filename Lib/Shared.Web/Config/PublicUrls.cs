using System;
using System.Collections.Generic;

namespace Shared.Web.Config;

/// <summary>
///     Builds the addresses PIN advertises to the clients it serves.
/// </summary>
/// <remarks>
///     Listening and advertising are two different things. The UDP servers bind
///     <see cref="System.Net.IPAddress.Any" /> and the Kestrel hosts listen on whatever their
///     <c>Firefall:WebHosts:&lt;host&gt;:urls</c> entry says, but the client learns *where to connect*
///     from the capability response (<c>GET /check</c>) and from the oracle ticket. Both used to
///     hardcode <c>localhost</c>, which resolves to the machine the client runs on - so a player on a
///     second machine was sent to himself and could never reach the server, however open its sockets
///     were. <see cref="Firefall.PublicHost" /> is now the single place that decides which hostname or IP
///     is handed out, and the port of each advertised URL is taken from that host's configured bind URLs
///     so the configuration stays the only source of truth for ports (bind to <c>*</c> or <c>0.0.0.0</c>
///     and advertise a routable host; the two never have to agree).
/// </remarks>
public class PublicUrls
{
    /// <summary>Config key of the catch-all host, which answers for the hosts PIN has not implemented.</summary>
    public const string CatchAllHost = "WebHost.CatchAll";

    /// <summary>Config key of the chat host.</summary>
    public const string ChatHost = "WebHost.Chat";

    /// <summary>Config key of the client api host.</summary>
    public const string ClientApiHost = "WebHost.ClientApi";

    /// <summary>Config key of the ingame api host.</summary>
    public const string InGameApiHost = "WebHost.InGameApi";

    /// <summary>Config key of the operator api host (the entry point named by <c>firefall.ini</c>).</summary>
    public const string OperatorApiHost = "WebHost.OperatorApi";

    /// <summary>Config key of the web asset host (the asset stream named by <c>firefall.ini</c>).</summary>
    public const string WebAssetHost = "WebHost.WebAsset";

    /// <summary>Host advertised when <see cref="Firefall.PublicHost" /> is empty: the server's own machine.</summary>
    private const string DefaultHost = "localhost";

    /// <summary>MatrixServer UDP port used when the configuration carries none.</summary>
    private const ushort DefaultMatrixPort = 25000;

    /// <summary>
    ///     Documented fallback ports per host, mirroring the defaults of <c>appsettings.json</c>. Only
    ///     consulted when a host has no usable URLs configured; unknown keys fall back to the catch-all
    ///     pair, which is the host that answers for everything PIN has not implemented.
    /// </summary>
    private static readonly Dictionary<string, (int Http, int Https)> DefaultPorts = new()
    {
        [OperatorApiHost] = (4400, 44300),
        [WebAssetHost] = (4401, 44301),
        [ClientApiHost] = (4402, 44302),
        [InGameApiHost] = (4403, 44303),
        [ChatHost] = (4407, 44307),
        [CatchAllHost] = (4499, 44399),
    };

    private readonly Firefall _config;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PublicUrls" /> class.
    /// </summary>
    /// <param name="config">
    ///     The <c>Firefall</c> configuration section. A missing section falls back to the defaults, i.e.
    ///     to advertising <c>localhost</c> over https.
    /// </param>
    public PublicUrls(Firefall config)
    {
        _config = config ?? new Firefall();
    }

    /// <summary>Hostname or IP handed to clients; never empty.</summary>
    public string Host => string.IsNullOrWhiteSpace(_config.PublicHost) ? DefaultHost : _config.PublicHost.Trim();

    /// <summary>Whether the advertised URLs use https.</summary>
    public bool UseHttps => _config.AdvertiseHttps;

    /// <summary>URI scheme of the advertised URLs, <c>https</c> or <c>http</c>.</summary>
    public string Scheme => UseHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;

    /// <summary>MatrixServer UDP port advertised in the oracle ticket.</summary>
    public ushort MatrixPort => _config.MatrixPort == 0 ? DefaultMatrixPort : _config.MatrixPort;

    /// <summary>
    ///     Matrix address in the <c>host:port</c> form the oracle ticket carries. The MatrixServer is a UDP
    ///     server, so there is no scheme - and no IP either: the client dials the GameServer at this same
    ///     address, using the port the MatrixServer's HUGG reply hands it.
    /// </summary>
    public string MatrixAddress => $"{Host}:{MatrixPort}";

    /// <summary>
    ///     Builds the URL a client should use for one of the web hosts.
    /// </summary>
    /// <param name="webHost">Config key of the host, e.g. <see cref="ClientApiHost" />.</param>
    /// <param name="path">Optional path to append, e.g. <c>/production-1973</c>.</param>
    /// <returns>
    ///     An absolute URL built from <see cref="Host" />, <see cref="Scheme" /> and the port resolved by
    ///     <see cref="Port" />, e.g. <c>https://26.1.2.3:44302</c>.
    /// </returns>
    public string Url(string webHost, string path = "") => $"{Scheme}://{Host}:{Port(webHost)}{path}";

    /// <summary>
    ///     Resolves the port a client should use for one of the web hosts.
    /// </summary>
    /// <param name="webHost">Config key of the host, e.g. <see cref="ClientApiHost" />.</param>
    /// <returns>
    ///     The port of the configured URL whose scheme matches <see cref="Scheme" />, or the documented
    ///     default for that host when the configuration has no such URL.
    /// </returns>
    public ushort Port(string webHost)
    {
        if (_config.WebHosts != null &&
            _config.WebHosts.TryGetValue(webHost, out var host) &&
            host != null &&
            !string.IsNullOrWhiteSpace(host.Urls))
        {
            foreach (var candidate in host.Urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // The bind address itself ("*", "0.0.0.0", "localhost") is irrelevant here - only the port
                // is taken from the configuration, the host part always comes from PublicHost.
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                    !uri.IsDefaultPort &&
                    string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    return (ushort)uri.Port;
                }
            }
        }

        var ports = DefaultPorts.TryGetValue(webHost, out var configured) ? configured : DefaultPorts[CatchAllHost];
        return (ushort)(UseHttps ? ports.Https : ports.Http);
    }
}