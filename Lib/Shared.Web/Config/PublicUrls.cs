using System;
using System.Collections.Generic;
using Shared.Common;

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

    /// <summary>
    ///     Whether <see cref="Host"/> only ever means the machine the server itself runs on - in which case
    ///     the client validating the TLS certificate is the machine that made it, and ASP.NET Core's
    ///     development certificate is enough. Anything else (a LAN or VPN address, a hostname) is an address
    ///     a certificate has to be issued <em>for</em>; see <see cref="Shared.Common.Certificates.TlsCertificateStore"/>.
    /// </summary>
    public bool IsLoopback => ServerAddress.IsLoopback(Host);

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
    public string Url(string webHost, string path = "") => Url(webHost, path, Scheme);

    /// <summary>
    ///     Builds a URL for one of the web hosts with a scheme of the caller's choosing. The hosts listen on
    ///     both ports whatever the configuration advertises, so this is how something that must be reachable
    ///     without TLS - the certificate download, say - names its plain http address while the advertised
    ///     URLs use the configured scheme.
    /// </summary>
    /// <param name="webHost">Config key of the host, e.g. <see cref="OperatorApiHost"/>.</param>
    /// <param name="path">Optional path to append.</param>
    /// <param name="scheme">Scheme to use, <see cref="Uri.UriSchemeHttp"/> or <see cref="Uri.UriSchemeHttps"/>.</param>
    /// <returns>The absolute URL for that host and scheme.</returns>
    public string Url(string webHost, string path, string scheme) => $"{scheme}://{Host}:{Port(webHost, scheme)}{path}";

    /// <summary>
    ///     The plain http URL of one of the web hosts, whatever the configuration advertises.
    /// </summary>
    /// <param name="webHost">Config key of the host, e.g. <see cref="OperatorApiHost"/>.</param>
    /// <param name="path">Optional path to append.</param>
    /// <returns>An absolute <c>http://</c> URL for that host.</returns>
    public string PlainUrl(string webHost, string path = "") => Url(webHost, path, Uri.UriSchemeHttp);

    /// <summary>
    ///     Resolves the port a client should use for one of the web hosts, in the advertised scheme.
    /// </summary>
    /// <param name="webHost">Config key of the host, e.g. <see cref="ClientApiHost" />.</param>
    /// <returns>
    ///     The port of the configured URL whose scheme matches <see cref="Scheme" />, or the documented
    ///     default for that host when the configuration has no such URL.
    /// </returns>
    public ushort Port(string webHost) => Port(webHost, Scheme);

    /// <summary>
    ///     Resolves the port one of the web hosts answers on in a given scheme.
    /// </summary>
    /// <param name="webHost">Config key of the host, e.g. <see cref="ClientApiHost"/>.</param>
    /// <param name="scheme">Scheme to resolve the port for, <c>http</c> or <c>https</c>.</param>
    /// <returns>
    ///     The port of the configured URL with that scheme, or the documented default for that host when the
    ///     configuration has no such URL - because the hosts listen on both, whichever the client was told to
    ///     use, and the port of the other one is still worth naming in a log line.
    /// </returns>
    public ushort Port(string webHost, string scheme)
    {
        var secure = string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
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
                    string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
                {
                    return (ushort)uri.Port;
                }
            }
        }

        var ports = DefaultPorts.TryGetValue(webHost, out var configured) ? configured : DefaultPorts[CatchAllHost];
        return (ushort)(secure ? ports.Https : ports.Http);
    }

    /// <summary>
    ///     The one thing in this configuration the client will refuse, as a log line - or <c>null</c> when
    ///     the client gets what it needs.
    /// </summary>
    /// <remarks>
    ///     Advertising plain http is a real option for everything the client fetches for itself, but not
    ///     for the oracle: the client checks the URL it is about to ask for a game server ticket with, and
    ///     an <c>http</c> one stops it with <c>Oracle URL ... not configured for HTTPS (request must be
    ///     secure)</c> - which reads like a server failure and is a client policy. The warning says so
    ///     before anyone spends an evening on the firewall. See <c>Docs/REMOTE_PLAY.md</c>.
    /// </remarks>
    /// <returns>A one-line warning for the log, or <c>null</c>.</returns>
    public string ClientWarning()
    {
        if (UseHttps)
        {
            return null;
        }

        return "Firefall:AdvertiseHttps is false, so the hosts advertise plain http - and the client will not " +
               "accept an http oracle URL: it refuses it with \"Oracle URL " + Url(ClientApiHost, "/api/v1/oracle/ticket") +
               " not configured for HTTPS (request must be secure)\" at Enter World, which is as far as a plain http " +
               "server gets a player. Set Firefall:AdvertiseHttps back to true and let PIN issue its own certificate " +
               "for " + Host + " (Docs/REMOTE_PLAY.md); the http endpoints keep answering either way, which is what curl and the " +
               "client's own asset stream use them for.";
    }
}