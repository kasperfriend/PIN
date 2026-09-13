using System;
using System.Collections.Generic;
using System.Numerics;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Systems.Combat;

/// <summary>
///     Pure stumble rules from <c>dbcharacter::Stumble</c> / <c>StumbleDirection</c>. The
///     tables name the animation, the status effect, the cooldown and the four hit-direction
///     substates; they do not name which weapon or damage type causes a stumble.
/// </summary>
public static class StumbleMath
{
    /// <summary>Hit from in front of the victim (<c>StumbleDirection.anim_substate</c> 0).</summary>
    public const byte Front = 0;

    /// <summary>Hit from the victim's right (<c>anim_substate</c> 1).</summary>
    public const byte Right = 1;

    /// <summary>Hit from behind the victim (<c>anim_substate</c> 2).</summary>
    public const byte Back = 2;

    /// <summary>Hit from the victim's left (<c>anim_substate</c> 3).</summary>
    public const byte Left = 3;

    /// <summary>
    ///     The <c>anim_substate</c> of a hit arriving along <paramref name="hitOffset" />
    ///     (attacker minus victim), relative to the victim's horizontal facing. Degenerate
    ///     facing or offset is the front substate.
    /// </summary>
    public static byte AnimSubstate(Vector3 victimForward, Vector3 hitOffset)
    {
        var forward = Flatten(victimForward);
        var incoming = Flatten(hitOffset);
        if (forward.LengthSquared() <= 0.0001f || incoming.LengthSquared() <= 0.0001f)
        {
            return Front;
        }

        forward = Vector3.Normalize(forward);
        incoming = Vector3.Normalize(incoming);

        // Signed yaw of the incoming vector relative to facing: +X is the model's right
        // (see CharacterEntity.CalculateProjectileOrigin). Positive yaw is to the right,
        // so a hit from +X of a +Y facing is the right substate.
        float cross = (forward.Y * incoming.X) - (forward.X * incoming.Y);
        float dot = (forward.X * incoming.X) + (forward.Y * incoming.Y);
        float degrees = MathF.Atan2(cross, dot) * (180f / MathF.PI);

        if (degrees >= -45f && degrees < 45f)
        {
            return Front;
        }

        if (degrees >= 45f && degrees < 135f)
        {
            return Right;
        }

        if (degrees >= -135f && degrees < -45f)
        {
            return Left;
        }

        return Back;
    }

    /// <summary>
    ///     Whether a stumble row may fire now: <c>only_once</c> blocks a second play of that
    ///     row, and <c>cooldown_ms</c> blocks a repeat inside the cooldown. A zero cooldown
    ///     is no cooldown.
    /// </summary>
    public static bool CanApply(uint now, uint lastAppliedAt, uint cooldownMs, bool onlyOnce, bool alreadyApplied)
    {
        if (onlyOnce && alreadyApplied)
        {
            return false;
        }

        if (cooldownMs == 0 || lastAppliedAt == 0)
        {
            return true;
        }

        int elapsed = unchecked((int)(now - lastAppliedAt));
        return elapsed < 0 || elapsed >= (int)cooldownMs;
    }

    /// <summary>
    ///     The first <c>dbcharacter::Stumble</c> row that has direction children, i.e. the
    ///     combat directional set (ids 1-32 in prod-1962). Special <c>only_once</c> rows have
    ///     no directions and are not a hit reaction.
    /// </summary>
    public static Stumble CombatStumble(IReadOnlyDictionary<uint, Stumble> stumbles, IReadOnlyDictionary<uint, IReadOnlyList<StumbleDirection>> directions)
    {
        if (stumbles == null || directions == null)
        {
            return null;
        }

        Stumble best = null;
        foreach (var pair in stumbles)
        {
            if (!directions.TryGetValue(pair.Key, out var rows) || rows == null || rows.Count == 0)
            {
                continue;
            }

            if (best == null || pair.Key < best.Id)
            {
                best = pair.Value;
            }
        }

        return best;
    }

    /// <summary>
    ///     The direction row of <paramref name="stumbleId" /> whose <c>anim_substate</c> matches,
    ///     or null when that stumble has none. Several rows can share a substate; the first is
    ///     the deterministic pick (the table does not rank them).
    /// </summary>
    public static StumbleDirection DirectionFor(IReadOnlyList<StumbleDirection> directions, byte animSubstate)
    {
        if (directions == null)
        {
            return null;
        }

        foreach (var row in directions)
        {
            if (row != null && row.AnimSubstate == animSubstate)
            {
                return row;
            }
        }

        return null;
    }

    private static Vector3 Flatten(Vector3 value) => new(value.X, value.Y, 0f);
}
