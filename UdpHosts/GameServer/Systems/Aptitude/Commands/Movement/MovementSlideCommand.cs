using System.Numerics;
using BepuUtilities;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Movement;

/// <summary>
///     Moves the character the offsets of a <c>aptfs::MovementSlideCommandDef</c> describe: the database's
///     displacement, over the row's duration.
/// </summary>
/// <remarks>
///     <para>
///     This is the motion behind several attacks whose animation only plays without it today. The clearest is
///     the dodge pair of the humanoid behaviour sets: ability 33833 applies effect 1282 and 33812 applies 1247,
///     and each effect's apply chain is <c>StatModifier</c> -&gt; <c>CombatFlags(restrict
///     movement/weapon/abilities/melee)</c> -&gt; <c>tfAbilityAnimation 10/9</c> -&gt;
///     <c>MovementSlide</c> - the rows state <c>offset_x -5</c> / <c>+5</c> over <b>667 ms</b>, i.e. a 5 m
///     sidestep left or right (5 m / 0.667 s = 7.5 m/s). The Move Then Fire module (86132 -&gt; ability 36817)
///     states <c>offset_y 20</c> over 1000 ms toward its current target (<c>offset_target 1</c>), 20 m/s, with
///     an along-the-aim variant for when it has no target. Ability 39066 rises <c>offset_z 20</c> over 2400 ms
///     and then drifts <c>offset_z 0.1</c> per 600 ms from its update loop; ability 36035 hops
///     <c>offset_y 4, offset_z -2</c> in 200 ms.
///     </para>
///     <para>
///     The offset components are the character's own axes, taken from the row fields: <c>offset_x</c> right,
///     <c>offset_y</c> forward, <c>offset_z</c> up - the frame <c>CharacterEntity</c> and the AI use (local +X
///     right, +Y forward, +Z up). The forward axis is replaced by the direction the row asks for:
///     <c>offset_target</c> (toward the context's current target), <c>along_velocity</c> (the character's
///     velocity) or <c>offset_aim</c> (its aim), each falling back to the character's facing when the data it
///     needs is not there. <c>fixed_speed</c>, when the row states one, derives the duration from the distance
///     instead of using <c>move_duration</c> (row 187880 states 2 ms of duration next to a speed of 10, which
///     is what says the duration field is unused there).
///     </para>
///     <para>
///     Only the server's own characters are moved. A player-controlled character's position is the client's to
///     report (that is what <c>allow_prediction</c> is about, and 5 of the 6 mob-reachable rows state 0), so
///     the command does nothing for one - the player's own client runs the slide it predicted. The fields the
///     reachable rows do not use and this build therefore records but does not apply: <c>velocity_type</c>,
///     <c>orientation_type</c>, <c>initiation_position</c>, <c>rollback</c> and the <c>rand_offset_*</c>
///     spread (see Docs/NPC_AI.md, Known gaps).
///     </para>
/// </remarks>
public class MovementSlideCommand : Command, ICommand
{
    private MovementSlideCommandDef Params;

    public MovementSlideCommand(MovementSlideCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character || character.IsPlayerControlled)
        {
            return true;
        }

        var register = context.Register;

        // The three offsets share one register operand (offset_regop), like the strength of a ForcePush: an
        // unset register (NaN) leaves the row's own values alone (AbilitySystem.RegistryOp).
        float offsetX = AbilitySystem.RegistryOp(register, Params.OffsetX, (Operand)Params.OffsetRegop);
        float offsetY = AbilitySystem.RegistryOp(register, Params.OffsetY, (Operand)Params.OffsetRegop);
        float offsetZ = AbilitySystem.RegistryOp(register, Params.OffsetZ, (Operand)Params.OffsetRegop);

        if (offsetX == 0f && offsetY == 0f && offsetZ == 0f)
        {
            return true;
        }

        ResolveAxes(context, character, out var forward, out var right);
        var offset = (right * offsetX) + (forward * offsetY) + (Vector3.UnitZ * offsetZ);

        float distance = offset.Length();
        float durationMs = AbilitySystem.RegistryOp(register, Params.MoveDuration, (Operand)Params.MoveDurationRegop);
        float fixedSpeed = AbilitySystem.RegistryOp(register, Params.FixedSpeed, (Operand)Params.FixedSpeedRegop);
        if (fixedSpeed > 0f)
        {
            durationMs = distance / fixedSpeed * 1000f;
        }

        if (durationMs <= 0f)
        {
            return true;
        }

        context.Shard.Abilities?.RegisterMovementSlide(character, (uint)durationMs, offset);
        return true;
    }

    /// <summary>
    ///     Resolves the character's forward and right axes for this row: the forward axis is where the row
    ///     says to go (<c>offset_target</c>, <c>along_velocity</c>, <c>offset_aim</c>) and the right axis is
    ///     the character's own, so a sideways offset stays sideways whichever way the row is heading.
    /// </summary>
    private void ResolveAxes(Context context, CharacterEntity character, out Vector3 forward, out Vector3 right)
    {
        // The local frame is +X right, +Y forward, +Z up, and a character's orientation is yaw-only, so the
        // inverse orientation carries a local axis into the world (CharacterEntity does the same for a
        // character's own muzzle and its aim).
        var facing = Flatten(QuaternionEx.Transform(Vector3.UnitY, QuaternionEx.Inverse(character.Orientation)));
        var side = Flatten(QuaternionEx.Transform(Vector3.UnitX, QuaternionEx.Inverse(character.Orientation)));

        forward = facing.LengthSquared() > 0.0001f ? Vector3.Normalize(facing) : Vector3.UnitY;
        right = side.LengthSquared() > 0.0001f ? Vector3.Normalize(side) : new Vector3(forward.Y, -forward.X, 0f);

        Vector3? asked = null;
        if (Params.OffsetTarget == 1 && context.Targets.TryPeek(out var target) && target != null)
        {
            asked = target.Position - character.Position;
        }
        else if (Params.AlongVelocity == 1 && character.Velocity.LengthSquared() > 0.0001f)
        {
            asked = character.Velocity;
        }
        else if (Params.OffsetAim == 1 && character.AimDirection.LengthSquared() > 0.0001f)
        {
            asked = character.AimDirection;
        }

        if (asked.HasValue)
        {
            var flat = Flatten(asked.Value);
            if (flat.LengthSquared() > 0.0001f)
            {
                forward = Vector3.Normalize(flat);

                // Keep the character's own chirality for the sideways axis: the local +X axis is the local
                // +Y axis turned -90 degrees about Z, so derive the right axis from the resolved forward.
                right = new Vector3(forward.Y, -forward.X, 0f);
            }
        }
    }

    private static Vector3 Flatten(Vector3 value)
    {
        return new Vector3(value.X, value.Y, 0f);
    }
}
