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
    ///     Whether the advertised URLs use https (the default) or plain http.
    /// </summary>
    /// <remarks>
    ///     Keep this <c>true</c>: the client itself refuses a plain http oracle URL - it answers the click
    ///     on <c>Enter World</c> with <c>Oracle URL http://host:4402 not configured for HTTPS (request must
    ///     be secure)</c>, so an http-only server gets a remote player to the character selection screen
    ///     and no further (see <c>PublicUrls.ClientWarning</c>, which says so in the log). With this on,
    ///     PIN issues itself a certificate for <see cref="PublicHost"/> when you have not configured one
    ///     (see <see cref="Certificate"/>) so the address the client dials is on the certificate it is
    ///     validated against.
    /// </remarks>
    public bool AdvertiseHttps { get; set; } = true;

    /// <summary>
    ///     Whether a plain http request is answered with a redirect to the host's TLS port. Off by
    ///     default: both ports are fully functional, and a 307 the client does not follow - or cannot,
    ///     because it has no reason to trust the certificate on the other side of it - is a server that
    ///     silently does not exist for that client. Turn it on to push everything that arrives in the clear
    ///     onto https; the certificate download routes stay plain either way, since a player who has to
    ///     trust the certificate first cannot follow a redirect that already requires it.
    /// </summary>
    public bool RedirectHttpToHttps { get; set; }

    /// <summary>
    ///     The TLS certificate the https endpoints are served with: your own <c>.pfx</c>, or the one PIN
    ///     issues for <see cref="PublicHost"/> and keeps next to the binary.
    /// </summary>
    public FirefallCertificate Certificate { get; set; } = new();

    /// <summary>
    ///     UDP port of the MatrixServer, advertised as the <c>host:port</c> matrix address of the oracle
    ///     ticket. Must match the <c>Port</c> the MatrixServer listens on (25000 by default). The GameServer
    ///     port is not advertised here: the MatrixServer's HUGG reply carries it to the client.
    /// </summary>
    public ushort MatrixPort { get; set; } = 25000;
}