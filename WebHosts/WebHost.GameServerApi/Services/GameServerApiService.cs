using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcGameServerAPIClient;
using Microsoft.Extensions.Logging;
using Shared.Common.Characters;

namespace WebHost.GameServerApi.Services;

/// <summary>
/// Server side of the GameServerAPI the GameServer talks to.
///
/// Before this existed the GameServer's call to GetCharacterAndBattleframeVisuals
/// always failed, so every login silently fell back to a hardcoded character and
/// players spawned as something other than what they picked in the selection
/// screen. This serves the real character out of the shared CharacterStore.
/// </summary>
public class GameServerApiService : GameServerAPI.GameServerAPIBase
{
    private readonly ILogger<GameServerApiService> _logger;

    // The live GameServer command streams. Events (like a New You appearance
    // update) are pushed down every stream a GameServer currently holds open,
    // so each shard re-skins the character it has zoned in. The service is
    // registered as a singleton by AddGrpc, so instance state is process-wide.
    private readonly ConcurrentDictionary<IServerStreamWriter<Event>, byte> _connectedStreams = new();

    public GameServerApiService(ILogger<GameServerApiService> logger)
    {
        _logger = logger;
        CharacterStore.Init();
        CharacterEvents.VisualsUpdated += OnCharacterVisualsUpdated;
    }

    public override Task<CharacterAndBattleframeVisuals> GetCharacterAndBattleframeVisuals(CharacterID request, ServerCallContext context)
    {
        var character = CharacterStore.Get((ulong)request.ID);

        if (character == null)
        {
            _logger.LogWarning("No character found for id {CharacterId}", request.ID);
            throw new RpcException(new Status(StatusCode.NotFound, $"No character with id {request.ID}"));
        }

        _logger.LogInformation(
            "Serving character {Name} ({CharacterGuid}) with battleframe {Battleframe}",
            character.Name,
            character.CharacterGuid,
            character.CurrentBattleframeSDBId);

        return Task.FromResult(Map(character));
    }

    public override Task<PingResp> Ping(PingReq request, ServerCallContext context)
    {
        return Task.FromResult(new PingResp
        {
            ClientSentTime = request.SentTime,
            ServerReciveTime = Timestamp.FromDateTime(DateTime.UtcNow)
        });
    }

    /// <summary>
    /// Long lived duplex stream. The GameServer pushes commands up; events flow
    /// back down. We persist the commands we understand and hold the stream open
    /// so the GameServer's listener stays connected.
    /// </summary>
    public override async Task Stream(
        IAsyncStreamReader<Command> requestStream,
        IServerStreamWriter<Event> responseStream,
        ServerCallContext context)
    {
        _connectedStreams[responseStream] = 0;
        _logger.LogInformation("GameServer connected to command stream");

        try
        {
            await foreach (var command in requestStream.ReadAllAsync(context.CancellationToken))
            {
                switch (command.SubtypeCase)
                {
                    case Command.SubtypeOneofCase.SaveGameSessionData:
                        var data = command.SaveGameSessionData;
                        CharacterStore.UpdateSessionData(
                            data.CharacterId,
                            data.ZoneId,
                            data.OutpostId,
                            data.TimePlayed);
                        _logger.LogInformation(
                            "Saved session for {CharacterId}: zone {ZoneId} outpost {OutpostId}",
                            data.CharacterId,
                            data.ZoneId,
                            data.OutpostId);
                        break;

                    case Command.SubtypeOneofCase.SaveCurrentBattleframe:
                        var frame = command.SaveCurrentBattleframe;
                        CharacterStore.UpdateCurrentBattleframe(
                            frame.CharacterId,
                            frame.ZoneId,
                            frame.BattleframeSDBId);
                        _logger.LogInformation(
                            "Saved battleframe {Battleframe} for {CharacterId}",
                            frame.BattleframeSDBId,
                            frame.CharacterId);
                        break;

                    case Command.SubtypeOneofCase.SaveLgvRaceFinish:
                        _logger.LogInformation(
                            "LGV race finish for {CharacterGuid}: {TimeMs}ms",
                            command.SaveLgvRaceFinish.CharacterGuid,
                            command.SaveLgvRaceFinish.TimeMs);
                        break;

                    default:
                        _logger.LogWarning("Unhandled command {Subtype}", command.SubtypeCase);
                        break;
                }
            }
        }
        catch (IOException)
        {
            // GameServer went away; nothing to do.
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            // Normal shutdown.
        }
        finally
        {
            // The stream is gone for good (completion, peer disconnect or
            // cancellation); stop sending events into it.
            _connectedStreams.TryRemove(responseStream, out _);
        }

        _logger.LogInformation("GameServer disconnected from command stream");
    }

    /// <summary>
    /// The New You terminal's save landed in the CharacterStore (same process,
    /// same in-memory copy): push a <c>CharacterVisualsUpdated</c> event down
    /// every connected GameServer so the character re-skins in-game instead of
    /// only wearing the new look from the next login on.
    /// </summary>
    private void OnCharacterVisualsUpdated(CharacterRecord updated)
    {
        if (updated == null)
        {
            return;
        }

        // Re-read the record: the payload must be the fully persisted state,
        // not a reference into whatever the caller is still mutating.
        var character = CharacterStore.Get(updated.CharacterGuid);
        if (character == null)
        {
            _logger.LogWarning("Visuals update for unknown character {CharacterGuid}; nothing to broadcast", updated.CharacterGuid);
            return;
        }

        var evt = new Event
                  {
                      CharacterVisualsUpdated = new CharacterVisualsUpdated
                                                {
                                                    CharacterGuid = CharacterResolver.GameServerEventGuid(character.CharacterGuid),
                                                    CharacterAndBattleframeVisuals = Map(character)
                                                }
                  };

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Write <paramref name="evt"/> to every GameServer stream that is
    /// currently connected. A dead stream is reported, never fatal: one
    /// flapping shard must not swallow the update for the others.
    /// </summary>
    private void BroadcastEvent(Event evt)
    {
        if (_connectedStreams.IsEmpty)
        {
            _logger.LogDebug("No GameServer stream connected; dropping {Subtype}", evt.SubtypeCase);
            return;
        }

        _logger.LogInformation("Broadcasting {Subtype} to {Count} GameServer stream(s)", evt.SubtypeCase, _connectedStreams.Count);

        foreach (var stream in _connectedStreams.Keys)
        {
            // Fire and forget: the event handler must not block on a slow or
            // dead stream, and write failures are logged inside.
            _ = WriteEventAsync(stream, evt);
        }
    }

    private async Task WriteEventAsync(IServerStreamWriter<Event> stream, Event evt)
    {
        try
        {
            await stream.WriteAsync(evt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write {Subtype} to a GameServer stream", evt.SubtypeCase);
        }
    }

    private static CharacterAndBattleframeVisuals Map(CharacterRecord character)
    {
        var visuals = character.Visuals;

        var result = new CharacterAndBattleframeVisuals
        {
            CharacterInfo = new BasicCharacterInfo
            {
                Name = character.Name,
                Race = character.Race,
                Gender = character.Gender,
                TitleId = character.TitleId,
                CurrentBattleframeId = 0,
                CurrentBattleframeSDBId = character.CurrentBattleframeSDBId,
                ArmyTag = character.ArmyTag ?? string.Empty,
                ArmyGuid = character.ArmyGuid,
                ArmyIsOfficer = character.ArmyIsOfficer,
                LastZoneId = character.LastZoneId,
                LastOutpostId = character.LastOutpostId,
                TimePlayed = character.TimePlayed
            },
            CharacterVisuals = new CharacterVisuals
            {
                Id = 0,
                Race = (int)character.Race,
                Gender = (int)character.Gender,
                SkinColor = Colored(visuals.SkinColorId, visuals.SkinColor),
                VoiceSet = Id(visuals.VoiceSet),
                Head = Id(visuals.Head),
                EyeColor = Colored(visuals.EyeColorId, visuals.EyeColor),
                LipColor = Colored(visuals.LipColorId, visuals.LipColor),
                HairColor = Colored(visuals.HairColorId, visuals.HairColor),
                FacialHairColor = Colored(visuals.FacialHairColorId, visuals.FacialHairColor),
                Eyes = Id(visuals.Eyes),
                Hair = new WebIdValueColorId
                {
                    Id = (int)visuals.Hair,
                    Color = new WebColorId { Id = (int)visuals.HairColorId, Value = visuals.HairColor }
                },
                FacialHair = new WebIdValueColorId
                {
                    Id = (int)visuals.FacialHair,
                    Color = new WebColorId { Id = (int)visuals.FacialHairColorId, Value = visuals.FacialHairColor }
                },
                Glider = Id(visuals.Glider),
                Vehicle = Id(visuals.Vehicle)
            },
            BattleframeVisuals = new PlayerBattleframeVisuals
            {
                WarpaintId = visuals.WarpaintId
            }
        };

        foreach (var accessory in CharacterAppearance.HeadAccessoryMeshes(visuals))
        {
            result.CharacterVisuals.HeadAccessories.Add(Colored(accessory, visuals.HeadAccessoryColor));
        }

        foreach (var ornament in visuals.Ornaments)
        {
            result.CharacterVisuals.Ornaments.Add(Id(ornament));
        }

        foreach (var value in visuals.Warpaint)
        {
            result.BattleframeVisuals.Warpaint.Add(value);
        }

        return result;
    }

    private static WebId Id(uint id) => new() { Id = (int)id };

    private static WebIdValueColor Colored(uint id, uint color) => new()
    {
        Id = (int)id,
        Value = new WebColor { Color = color }
    };
}
