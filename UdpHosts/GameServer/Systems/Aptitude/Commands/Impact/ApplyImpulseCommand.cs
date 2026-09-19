using System;
using System.Collections.Generic;
using System.Numerics;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Impact;

/// <summary>
///     <c>aptfs::ApplyImpulseCommandDef</c>: knocks the chain's targets with a one-frame velocity
///     impulse (the same ForcedMovement type 5 tube <see cref="ForcePushCommand"/> uses) in a
///     direction derived from the row: the target's ground-plane forward (its reported aim,
///     X/Y in the Z-up world) rotated around Z by <c>Yawangle</c> degrees and lifted by
///     <c>Loftangle</c> degrees, scaled by <c>Speed</c> (a negative Speed pulls instead of
///     pushing - the row set carries -90..-1 for that). With <c>Alongvelocity</c> set the base
///     direction is the character's current velocity instead of its facing, which is how the
///     momentum-preserving knockbacks read.
///     <c>SpeedRegop</c> 1 folds the chain register into the impulse strength additively, like
///     ForcePush. <c>Duration</c> and <c>AllowPrediction</c> are recorded for fidelity: every
///     captured impulse this server sends is the one-frame type 5, and the held-trajectory
///     forced-movement type (6, "bullrush cvars") is not established against the client, so a
///     non-zero Duration does not switch packet types.
/// </summary>
public class ApplyImpulseCommand : Command, ICommand
{
    private ApplyImpulseCommandDef Params;

    public ApplyImpulseCommand(ApplyImpulseCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float speed = Params.Speed;
        if (Params.SpeedRegop == 1 && !float.IsNaN(context.Register))
        {
            speed += context.Register;
        }

        if (Params.Duration != 0 || Params.AllowPrediction != 0)
        {
            Logger.Debug(
                "{Command} {CommandId}: Duration {Duration} / AllowPrediction {AllowPrediction} are recorded, but the impulse stays the one-frame ForcedMovement type 5 (see class docs)",
                nameof(ApplyImpulseCommand), Params.Id, Params.Duration, Params.AllowPrediction);
        }

        var targets = new List<IAptitudeTarget>(context.Targets);
        if (context.Self is CharacterEntity || (context.Self as DeployableEntity)?.Owner != null)
        {
            targets.Add(context.Self);
        }

        var visited = new HashSet<ulong>();
        foreach (IAptitudeTarget target in targets)
        {
            var id = target?.AeroEntityId.Backing ?? 0;
            if (id == 0 || !visited.Add(id))
            {
                continue;
            }

            var character = target as CharacterEntity ?? (target as DeployableEntity)?.Owner;
            if (character is not { IsPlayerControlled: true })
            {
                continue;
            }

            var facing = character.AimDirection;
            float facingLen = MathF.Sqrt(facing.X * facing.X + facing.Y * facing.Y);
            if (facingLen > 0.0001f)
            {
                facing = new Vector3(facing.X / facingLen, facing.Y / facingLen, 0f);
            }
            else
            {
                facing = Vector3.UnitX;
            }

            var velocity = new Vector3(character.Velocity[0], character.Velocity[1], character.Velocity[2]);

            Vector3 baseline = facing;
            if (Params.Alongvelocity == 1)
            {
                float velLen2d = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
                if (velLen2d > 0.0001f)
                {
                    baseline = new Vector3(velocity.X / velLen2d, velocity.Y / velLen2d, 0f);
                }
            }

            float yaw = Params.Yawangle * MathF.PI / 180f;
            var heading = new Vector3(
                baseline.X * MathF.Cos(yaw) - baseline.Y * MathF.Sin(yaw),
                baseline.X * MathF.Sin(yaw) + baseline.Y * MathF.Cos(yaw),
                0f);

            float loft = Params.Loftangle * MathF.PI / 180f;
            var dir = new Vector3(
                heading.X * MathF.Cos(loft),
                heading.Y * MathF.Cos(loft),
                MathF.Sin(loft));

            velocity += dir * speed;

            uint now = context.Shard.CurrentTime;
            uint impulseAt = unchecked(now + 19);
            var player = character.Player;

            // Same provisional-launch bookkeeping as ForcePush: the gates keeping the
            // knockback's effect chain alive must not tear it down before the client has
            // applied and reported the airborne pose.
            character.MarkServerLaunchPending(now);
            var message = new ForcedMovement
            {
                Data = new AeroMessages.GSS.ForcedMovementData
                {
                    Type = 5,
                    Unk1 = 0,
                    HaveUnk2 = 0,
                    Params5 = new AeroMessages.GSS.ForcedMovementType5Params
                    {
                        Velocity = velocity,
                        Time1 = impulseAt,
                        Time2 = unchecked(impulseAt + 1),
                        Unk2 = 0
                    }
                },

                ShortTime = context.Shard.CurrentShortTime,
            };
            Logger.Debug("{Command} {CommandId} Target={Target} Speed={Speed} Loft={Loft} Yaw={Yaw} Along={Along} Velocity={Velocity}",
                nameof(ApplyImpulseCommand), Params.Id, character.EntityId, speed, Params.Loftangle, Params.Yawangle, Params.Alongvelocity, velocity);
            player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }

        return true;
    }
}
