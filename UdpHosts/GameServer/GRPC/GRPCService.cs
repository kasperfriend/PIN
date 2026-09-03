using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using GameServer.GRPC.EventHandlers;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcGameServerAPIClient;
using Serilog;

namespace GameServer.GRPC;

public static class GRPCService
{
    private static readonly ILogger _logger = Log.ForContext(typeof(GRPCService));

    private static GrpcChannel _channel;
    private static GameServerAPI.GameServerAPIClient _client;
    private static AsyncDuplexStreamingCall<Command, Event> _stream;

    public static void Init(string address)
    {
        _channel = GrpcChannel.ForAddress(address);
        _client = new GameServerAPI.GameServerAPIClient(_channel);
    }

    public static bool IsAvailable()
    {
        return _channel.State == ConnectivityState.Ready;
    }

    public static async Task<CharacterAndBattleframeVisuals> GetCharacterAndBattleframeVisualsAsync(long characterId)
    {
        return await _client.GetCharacterAndBattleframeVisualsAsync(new CharacterID { ID = characterId });
    }

    public static async Task SaveCharacterSessionDataAsync(ulong characterId, uint zoneId, uint outpostId, uint timePlayed)
    {
        var data = new SaveGameSessionData()
           {
               CharacterId = characterId, ZoneId = zoneId, OutpostId = outpostId, TimePlayed = timePlayed
           };

        await SendCommandAsync(new Command() { SaveGameSessionData = data });
    }

    /// <summary>
    /// Persist the battleframe the player just switched to, so the selection
    /// screen and the next login both reflect it.
    /// </summary>
    public static async Task SaveCurrentBattleframeAsync(ulong characterId, uint zoneId, uint battleframeSdbId)
    {
        var data = new SaveCurrentBattleframe()
           {
               CharacterId = characterId, ZoneId = zoneId, BattleframeSDBId = battleframeSdbId
           };

        await SendCommandAsync(new Command() { SaveCurrentBattleframe = data });
    }

    public static async Task SendCommandAsync(Command command)
    {
        // The stream is only established once ListenAsync connects. Dropping the
        // command is far better than taking the shard down on a background task.
        if (_stream == null)
        {
            _logger.Warning("Dropping GRPC command {Subtype}, stream is not connected", command.SubtypeCase);
            return;
        }

        try
        {
            await _stream.RequestStream.WriteAsync(command);
        }
        catch (RpcException ex)
        {
            _logger.Warning(ex, "Failed to send GRPC command {Subtype}", command.SubtypeCase);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warning(ex, "Failed to send GRPC command {Subtype}", command.SubtypeCase);
        }
    }

    public static async Task ListenAsync(ConcurrentDictionary<uint, INetworkPlayer> clientMap, CancellationToken ct)
    {
        _stream = _client.Stream(cancellationToken: ct);

        await foreach (var evt in _stream.ResponseStream.ReadAllAsync(ct))
        {
            _logger.Information("{Event}", evt);
            switch (evt.SubtypeCase)
            {
                case Event.SubtypeOneofCase.ArmyApplicationApproved:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationApproved, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyApplicationReceived:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationReceived, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyApplicationRejected:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationRejected, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyApplicationsUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationsUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyIdChanged:
                    ArmyEventHandler.HandleEvent(evt.ArmyIdChanged, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInfoUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyInfoUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInviteApproved:
                    ArmyEventHandler.HandleEvent(evt.ArmyInviteApproved, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInviteReceived:
                    ArmyEventHandler.HandleEvent(evt.ArmyInviteReceived, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInviteRejected:
                    ArmyEventHandler.HandleEvent(evt.ArmyInviteRejected, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyMembersUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyMembersUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyRanksUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyRanksUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyTagUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyTagUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.CharacterVisualsUpdated:
                    CharacterEventHandler.HandleEvent(evt.CharacterVisualsUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.None:
                default:
                    break;
            }
        }
    }
}