using System.Numerics;
using AeroMessages.GSS;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Movement;

/// <summary>
///     <c>aptfs::BullrushCommandDef</c> (31 rows): a timed charge that carries the character forward -
///     the Rhino/Dreadnaught <c>Charge</c> family. The def maps 1:1 onto the protocol's own bullrush
///     block, <c>ForcedMovementType.Bullrush</c> (6): <c>Speed</c> and <c>Duration</c> become the
///     block's <c>Speed</c> and its <c>StartTime</c>/<c>EndTime</c> window, and <c>Velocity</c> is the
///     character's facing scaled by the speed. Both columns take a regop, resolved the way every other
///     movement command resolves one (<c>AbilitySystem.RegistryOp</c>).
///     <para>
///         The client drives the motion from the packet ("uses bullrush cvars"), so unlike a
///         <c>ForcePush</c> impulse the server does not integrate the path itself; it stamps the
///         destination-side fall-damage reset the same way <c>TeleportCommand</c> does, so landing the
///         charge cannot bill the pre-charge fall speed.
///     </para>
///     <para>
///         <c>ImpactEffect</c> is not applied: it is nonzero in exactly 1 of the 31 rows, and honouring
///         it means a swept collision test along the charge path, which the server has no geometry for.
///         The chain that carries it still applies its own effects through its
///         <c>ImpactApplyEffect</c> nodes.
///     </para>
/// </summary>
public class BullrushCommand : Command, ICommand
{
    private BullrushCommandDef Params;

    public BullrushCommand(BullrushCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float speed = AbilitySystem.RegistryOp(context.Register, Params.Speed, (Operand)Params.SpeedRegop);
        float duration = AbilitySystem.RegistryOp(context.Register, Params.Duration, (Operand)Params.DurationRegop);

        if (speed <= 0f || duration <= 0f)
        {
            Logger.Debug(
                "{Command} {CommandId} is a no-op: speed {Speed} duration {Duration}",
                nameof(BullrushCommand), Params.Id, speed, duration);
            return true;
        }

        if (Params.ImpactEffect != 0)
        {
            Logger.Debug(
                "{Command} {CommandId} carries ImpactEffect {ImpactEffect}, which needs a swept collision test the server has no geometry for",
                nameof(BullrushCommand), Params.Id, Params.ImpactEffect);
        }

        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character == null)
        {
            Logger.Debug(
                "{Command} {CommandId} skips {Self}: only a character can be charged",
                nameof(BullrushCommand), Params.Id, context.Self?.GetType().Name ?? "null");
            return true;
        }

        // Horizontal facing: the charge runs along the ground, so the aim's vertical component is
        // dropped rather than launching the character at the pitch angle.
        var facing = character.AimDirection;
        facing.Z = 0f;
        if (facing.LengthSquared() < 0.0001f)
        {
            facing = Vector3.Transform(-Vector3.UnitY, character.Orientation);
            facing.Z = 0f;
        }

        if (facing.LengthSquared() < 0.0001f)
        {
            return true;
        }

        facing = Vector3.Normalize(facing);

        uint now = context.Shard.CurrentTime;
        var message = new ForcedMovement
        {
            Data = new ForcedMovementData
            {
                Type = (byte)ForcedMovementType.Bullrush,
                Unk1 = 0,
                HaveUnk2 = 0,
                Bullrush = new Bullrush
                {
                    Velocity = facing * speed,
                    StartTime = now,
                    EndTime = now + (uint)duration,
                    Speed = speed,
                },
            },
            ShortTime = context.Shard.CurrentShortTime,
        };

        // Landing the charge must not bill the fall speed accumulated before it started.
        context.Shard.FallDamage?.ResetFor(character);

        if (character.IsPlayerControlled && character.Player != null)
        {
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
            context.Shard.EntityMan?.SendToScoped(character, message, character.Player);
        }
        else
        {
            context.Shard.EntityMan?.SendToScoped(character, message);
        }

        return true;
    }
}
