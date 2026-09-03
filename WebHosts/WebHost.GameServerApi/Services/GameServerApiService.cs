using System;
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

    public GameServerApiService(ILogger<GameServerApiService> logger)
    {
        _logger = logger;
        CharacterStore.Init();
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

        _logger.LogInformation("GameServer disconnected from command stream");
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

        foreach (var accessory in visuals.HeadAccessories)
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
