using System.Numerics;
using AiPrng = GameServer.Systems.PRNG.PRNG;

namespace GameServer.Systems.Ai;

/// <summary>
///     The pure half of an NPC's weapon spread: turning the static database's spread columns into
///     the cone one standing shot is scattered in, and applying that cone to an aim direction the
///     same way a player shot is scattered.
/// </summary>
/// <remarks>
///     <para>
///     A player's spread is a running state: heat and accumulated cone grow as they fire, movement
///     and agility change the floor, and the cone returns over <c>ms_spread_return</c>. An NPC has
///     none of that. It rests between bursts for the behaviour's <c>fireRestDuration</c> (median
///     1,000 ms across the build's ranged humanoids), which outlasts every <c>ms_spread_return</c>
///     the NPC weapons carry, so there is no heat to keep between attacks. The cone of one attack
///     is therefore the weapon's own first-shot cone - the same number a standing player would get
///     from the same row on their first trigger pull, with no accumulated heat, no movement add and
///     agility 1.
///     </para>
///     <para>
///     That number is <c>MinSpread + StartingSpread × (MaxSpread − MinSpread)</c>, with the item's
///     attribute 958 (Weapon Spread) scaling both terms the way
///     <c>WeaponSpreadProfile.Build</c> scales them for a player. A weapon the database gives no
///     spread at all (every melee row, and the ranged rows whose min/max/starting are 0) resolves
///     to 0 and fires along the aim, which is what those rows asked for and what the AI did before
///     this surface existed.
///     </para>
///     <para>
///     One attack then spends that cone through the same <c>PRNG.Spread</c> a player shot uses, once
///     per round in the burst, so a shotgun's 16 pellets scatter inside the cone instead of stacking
///     on a single chest-aimed ray. The seed is the shard time, the weapon's <c>slot_index</c> and
///     the round number - the three inputs the player path already uses - so the same fight replays
///     the same way.
///     </para>
/// </remarks>
public static class NpcAttackSpreadMath
{
    /// <summary>
    ///     Below this, <c>PRNG.Spread</c> returns the aim unchanged. Used as the "this weapon has no
    ///     spread" threshold so a 0 row and a rounding crumb are the same no-op.
    /// </summary>
    public const float MinimumSpreadPct = 0.001f;

    /// <summary>
    ///     Resolves the spread percent a standing NPC fires at, per the rules in the type's remarks.
    /// </summary>
    /// <param name="minSpread">Template <c>min_spread</c> (with item/slot modifiers already applied).</param>
    /// <param name="maxSpread">Template <c>max_spread</c>.</param>
    /// <param name="startingSpread">Template <c>starting_spread</c>, the fraction of the (max − min) band the first shot opens at.</param>
    /// <param name="itemSpreadAttribute">Attribute 958 of the weapon item, or 0 when it has no row.</param>
    public static float ResolveSpreadPct(float minSpread, float maxSpread, float startingSpread, float itemSpreadAttribute)
    {
        float baseSpreadPct;
        float otherSpreadPct;

        // Attribute 958 is the item's own scale on the template cone, identical to WeaponSpreadProfile.Build:
        // present and the template has a max, it is a fraction of that max; present with no max, it is the
        // cone itself; missing, the template columns stand.
        if (itemSpreadAttribute > 0f)
        {
            if (maxSpread > 0f)
            {
                float scale = itemSpreadAttribute / maxSpread;
                baseSpreadPct = minSpread * scale;
                otherSpreadPct = (maxSpread - minSpread) * scale;
            }
            else
            {
                baseSpreadPct = minSpread * itemSpreadAttribute;
                otherSpreadPct = itemSpreadAttribute - baseSpreadPct;
            }
        }
        else
        {
            baseSpreadPct = minSpread;
            otherSpreadPct = maxSpread - minSpread;
        }

        // Same clamp GetCurrentSpreadPct applies to a standing first shot: the cone sits between the
        // floor (min) and the ceiling (max), so a starting_spread outside [0, 1] cannot push it past
        // either. A weapon the database gives no spread (every term 0) stays 0.
        float spread = baseSpreadPct + (startingSpread * otherSpreadPct);
        float floor = baseSpreadPct;
        float ceiling = baseSpreadPct + otherSpreadPct;
        if (spread < floor)
        {
            spread = floor;
        }

        if (spread > ceiling)
        {
            spread = ceiling;
        }

        return spread > 0f ? spread : 0f;
    }

    /// <summary>
    ///     Scatters <paramref name="aimForward" /> inside <paramref name="spreadPct" /> through the
    ///     same <c>PRNG.Spread</c> a player shot uses. A cone below <see cref="MinimumSpreadPct" />
    ///     (or a degenerate aim) is a no-op, so a weapon the database gives no spread still fires
    ///     along the aim.
    /// </summary>
    /// <param name="aimForward">Unit aim direction, typically from the muzzle to the target's chest.</param>
    /// <param name="spreadPct">The cone <see cref="ResolveSpreadPct" /> resolved, in the same units <c>PRNG.Spread</c> reads.</param>
    /// <param name="time">Shard time of the attack, the PRNG's time seed.</param>
    /// <param name="slotIndex">The weapon template's <c>slot_index</c>, the PRNG's slot seed.</param>
    /// <param name="round">Round index inside the burst, so two pellets of the same volley do not land on the same ray.</param>
    /// <param name="lastSpreadDirection">Previous round's result, or zero for the first pellet of a burst.</param>
    /// <param name="lastSpreadTime">Time the previous round was scattered at, or <paramref name="time" /> for the first pellet.</param>
    public static Vector3 Apply(
        Vector3 aimForward,
        float spreadPct,
        uint time,
        byte slotIndex,
        byte round,
        Vector3 lastSpreadDirection,
        uint lastSpreadTime)
    {
        if (spreadPct < MinimumSpreadPct || aimForward.LengthSquared() < 0.0001f)
        {
            return aimForward;
        }

        Vector3 forward = Vector3.Normalize(aimForward);

        // The spread basis is the character's own axes: right is aim × world +Z (the same up the body
        // orientation is a yaw about), falling back to aim × world +X when the aim is straight up or
        // down and that cross vanishes. Player WeaponSim does not need the fallback - a client aim is
        // almost never vertical - but an NPC shooting a target on a crate above it can be.
        Vector3 aimRight = Vector3.Cross(forward, Vector3.UnitZ);
        if (aimRight.LengthSquared() < 0.0001f)
        {
            aimRight = Vector3.Cross(forward, Vector3.UnitX);
        }

        aimRight = Vector3.Normalize(aimRight);
        Vector3 aimUp = Vector3.Normalize(Vector3.Cross(aimRight, forward));

        AiPrng.Spread(time, slotIndex, round, forward, aimRight, aimUp, spreadPct, lastSpreadDirection, lastSpreadTime, out Vector3 result);
        return result.LengthSquared() < 0.0001f ? forward : result;
    }
}
