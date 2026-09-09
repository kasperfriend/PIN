using System.Collections.Generic;
using System.Numerics;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Impact;

public class ForcePushCommand : Command, ICommand
{
    private ForcePushCommandDef Params;

    public ForcePushCommand(ForcePushCommandDef par)
: base(par)
    {
        Params = par;
    }

    // The row's strength drives the impulse (the glider pad's row carries 30), pushed straight up like the
    // launch pads of the original game. StrengthRegop 1 folds the chain register into the strength additively
    // (the glider pad chain zeroes the register and then adds +3 per special pad module / +10 for boost
    // effects before reaching ForcePush), so ADD can only make the launch stronger, never weaker; an
    // unset register (NaN) is treated as zero. Loft and impact_position affect direction and impact details
    // that are not read yet either.
    public bool Execute(Context context)
    {
        float strength = Params.Strength;
        if (Params.StrengthRegop == 1 && !float.IsNaN(context.Register))
        {
            strength += context.Register;
        }
        var targets = new List<IAptitudeTarget>(context.Targets);
        // If the ability applies to the deployable itself (e.g. glider ability), push the deployable's owner.
        if (context.Self is CharacterEntity || (context.Self as DeployableEntity)?.Owner != null)
        {
            targets.Add(context.Self);
        }

        var visited = new HashSet<ulong>();
        foreach (IAptitudeTarget target in targets)
        {
            var id = target != null ? target.AeroEntityId.Backing : 0;
            if (id == 0 || !visited.Add(id))
            {
                continue;
            }
            var character = target as CharacterEntity ?? (target as DeployableEntity)?.Owner;
            if (character == null)
            {
                continue;
            }

            if (!character.IsPlayerControlled)
            {
                continue;
            }

            var velocity = new Vector3(character.Velocity[0], character.Velocity[1], character.Velocity[2]);
            velocity.Z += strength;

            // Type 5 is a one-frame velocity impulse, not a held trajectory. The client applies the
            // packet while it is still fresh: the packet's own `ShortTime` is what marks it current
            // (it must be the 16-bit clock now, not the low half of the impulse time), and the
            // impulse itself is stamped a short hop ahead (`Time1 = now+19`). A previous 50-550 ms
            // hold and a now-25 ms rewind both produced the same field result: `ForcePush` fired
            // while the client stayed standing, then `[Glider] Launch handoff ... MoveState=4096
            // AirTime=32767 Airborne=False VelocityZ=0` and the pad retriggered. Upstream PIN and
            // the live client send exactly `Time1 = now+19`, `Time2 = now+20` and
            // `ShortTime = CurrentShortTime`, like the teleport/respawn `ForcedMovement` packets
            // elsewhere in this codebase.
            uint now = context.Shard.CurrentTime;
            uint impulseAt = unchecked(now + 19);
            var player = character.Player;

            // The aptitude gates that keep the launch effects alive (AirborneDuration, RequireMovestate
            // gliding/falling) read the character's *reported* pose, but no post-launch pose can exist until
            // the client has applied this impulse. Without a provisional window the server tore its own
            // launch down on the first duration ticks (see Docs/GLIDER_AND_ADS.md). Seed the movement
            // state nibble so movestate reads agree with the pending window, and open the window until 1.5 s
            // after the server-side wait. MovementRelay closes the window again as soon as the client's
            // poses say the launch is over, so a client that never leaves the ground still ends the launch
            // normally.
            character.MovementStateContainer.MovementStateValue =
                (ushort)((character.MovementStateContainer.MovementStateValue & 0x00FF) | 0x7000);
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
            Logger.Debug("[Glider] ForcePush {CommandId} Target={Target} Strength={Strength} Velocity={Velocity} Start={StartTime} End={EndTime} Short={ShortTime}",
                Params.Id, character.EntityId, strength, velocity, message.Data.Params5.Time1, message.Data.Params5.Time2, message.ShortTime);
            player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }

        return true;
    }
}