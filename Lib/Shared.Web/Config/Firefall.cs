using System.Collections.Generic;

namespace Shared.Web.Config;

/// <summary>
///     The <c>Firefall</c> configuration section shared by every web host.
/// </summary>
public class Firefall
{
    /// <summary>
    ///     Bind addresses of the individual web hosts, keyed by the host namespace
    ///     (<c>WebHost.ClientApi</c>, <c>WebHost.OperatorApi</c>, ...). Each entry holds the URLs Kestrel
    ///     listens on, separated by <c>;</c>.
    /// </summary>
    public Dictionary<string, WebHost> WebHosts { get; set; } = new();

    /// <summary>
    ///     Hostname or IP address PIN advertises to clients - the address the capability response
    ///     (<c>GET /check</c>) and the oracle ticket hand out for every host, including the MatrixServer
    ///     address the client dials for the UDP handshake. Leave it at <c>localhost</c> for a server that
    ///     only ever serves the machine it runs on; set it to the address players reach you on (a LAN IP
    ///     or a RadminVPN/Hamachi address such as <c>26.1.2.3</c>) so a second player is sent to your
    ///     machine instead of to his own. See <c>Docs/REMOTE_PLAY.md</c>.
    /// </summary>
    public string PublicHost { get; set; } = "localhost";

    /// <summary>
    ///     Whether the advertised URLs use https (the default) or plain http. Set this to <c>false</c> to
    ///     serve players that do not trust PIN's self-signed development certificate: the http endpoints
    ///     are bound either way, and with this off the hosts stop redirecting plain requests to their TLS
    ///     port (a redirect a remote client cannot follow without trusting that certificate).
    /// </summary>
    public bool AdvertiseHttps { get; set; } = true;

    /// <summary>
    ///     UDP port of the MatrixServer, advertised as the <c>host:port</c> matrix address of the oracle
    ///     ticket. Must match the <c>Port</c> the MatrixServer listens on (25000 by default). The GameServer
    ///     port is not advertised here: the MatrixServer's HUGG reply carries it to the client.
    /// </summary>
    public ushort MatrixPort { get; set; } = 25000;
}