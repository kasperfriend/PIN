using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Controllers;
using GameServer.GRPC;
using GameServer.StaticDB;
using GameServer.Test;
using Serilog;
using Shared.Udp;
using SDB = FauFau.Formats.StaticDB;

namespace GameServer;

internal class GameServer : PacketServer
{
    private const double _gameTickRate = 1.0 / 60.0;

    private readonly ConcurrentDictionary<uint, INetworkPlayer> _clientMap;

    private readonly ulong _serverId;
    private readonly GameServerSettings  _settings;

    private IShard _shard;
    private bool _isReady;

    public GameServer(GameServerSettings serverSettings,
                      ILogger logger,
                      SDB sdb)
        : base(serverSettings.Port, logger)
    {
        _clientMap = new ConcurrentDictionary<uint, INetworkPlayer>();

        _serverId = GenerateServerId();

        _settings = serverSettings;

        Logger.Information("Serving client {Environment}/{Branch} version {ClientVersion}: GSS protocol {GssVersion}, Matrix protocol {MatrixVersion}", serverSettings.ClientEnvironment, serverSettings.ClientBranch, serverSettings.ClientVersion, serverSettings.GssProtocolVersion, serverSettings.MatrixProtocolVersion);
        Logger.Information("Firefall data paths -> StaticDBPath: {StaticDBPath} | MapsPath: {MapsPath} | AssetDBPath: {AssetDBPath} | CachePath: {CachePath}", serverSettings.StaticDBPath, serverSettings.MapsPath, serverSettings.AssetDBPath, serverSettings.CachePath);

        Logger.ForContext<SDBInterface>().Information("Reading from SDB");
        SDBInterface.Init(sdb);

        Logger.ForContext<SDBInterface>().Information("Reading custom data");
        CustomDBInterface.Init();

        Logger.ForContext(typeof(GRPCService)).Information("Initializing GRPC");
        GRPCService.Init(serverSettings.GrpcChannelAddress);
    }

    protected override void Startup(CancellationToken ct)
    {
        DataUtils.Init();
        Factory.Init();

        var shardId = _serverId | (1u << 8) | (byte)GuidService.AdditionalTypes.Instance;
        var shard = new Shard(_gameTickRate, shardId, _settings, this, Logger);

        // Attach before the shard starts serving clients: a player that migrates out must also leave the
        // socket id map. The map is the one reference to the player the shard does not control — a leaked
        // entry pins the player (character entity, inventory, channels) for the lifetime of the process,
        // and a later client handed the same socket id would be served the dead player's connection.
        shard.PlayerMigratedOut += OnPlayerMigratedOut;

        _shard = shard;

        _shard.Run(ct);

        if (_settings.GrpcChannelAddress != string.Empty)
        {
            _ = ListenGrpcAsync(ct);
        }

        _isReady = true;
        Logger.Information("Server is ready to accept connections.");
    }

    protected override void HandlePacket(Packet packet, CancellationToken ct)
    {
        if (Logger.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
        {
            Logger.Verbose("[GAME] {RemoteEndpoint} sent {PacketLength} bytes.", packet.RemoteEndpoint, packet.PacketData.Length);
            Logger.Verbose(">  {PacketData}", BitConverter.ToString(packet.PacketData.ToArray()).Replace("-", " "));
        }

        var client = RetrieveClient(packet);
        client.HandlePacket(packet.PacketData[4..], packet);
    }

    /// <summary>
    ///     Generate the Server Id
    ///     TODO: Incorporate the Sql Node Number as per https://gist.github.com/SilentCLD/881839a9f45578f1618db012fc789a71
    /// </summary>
    private static ulong GenerateServerId()
    {
        // The low two bytes are reserved for the shard flags ORed on top of the id, so only
        // bytes 2..7 are randomized. Clear the buffer first: stackalloc memory is not zeroed,
        // and reading the uninitialized bytes back would leak whatever was on the stack into
        // the server id.
        Span<byte> ranSpan = stackalloc byte[8];
        ranSpan.Clear();
        Random.Shared.NextBytes(ranSpan.Slice(2, 6));
        return BinaryPrimitives.ReadUInt64LittleEndian(ranSpan);
    }

    private void OnPlayerMigratedOut(INetworkPlayer player)
    {
        // Value-checked removal (like the shard's client map): if a new connection already reused
        // the socket id during teardown, its entry stays.
        _clientMap.TryRemove(new KeyValuePair<uint, INetworkPlayer>(player.SocketId, player));
    }

    private INetworkClient RetrieveClient(Packet packet)
    {
        var socketId = Utils.SimpleFixEndianness(packet.Read<uint>());

        if (_clientMap.TryGetValue(socketId, out var existing))
        {
            return existing;
        }

        if (!_isReady)
        {
            var rejected = new NetworkClient(packet.RemoteEndpoint, socketId, Logger);
            rejected.NetClientStatus = ClientStatus.Aborted;
            Logger.Information("Rejected connection from {Endpoint} — server not ready.", packet.RemoteEndpoint);
            return rejected;
        }

        var newClient = new NetworkPlayer(packet.RemoteEndpoint, socketId, Logger);

        // TryAdd instead of AddOrUpdate: only the thread that actually inserted the player
        // migrates it into the shard, so a racing pair of first packets can neither run
        // MigrateIn twice nor migrate the winner's client from the loser's thread. A lost
        // race simply falls through to reading the winner's entry below.
        if (_clientMap.TryAdd(socketId, newClient))
        {
            _shard.MigrateIn(newClient);
            return newClient;
        }

        // Lost the insert race: serve whoever won it. (TryGetValue rather than the indexer
        // because even the winner can already have been removed again by a disconnect.)
        return _clientMap.TryGetValue(socketId, out var winner) ? winner : newClient;
    }

    private async Task ListenGrpcAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await GRPCService.ListenAsync(_clientMap, ct);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested through the token; stop retrying.
                break;
            }
            catch (Exception ex)
            {
                Logger.ForContext(typeof(GRPCService)).Error(ex, "Failed to establish GRPC stream, retrying in 30 seconds");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }
}