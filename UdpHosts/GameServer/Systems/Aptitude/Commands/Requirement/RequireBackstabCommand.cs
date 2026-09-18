using System;
using System.Numerics;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireBackstabCommandDef</c>: passes when the chain's initiator is inside the
///     <c>Backangle</c> cone behind the character the chain runs on. "Behind" is measured against
///     the victim's facing on the ground plane: the horizontal part of its client-reported aim
///     direction (falling back to the orientation's forward axis when no aim has been reported).
///     A zero or negative Backangle means no positional restriction, which matches how the client
///     treats the row when it does not name an attacker.
/// </summary>
public class RequireBackstabCommand : Command, ICommand
{
    private RequireBackstabCommandDef Params;

    public RequireBackstabCommand(RequireBackstabCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Backangle <= 0f)
        {
            return true;
        }

        if (context.Self is not CharacterEntity victim)
        {
            Logger.Debug(
                "{Command} {CommandId} fails because self is not a Character ({SelfType})",
                nameof(RequireBackstabCommand), Params.Id, context.Self?.GetType().Name ?? "null");
            return false;
        }

        if (context.Initiator is not BaseEntity attacker || ReferenceEquals(attacker, victim))
        {
            // No attacker to be stabbed by: the gate cannot be satisfied.
            return false;
        }

        var facing = Horizontal(victim.AimDirection);
        if (facing == Vector3.Zero)
        {
            // Best-effort fallback for a character that has not reported aim yet (e.g. aiming
            // straight up or down): the orientation's forward axis in the Z-up world.
            facing = Horizontal(Vector3.Transform(Vector3.UnitY, victim.Orientation));
            if (facing == Vector3.Zero)
            {
                return false;
            }
        }

        var toAttacker = Horizontal(attacker.Position - victim.Position);
        if (toAttacker == Vector3.Zero)
        {
            // Attacker directly overhead counts as behind: the fight is point-blank.
            return true;
        }

        facing = Vector3.Normalize(facing);
        toAttacker = Vector3.Normalize(toAttacker);

        float cosAngle = Vector3.Dot(facing, toAttacker);
        float angleDeg = MathF.Round(MathF.Acos(Math.Clamp(cosAngle, -1f, 1f)) * (180f / MathF.PI));

        // Backangle is the full width of the cone centred on the rear, so the pass
        // region starts Backangle/2 shy of directly behind the victim.
        return angleDeg >= 180f - (Params.Backangle / 2f);
    }

    private static Vector3 Horizontal(Vector3 v)
    {
        // Firefall's world is Z-up (jetpack flight reads Velocity.Z), so the ground
        // plane is X/Y.
        return new Vector3(v.X, v.Y, 0f);
    }
}
