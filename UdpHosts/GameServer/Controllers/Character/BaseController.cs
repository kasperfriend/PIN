using System;
using System.Numerics;
using Aero.Protocol;
using AeroMessages.GSS;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Command;
using AeroMessages.GSS.Character.Event;
using GameServer.Data;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Turret;
using GameServer.Entities.Vehicle;
using GameServer.Extensions;
using GameServer.GRPC;
using GameServer.Packets;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Dialog;
using GameServer.Systems.Encounters;
using Serilog;
using static AeroMessages.GSS.Character.Command.NonDevDebugCommand;
using LoadoutVisualType = AeroMessages.GSS.Character.LoadoutConfig_Visual.LoadoutVisualType;

namespace GameServer.Controllers.Character;

[Typecode(GssCharacterView.BaseController)]
public class BaseController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger.ForContext<CharacterEntity>();
    }

    [MessageID(GssCharacterCommand.FetchQueueInfo)]
    public void FetchQueueInfo(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var fetchQueueInfoResponse = new FetchQueueInfoResponse
        {
            Succes = 0,
            Queues =
            [
                new FetchQueueData
                {
                    QueueId = 2121,
                    Qualifies = 1,
                    ChallengeEnabled = 0,
                    Gametype = "campaign",
                    DisplayKeyName = "MATCH_MAP_PROVING_GROUND",
                    DisplayKeyDesc = "MATCH_MAP_PROVING_GROUND_DESC",
                    ZoneId = 1155,
                    MissionId = 497,
                    Certs =
                    [
                        new QueueCertsData
                        {
                            CertId = 3589,
                            Passed = 1
                        }
                    ],
                    Difficulties =
                    [
                        new QueueDifficultiesData
                        {
                            DifficultyId = 6922,
                            UiString = "INSTANCE_DIFFICULTY_NORMAL",
                            MinLevel = 20,
                            DisplayLevel = 20,
                            MaxSuggestedLevel = 40,
                            DifficultyKey = "NORMAL_MODE",
                            PlayerCount1 = 5,
                            PlayerCount2 = 5,
                            PlayerCount3 = 5,
                            MinPlayers = 1,
                            MaxPlayers = 5
                        },
                        new QueueDifficultiesData
                        {
                            DifficultyId = 7022,
                            UiString = "INSTANCE_DIFFICULTY_CHALLENGE",
                            MinLevel = 20,
                            DisplayLevel = 20,
                            MaxSuggestedLevel = 40,
                            DifficultyKey = "CHALLENGE_MODE",
                            PlayerCount1 = 5,
                            PlayerCount2 = 5,
                            PlayerCount3 = 5,
                            MinPlayers = 1,
                            MaxPlayers = 5
                        },
                        new QueueDifficultiesData
                        {
                            DifficultyId = 9122,
                            UiString = "INSTANCE_DIFFICULTY_HARD",
                            MinLevel = 45,
                            DisplayLevel = 45,
                            MaxSuggestedLevel = 45,
                            DifficultyKey = "HARD_MODE",
                            PlayerCount1 = 5,
                            PlayerCount2 = 5,
                            PlayerCount3 = 5,
                            MinPlayers = 1,
                            MaxPlayers = 5
                        }
                    ],
                    RewardsWinnerItems = [],
                    RewardsWinnerLoots =
                    [
                        new QueueRewardsLootData { LootTableId = 10133, DifficultyKey = "CHALLENGE_MODE" },
                        new QueueRewardsLootData { LootTableId = 10134, DifficultyKey = "HARD_MODE" },
                        new QueueRewardsLootData { LootTableId = 10132, DifficultyKey = "NORMAL_MODE" }
                    ],
                    RewardsLooserItems = [],
                    RewardsLooserLoots = []
                }
            ]
        };
    }

    [MessageID(GssCharacterCommand.PlayerReady)]
    public void PlayerReady(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        player.Ready();
    }

    [MessageID(GssCharacterCommand.MovementInput)]
    public void MovementInput(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // ToDo: This currently only handles PosRotState inputs, logic needs to be added for the other MovementDataTypes
        if (packet.BytesRemaining < 64)
        {
            return;
        }

        var movementInput = packet.Unpack<MovementInput>();

        if (!player.CharacterEntity.Alive)
        {
            return; // can't move if you're dead (or at least shouldn't o.o")
        }

        client.AssignedShard.Movement.CharacterMovementInput(client, player.CharacterEntity, movementInput);
    }

    [MessageID(GssCharacterCommand.SetMovementSimulation)]
    public void SetMovementSimulation(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // Dear client, thanks for informing us about how often you will update us.
        // We will continue to rely on your support, whilst doing nothing ourselves.
        // Best regards, TMW
        // var setMovementSimulation = packet.Unpack<SetMovementSimulation>();
        // LogMissingImplementation<BaseController>(nameof(SetMovementSimulation), entityId, packet, _logger);
    }

    [MessageID(GssCharacterCommand.BagInventorySettings)]
    public void BagInventorySettings(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // The client asks for its bag model and then enforces it locally - a full model refuses
        // purchases and drops incoming items. Answer with the real inventory (the captured
        // nine-bag layout, the actually carried slots), or the model drifts into the phantom
        // "inventory full" state a frozen developer snapshot used to impose.
        var bagInventoryUpdate = new BagInventoryUpdate
        {
            Data = BagInventoryLayout.BuildUpdateJson(player.Inventory?.GetBagSlots() ?? []),
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(bagInventoryUpdate, player.CharacterEntity.EntityId);
    }

    [MessageID(GssCharacterCommand.SetSteamUserId)]
    public void SetSteamUserId(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var setSteamIdPacket = packet.Unpack<SetSteamUserId>();
        player.SteamUserId = setSteamIdPacket.SteamUserId;
        _logger.Debug("Entity {EntityId:x8} Steam user id (Aero): {SteamUserId}", entityId, player.SteamUserId);

        // var conventional = packet.Read<SetSteamIdRequest>();
        // _logger.Verbose("Packet Data: {0}", BitConverter.ToString(packet.PacketData.ToArray()).Replace("-", " "));
        // _logger.Verbose("Entity {0:x8} Steam user id (conventional): {1}", entityId, conventional.SteamId);
    }

    [MessageID(GssCharacterCommand.VehicleCalldownRequest)]
    public void VehicleCalldownRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var vehicleCalldownRequest = packet.Unpack<VehicleCalldownRequest>();
        if (vehicleCalldownRequest == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        var abilities = client.AssignedShard.Abilities;
        abilities.HandleVehicleCalldownRequest(character.EntityId, vehicleCalldownRequest);
    }

    [MessageID(GssCharacterCommand.DeployableCalldownRequest)]
    public void DeployableCalldownRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var deployableCalldownRequest = packet.Unpack<DeployableCalldownRequest>();
        if (deployableCalldownRequest == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        var abilities = client.AssignedShard.Abilities;
        abilities.HandleDeployableCalldownRequest(character.EntityId, deployableCalldownRequest);
    }

    [MessageID(GssCharacterCommand.ResourceNodeBeaconCalldownRequest)]
    public void ResourceNodeBeaconCalldownRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var thumperCalldownRequest = packet.Unpack<ResourceNodeBeaconCalldownRequest>();
        if (thumperCalldownRequest == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        var abilities = client.AssignedShard.Abilities;
        abilities.HandleResourceNodeBeaconCalldownRequest(character.EntityId, thumperCalldownRequest);
    }

    [MessageID(GssCharacterCommand.SetEffectsFlag)]
    public void SetEffectsFlag(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SetEffectsFlag>();
        player.CharacterEntity.SetEffectsFlags(query.Flashlight);
    }

    [MessageID(GssCharacterCommand.PerformEmote)]
    public void PerformEmote(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<PerformEmote>();

        // The emote id has to be one the client's own emote table can resolve (dbcharacter::EmoteRecord):
        // an id outside it would leave every client in range with an animation it cannot look up.
        if (!player.CharacterEntity.PerformEmote(query.EmoteId, query.Time))
        {
            _logger?.Debug("Ignoring emote {EmoteId}: not in dbcharacter::EmoteRecord", query.EmoteId);
        }
    }

    [MessageID(GssCharacterCommand.NotifyDialogScriptComplete)]
    public void NotifyDialogScriptComplete(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<NotifyDialogScriptComplete>();
        uint completedId = query.Unk1 != 0 ? query.Unk1 : query.Unk2;
        DialogService.Production.OnScriptComplete(player.CharacterEntity, completedId, player.CharacterEntity.Shard.CurrentTime);
    }

    [MessageID(GssCharacterCommand.ClientQueryInteractionStatus)]
    public void ClientQueryInteractionStatus(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var inquiringEntity = player.CharacterEntity;
        var query = packet.Unpack<ClientQueryInteractionStatus>();
        ulong requestedEntityId = query.Entity.Id << 8;
        var found = client.AssignedShard.Entities.TryGetValue(requestedEntityId, out var entity);
        if (found)
        {
            // Console.WriteLine($"ClientQueryInteractionStatus IsInteractable: {entity.IsInteractable()} CanBeInteractedBy {entity.CanBeInteractedBy(inquiringEntity)}");
            if (entity.IsInteractable() && entity.CanBeInteractedBy(inquiringEntity))
            {
                var response = new AddOrUpdateInteractives()
                {
                    Entities = [query.Entity.Backing],
                    InteractionTypes = [entity.GetInteractionType()],
                    InteractionDurationsMs = [entity.GetInteractionDuration()],
                };
                client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
            }
            else
            {
                var response = new RemoveInteractives
                {
                    Entities = [query.Entity]
                };
                client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
            }
        }
        else
        {
            _logger.Debug("ClientQueryInteractionStatus entity {EntityId:x8} not found!", requestedEntityId);
        }
    }

    [MessageID(GssCharacterCommand.SalvageRequest)]
    public void SalvageRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<SalvageRequest>();
        if (request == null)
        {
            _logger.Warning("SalvageRequest from {Player}: could not unpack the request", player.CharacterEntity);
            return;
        }

        var response = Systems.Salvage.SalvageService.Run(player, request.SalvageRequests);

        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
    }

    [MessageID(GssCharacterCommand.VendorPurchaseRequest)]
    public void VendorPurchaseRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<VendorPurchaseRequest>();
        if (request == null)
        {
            _logger.Warning("VendorPurchaseRequest from {Player}: could not unpack the request", player.CharacterEntity);
            return;
        }

        var packetVendorId = Systems.Vendor.NpcVendorService.ReadStoreId(request, packet.Peek(packet.BytesRemaining).Span);
        var vendorId = Systems.Vendor.NpcVendorService.ResolvePurchaseVendorId(player, packetVendorId);

        if (packetVendorId != 0 && packetVendorId != vendorId)
        {
            // Not fatal - the guids still have to decode against the authorized terminal's vendor -
            // but a client naming a different store than the one that is open is worth knowing about.
            _logger.Warning(
                "VendorPurchaseRequest from {Player} names store {PacketVendorId} while terminal vendor {VendorId} is open",
                player.CharacterEntity,
                packetVendorId,
                vendorId);
        }

        _logger.Information(
            "VendorPurchaseRequest from {Player}: store {PacketVendorId} resolved to vendor {VendorId}, product {ProductId} price {PriceId}",
            player.CharacterEntity,
            packetVendorId,
            vendorId,
            request.ProductID,
            request.PriceID);

        var response = Systems.Vendor.NpcVendorService.TryPurchase(player, vendorId, request.ProductID, request.PriceID);

        // Root-namespace (Generic) answers travel against the shard entity on live; the purchase
        // response is one of them.
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, client.AssignedShard.InstanceId);
    }

    [MessageID(GssCharacterCommand.VendorTokenMachineRequest)]
    public void VendorTokenMachineRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<VendorTokenMachineRequest>();
        if (request == null)
        {
            return;
        }

        // The token machine window is a web store on live; answer with an empty, zeroed response
        // so the request does not go unanswered.
        var response = new VendorTokenMachineResponse
        {
            Unk1 = request.Unk1,
            Unk2 = request.Unk2,
            Unk3 = request.Unk3,
            Unk4 = request.Unk4,
            Unk5 = 0,
            Unk6 = [],
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
    }

    [MessageID(GssCharacterCommand.ResourceLocationInfosRequest)]
    public void ResourceLocationInfosRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var resourceLocationInfosResponse = new ResourceLocationInfosResponse
        {
            Data = [],
            Unk = 0x01
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(resourceLocationInfosResponse, player.CharacterEntity.EntityId);
    }

    [MessageID(GssCharacterCommand.FriendsListRequest)]
    public void FriendsListRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var friendsListResponse = new FriendsListResponse
        {
            Unk1 =
            [
                new FriendsListData
                {
                    Unk1 = 9162788533740412926,
                    Unk2 = "TestUser1",
                    Unk3 = string.Empty,
                    Unk4 = 1,
                    Unk5 = 1427570048,
                    Unk6 = 1
                },
                new FriendsListData
                {
                    Unk1 = 9153042507174448638,
                    Unk2 = "TestUser2",
                    Unk3 = string.Empty,
                    Unk4 = 1,
                    Unk5 = 1471686583,
                    Unk6 = 1
                }
            ],
            Unk2 = 0
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(friendsListResponse, player.CharacterEntity.EntityId);
    }

    [MessageID(GssCharacterCommand.MapOpened)]
    public void MapOpened(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var mapOpened = new GeographicalReportResponse
        {
            ScanId = 0,
            Position = new Vector3 { X = 0, Y = 0, Z = 0 },
            Valid = 0x00,
            Composition = []
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(mapOpened, player.CharacterEntity.EntityId);
    }

    [MessageID(GssCharacterCommand.RequestTeleport)]
    public void RequestTeleport(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var character = player.CharacterEntity;
        var query = packet.Unpack<RequestTeleport>();

        // TODO: Raycast for z
        var spawnPoint = new SpawnPoint { Position = new Vector3(query.PosX, query.PosY, 500) };

        // Workaround by looking for Z coordinate in Outposts since that's the primary way players send this message
        var zoneId = player.CurrentZone.ID;
        foreach (var outpost in CustomDBInterface.GetZoneOutposts(zoneId).Values)
        {
            if (Math.Round(outpost.Position.X) == Math.Round(query.PosX) && Math.Round(outpost.Position.Y) == Math.Round(query.PosY))
            {
                spawnPoint = client.AssignedShard.Outposts[zoneId][outpost.Id].RandomSpawnPoint;
                break;
            }
        }

        // Instantly transport character to target location
        character.PositionAtSpawnPoint(spawnPoint);
        client.AssignedShard.FallDamage?.ResetFor(character); // The drop onto the destination must not count as a fall
        var forcedMove = new ForcedMovement
        {
            Data = new ForcedMovementData
            {
                Type = 1,
                Unk1 = 0,
                HaveUnk2 = 0,
                Params1 = new ForcedMovementType1Params { Position = spawnPoint.Position, Direction = character.AimDirection, Velocity = Vector3.Zero, Time = character.Shard.CurrentTime + 1 }
            },
            ShortTime = character.Shard.CurrentShortTime
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(forcedMove, character.EntityId);
    }

    [MessageID(GssCharacterCommand.ExitAttachmentRequest)]
    public void ExitAttachmentRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        if (player.CharacterEntity.AttachedToEntity == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        var entity = character.AttachedToEntity;

        if (entity is VehicleEntity vehicle)
        {
            vehicle.RemoveOccupant(character);
        }
        else if (entity is TurretEntity turret)
        {
            if (turret.Parent is VehicleEntity parentVehicle)
            {
                parentVehicle.RemoveOccupant(character);
            }
            else
            {
                turret.SetControllingPlayer(null);
            }
        }

        character.ClearAttachedTo();

        var response = new ExitingAttachment() { Direction = new Vector3(-0.5f, -0.5f, -0.47f) };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, character.EntityId);

        if (entity is BaseEntity { Encounter.Instance: IExitAttachmentHandler handler } baseEntity
            && baseEntity.Encounter.Handles(EncounterComponent.Event.ExitAttachment))
        {
            handler.OnExitAttachment(baseEntity, (INetworkPlayer)player);
        }
    }

    [MessageID(GssCharacterCommand.SeatChangeRequest)]
    public void SeatChangeRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        if (player.CharacterEntity.AttachedToEntity == null)
        {
            return;
        }

        var query = packet.Unpack<SeatChangeRequest>();

        var character = player.CharacterEntity;

        if (character.AttachedToEntity is VehicleEntity vehicle)
        {
            vehicle.ChangeOccupantSeat(character, query.RequestedSeatIndex);
        }
        else if (character.AttachedToEntity is TurretEntity { Parent: VehicleEntity parentVehicle })
        {
            parentVehicle.ChangeOccupantSeat(character, query.RequestedSeatIndex);
        }
    }

    [MessageID(GssCharacterCommand.SelectLoadout)]
    public void SelectLoadout(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectLoadout>();
        var loadoutRefData = player.Inventory.GetLoadoutReferenceData(query.LoadoutId);
        if (loadoutRefData != null)
        {
            var loadout = new CharacterLoadout(loadoutRefData);
            player.CharacterEntity.ApplyLoadout(loadout);

            // Remember the frame so the character selection screen and the next
            // login show what the player is actually wearing.
            _ = GRPCService.SaveCurrentBattleframeAsync(
                    player.CharacterId + 0xFE,
                    player.CurrentZone?.ID ?? 0,
                    loadoutRefData.ChassisId);

            // Several UI components (like PaperdollSlotting) only refresh when ON_LEVEL_CHANGED fires.
            // Since we dont yet implement progression we just force an update here.
            if (player.CharacterEntity.Character_BaseController != null)
            {
                player.CharacterEntity.Character_BaseController.LevelProp = player.CharacterEntity.FrameProgressionLevel;
                player.CharacterEntity.Character_BaseController.EffectiveLevelProp = player.CharacterEntity.FrameProgressionLevel;
            }
        }
    }

    [MessageID(GssCharacterCommand.PerformTextChat)]
    public void PerformTextChat(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<PerformTextChat>();
        var character = player.CharacterEntity;
        var shard = player.CharacterEntity.Shard;
        shard.Chat.CharacterPerformTextChat(client, character, query);
    }

    [MessageID(GssCharacterCommand.SlotGearRequest)]
    public void SlotGearRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<SlotGearRequest>();

        player.CharacterEntity.EquipItemByGUID(request.LoadoutId, (LoadoutSlotType)request.SlotIdx, request.ItemGUID);

        var response = new SlotGearResponse()
                       {
                           ItemGUID = request.ItemGUID,
                           SlotIdx = request.SlotIdx,
                           LoadoutId = request.LoadoutId,
                           Unk1 = request.Unk,
                           Result = 1,
                       };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, entityId);
    }

    [MessageID(GssCharacterCommand.SlotVisualRequest)]
    public void SlotVisualRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<SlotVisualRequest>();

        player.CharacterEntity.EquipVisualBySdbId(request.LoadoutId, (LoadoutVisualType)request.SlotIdx1, (LoadoutSlotType)request.SlotIdx2, request.ItemSdbId);

        var response = new SlotVisualResponse()
                       {
                           ConfigId = 1,
                           SlotIdx = request.SlotIdx2,
                           LoadoutId = request.LoadoutId,
                           Result = 1,
                       };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, entityId);
    }

    [MessageID(GssCharacterCommand.NonDevDebugCommand)]
    public void NonDevDebugCommand(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<NonDevDebugCommand>();
        switch (request.Type)
        {
            case NonDevDebugCommandType.DEBUGWEAPON:
                player.Preferences.DebugWeapon = request.Value;
                break;
            case NonDevDebugCommandType.DEBUGEVENT:
                player.Preferences.DebugEvent = request.Value;
                break;
            case NonDevDebugCommandType.DEBUGLAG:
                player.Preferences.DebugLag = request.Value;
                break;
            default:
                break;
        }
    }

    [MessageID(GssCharacterCommand.UiQueryResponse)]
    public void UiQueryResponse(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var response = packet.Unpack<UiQueryResponse>();

        if (response.SelectedOptionId == 0)
        {
            return;
        }

        client.AssignedShard.EncounterMan.HandleUiQueryResponse(response, (INetworkPlayer)player);
    }
}