# Pirate Intelligence Network - Documention

PIN is split into two areas:
- [UdpHosts](#udphosts)
- [WebHosts](#webhosts)
- [Accounts & Login](ACCOUNTS.md) — `accounts.json`, the admin account, Red5 signature login, account and character creation, multiplayer notes
- [Characters & Battleframes](CHARACTERS_AND_BATTLEFRAMES.md) — `characters.json` configuration
- [Spawning & Combat](SPAWNING_AND_COMBAT.md) — NPC spawning and combat flow
- [Static Database](STATIC_DATABASE.md) — the `clientdb.sd2` format, what PIN loads from it, and the in-game `spawn` / `sdb` / `sdbinfo` commands
- [Spawnable Reference](SpawnReference/README.md) — **every** spawnable row in `clientdb.sd2` (mobs, deployables, vehicles, carryables, turrets) as a spreadsheet, each with the exact command that spawns it, plus CSV exports
- [Mobs & NPCs Catalog](MOBS_AND_NPCS.md) — every mob/NPC in `clientdb.sd2` (decoded with `Tools/SdbDump`)
- [Health System](HEALTH_SYSTEM.md) — health, damage, death, respawn and fall damage
- [NPC AI](NPC_AI.md) — how spawned mobs target, chase, attack and leash, and how to tune it
- [NPC routines & movement census](NPC_ROUTINES.md) — bounded ambient roaming, real placed work/rest activities, every database movement invocation, and the missing original-route boundary
- [World Population](WORLD_POPULATION.md) — how a zone gets filled with every mob/NPC the database puts there: where the positions come from, what the two collision checks are, what bounds the cost, and the `\population` command
- [Single Zone](SINGLE_ZONE.md) — one shard simulates one zone: what `ZoneId` controls, which zones have authored data, and what players in other zones experience
- [Web Assets](ASSETS.md) — what the client streams from the server: the asset stream, the high-resolution texture chunks, where they live, and why an empty `Assets` folder means blurry terrain
- [Remote Play & Networking](REMOTE_PLAY.md) — what the servers bind to, what they advertise to clients, and how to let a second player in over LAN / RadminVPN (config, firewall, TLS, ports, troubleshooting)

## UdpHosts

As the name suggests these are the server hosts that talk via UDP to the client.
They are further more split into two servers:
- [MatrixServer](#matrixserver)
- [GameServer](#gameserver)

### MatrixServer

The MatrixServer handles the whole initial connection of a new client and tells it where to find the GameServer to connect to.
The handshake protocol is quite simple and as such the server setup is also rather basic.

![](MatrixServer.png)

### GameServer

The GameServer is really the heart of the whole operation, which handles player connections, active shaders and receives and sends packets in the Game Server Socket (GSS) protocol.

The GameServer runs three different threads:
- ListenThread
- ServerRunThread
- SendThread

Packets are fetch from the network socket in the `ListenThread` and pushed to a `BufferBlock` of incoming packets.
The `ServerRunThread` is responsible to picking the latest packet on the `BufferBlock` and start handling it.
The `SendThread` reads from a separate `BufferBlock` of outgoing packets and pushes them onto the network socket.

A GameServer hosts exactly one `Shard`. The `Shard` has its own `RunThread` that triggers a timed network tick, which then causes the queued packets to be sent and pending packets to be proccessed. Each connecting client is migrated into the `Shard`.

The GSS protocol has four different channels:
- Control - Connection and time sync handling
- Matrix - Shard / zone related events and commands
- Reliable GSS - Message delivery is ensured with resend functionality
- Unreliable GSS - Message delivery is not guaranteed

The `NetworkClient` (`NetworkPlayer` as concrete implementation) subscribes via delegate onto the different channels and during the `Shard` network tick, the channel processing triggers these delegates.

![](GameServer.png)

### References

- [Game Server Protocol Overview](https://github.com/themeldingwars/Documentation/wiki/Game-Server-Protocol-Overview)
- [AeroMessages](https://github.com/themeldingwars/AeroMessages)

## WebHosts

Firefall uses a selection of different HTTP-based web hosts for various functionalities.
PIN implements some of those end points and has split them into the following projects:
- WebHost.CatchAll - Fallback for any unimplemented endpoint
- WebHost.Chat - API for handling the various chat channels
- WebHost.ClientApi - Primary API for characters, armies, social features, etc.
- WebHost.InGameApi - Secondary API for more bulkier data and game client information
- WebHost.Market - API for handling marketplace information
- WebHost.OperatorApi - Basic operation info, such as current API versions
- WebHost.Replay - Handling of replay actions
- WebHost.Store - RedBean store information
- WebHost.WebAsset - Assets of all sorts, from icons, to JavaScript, to streamed audio or
  textures; it is the host the client's `AssetStreamPath`/`VTRemotePath` point at, and the
  high-resolution texture chunks it serves are what decides whether the world is sharp
  ([ASSETS.md](ASSETS.md))

### Binding and advertised hosts

Every host listens on the addresses its `Firefall:WebHosts:<host>:urls` entry in
`WebHostManager/config/appsettings.json` names — `*` by default, i.e. every
interface, IPv4 and IPv6 — which `Lib/Shared.Web/BaseWebServer.cs` applies
through `UseUrls`. Which address the *client* is told to use is decided
separately, by `Firefall:PublicHost`: the capability response (`/check`) and the
oracle ticket build their URLs through `Lib/Shared.Web/Config/PublicUrls.cs`, so
the servers can listen everywhere while still advertising `localhost`, or
advertise a LAN/VPN address instead — and the advertised address decides the TLS
certificate the https endpoints are served with, since a client dialling
`26.1.2.3` needs one that names `26.1.2.3`. PIN issues and keeps that one
(`Lib/Shared.Common/Certificates/TlsCertificateStore.cs`, `certs/` next to the
binary) and serves its public half at `GET /certificate.cer`. See
[Remote Play & Networking](REMOTE_PLAY.md).

### Asset roots

`WebHost.WebAsset` answers everything the client streams, and it answers it out of
the folders on disk — `<content root>\Assets` plus the folders named by
`Firefall:Assets:Paths` — through `Shared.Web.Assets.WebAssetFileProvider`. The
repository ships that folder empty (`.gitignore`), and `Assets/README.md` next to
the binary says what goes into it; `AssetRootProbe` is the read-only scan behind
the startup report, and `VirtualTextureChunks` knows the chunk names the client
probes for. See [ASSETS.md](ASSETS.md).

### References

- [OpenAPI Specification](https://github.com/themeldingwars/Documentation/tree/master/Networking)
- [Game Hosts - FireFall Wiki](https://firefall-archive.fandom.com/wiki/Game_Hosts)