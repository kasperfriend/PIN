using System.Numerics;
using AeroMessages.GSS;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Movement;

/// <summary>
///     <c>aptgss::TeleportCommandDef</c> (id only, 42 rows): moves the current target list to the
///     initiation position of the executing chain. The def carries no destination column, so the
///     position has to come from the context; <see cref="Context.InitPosition" /> is the aptitude
///     initiation position the rest of the movement commands read (<c>ForcePush</c>,
///     <c>ApplyImpulse</c>, <c>MovementSlide</c>). The chain shapes back the target-list reading:
///     they settle the list first and then teleport it - <c>TargetInitiator -> Teleport</c>,
///     <c>TargetTrim -> Teleport</c>, <c>TargetByCharacterState -> Teleport</c>, and the Teleport
///     Beacon's <c>TargetClear -> TargetFromStatusEffect -> Teleport</c>.
///     <para>
///         The move itself follows <c>TeleportServerCommand</c>: write the server position, clear the
///         fall-damage accumulator so a stale fall speed cannot deal damage at the destination, then
///         hand the client a <c>ForcedMovement</c> type 1 so it does not interpolate the whole way
///         across the map. The actor gets it on ReliableGss (a teleport is not something an unreliable
///         packet should be allowed to lose) and the watchers get it through the same
///         <c>EntityManager.SendToScoped</c> path the other announcements use, excluding the actor so
///         it is not sent twice.
///     </para>
///     <para>
///         Documented limit: a recall to a position recorded by an <em>earlier</em> activation - the
///         Teleport Beacon's "back to where I threw it" - would need the destination effect's stored
///         application context, and an id-only def has no column to name that effect. Those rows
///         teleport to the recall's own initiation position, which for a self-targeted recall is the
///         caster's current spot.
///     </para>
/// </summary>
public class TeleportCommand : Command, ICommand
{
    private TeleportCommandDef Params;

    public TeleportCommand(TeleportCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        Vector3 destination = context.InitPosition;
        bool moved = false;

        foreach (IAptitudeTarget target in context.Targets)
        {
            // Only a character can be relocated by this command: the ForcedMovement payload is a
            // character-view message, and the other entity families place themselves through their
            // own spawn/slide paths.
            if (target is not CharacterEntity character)
            {
                Logger.Debug(
                    "{Command} {CommandId} skips {Target}: only a character can be teleported",
                    nameof(TeleportCommand), Params.Id, target);
                continue;
            }

            if (!context.Shard.Entities.TryGetValue(character.EntityId, out _))
            {
                continue;
            }

            character.SetPosition(destination);

            // Don't let the fall speed accumulated before the jump deal damage at the destination.
            context.Shard.FallDamage?.ResetFor(character);

            var forcedMove = new ForcedMovement
            {
                Data = new ForcedMovementData
                {
                    Type = 1,
                    Unk1 = 0,
                    HaveUnk2 = 0,
                    Params1 = new ForcedMovementType1Params
                    {
                        Position = destination,
                        Direction = character.AimDirection,
                        Velocity = Vector3.Zero,
                        Time = context.Shard.CurrentTime + 1,
                    },
                },
                ShortTime = context.Shard.CurrentShortTime,
            };

            if (character.IsPlayerControlled && character.Player != null)
            {
                character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(forcedMove, character.EntityId);
                context.Shard.EntityMan?.SendToScoped(character, forcedMove, character.Player);
            }
            else
            {
                context.Shard.EntityMan?.SendToScoped(character, forcedMove);
            }

            moved = true;
        }

        if (!moved)
        {
            Logger.Debug(
                "{Command} {CommandId} teleported nobody (no character in the target list)",
                nameof(TeleportCommand), Params.Id);
        }

        return true;
    }
}
