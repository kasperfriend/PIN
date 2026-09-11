using System;
using System.Collections.Generic;
using System.Threading;
using Aero.Protocol;
using MatrixServer.Packets;
using Serilog;
using Shared.Udp;

namespace MatrixServer;

internal class MatrixServer : PacketServer
{
    /// <summary>
    ///     How many of the most recently handed out socket ids stay reserved. The id space only has 256
    ///     values, so without this window two connections collide (on average once every 256 POKEs) and
    ///     the GameServer then serves both endpoints with a single NetworkPlayer.
    /// </summary>
    private const int RecentSocketIdWindow = 64;

    private readonly Queue<uint> _recentSocketIds = new();
    private readonly HashSet<uint> _recentSocketIdSet = new();

    public MatrixServer(MatrixServerSettings matrixServerSettings,
                        ILogger logger)
        : base(matrixServerSettings.Port, logger)
    {
    }

    protected override void HandlePacket(Packet packet, CancellationToken ct)
    {
        var mem = packet.PacketData;
        var socketId = Deserializer.ReadStruct<uint>(mem);
        if (socketId != 0)
        {
            return;
        }

        Logger.Verbose("[MATRIX] " + packet.RemoteEndpoint + " sent " + packet.PacketData.Length + " bytes.");

        var matrixPkt = Deserializer.ReadStruct<MatrixPacketBase>(mem);

        switch (matrixPkt.Type)
        {
            case "POKE":
                var nextSocketId = GenerateSocketId();
                Logger.Information("Assigning SocketID [{SocketID}] to [{RemoteEndpoint}]", nextSocketId, packet.RemoteEndpoint);

                var poke = Deserializer.ReadStruct<MatrixPacketPoke>(mem);
                var knownProtocol = ProtocolVersions.TryGetMatrixVersion(poke.ProtocolVersion, out var matrixVersion);
                if (!knownProtocol)
                {
                    Logger.Warning("SocketID [{SocketID}] Unknown ProtocolVersion: {ProtocolVersion}", poke.SocketID, poke.ProtocolVersion);
                }
                else
                {
                    Logger.Information("SocketID [{SocketID}] Matrix Protocol {MatrixVersion} ({ProtocolVersion})", nextSocketId, matrixVersion, poke.ProtocolVersion);
                }

                _ = SendAsync(Serializer.WriteStruct(new MatrixPacketHehe(nextSocketId)), packet.RemoteEndpoint);
                break;
            case "KISS":
                var kiss = Deserializer.ReadStruct<MatrixPacketKiss>(mem);
                var knownStreamingProtocol = ProtocolVersions.TryGetGssVersion(kiss.StreamingProtocolVersion, out var gssVersion);
                if (!knownStreamingProtocol)
                {
                    Logger.Warning("SocketID [{SocketID}] Unknown StreamingProtocolVersion {StreamingProtocolVersion}", kiss.ReceivedSocketID, kiss.StreamingProtocolVersion);
                }
                else
                {
                    Logger.Information("SocketID [{SocketID}] GSS Protocol {GssVersion} ({StreamingProtocolVersion})", kiss.ReceivedSocketID, gssVersion, kiss.StreamingProtocolVersion);
                }

                _ = SendAsync(Serializer.WriteStruct(new MatrixPacketHugg(1, 25001)), packet.RemoteEndpoint);
                break;
            case "ABRT":
                var abrt = Deserializer.ReadStruct<MatrixPacketAbrt>(mem);
                Logger.Information("Received abort with reason: {AbortCode}", abrt.Code);
                break;
            default:
                Logger.Error("Unknown Matrix Packet Type: " + matrixPkt.Type);
                return;
        }
    }

    private uint GenerateSocketId()
    {
        // Keep the 0xff00ff00..0xff00ffff format the original service handed out (the client only echoes
        // the value back), but skip the ids reserved by the recent window so concurrent connections never
        // share one. Random.Shared instead of a fresh Random per POKE, which paid the seed entropy on
        // every call.
        uint socketId;
        do
        {
            socketId = unchecked((uint)((0xff00ff << 8) | Random.Shared.Next(0, 256)));
        }
        while (_recentSocketIdSet.Contains(socketId) && _recentSocketIdSet.Count < 256);

        if (_recentSocketIds.Count >= RecentSocketIdWindow)
        {
            _recentSocketIdSet.Remove(_recentSocketIds.Dequeue());
        }

        _recentSocketIds.Enqueue(socketId);
        _recentSocketIdSet.Add(socketId);

        return socketId;
    }
}