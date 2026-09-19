using Aero.Protocol;
using AeroMessages.GSS.Vehicle.Command;
using AeroMessages.GSS.Vehicle.Event;
using GameServer.Entities.Vehicle;
using GameServer.Extensions;
using GameServer.Packets;
using GameServer.Systems.Aptitude;
using Serilog;

namespace GameServer.Controllers.Vehicle;

[Typecode(GssVehicleView.CombatController)]
public class CombatController : Base
{
    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        // Nothing to register: vehicle combat state lives on VehicleEntity and the
        // vehicle's ability list is populated at spawn time by EntityMan.SpawnVehicle.
    }

    [MessageID(GssVehicleCommand.ActivateAbility)]
    public void ActivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var activateAbility = packet.Unpack<ActivateAbility>();

        if (!client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out var entity) || entity is not VehicleEntity vehicle)
        {
            return;
        }

        var abilityId = vehicle.Abilities[(byte)activateAbility.AbilitySlotIndex];

        var character = player.CharacterEntity;
        var shard = character.Shard;

        if (character.IsPlayerControlled)
        {
            var message = new AbilityActivated() { AbilityId = abilityId, Time = activateAbility.Time };

            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }

        shard.Abilities.HandleActivateAbility(shard, vehicle, abilityId, activateAbility.Time, new AptitudeTargets(vehicle));
    }

    [MessageID(GssVehicleCommand.DeactivateAbility)]
    public void DeactivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // Vehicle activations are fire-and-forget: unlike the character combat
        // controller, no BeginAbilityActivation registration happens here, so there
        // is nothing to end. A future channeled vehicle ability (duration chains
        // driven by ActivationDurationCommand) would need the same
        // Begin/EndAbilityActivation mirror the character side uses. The packet is
        // consumed so it stays out of the unhandled-command log.
    }
}