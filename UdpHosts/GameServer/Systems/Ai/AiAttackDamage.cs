using System;

namespace GameServer.Systems.Ai;

/// <summary>
///     The pure half of what a monster's attack is worth: turning the level's damage rating from
///     <c>dbcharacter::MonsterScaling</c> into the damage of one swing. Split out of
///     <see cref="AiEngine" /> so the rule can be asserted on without a shard.
/// </summary>
public static class AiAttackDamage
{
    /// <summary>
    ///     Resolves the damage one NPC attack deals.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <c>dbcharacter::MonsterScaling.damage</c> is a per-level <b>rating</b>, not a per-hit
    ///     amount: on every one of the table's 80 rows it is exactly half of that level's health
    ///     rating, i.e. "how much damage a monster of this level is worth", the same way
    ///     <c>health</c> is "how much a monster of this level is worth". Applied whole to a single
    ///     swing on the AI's ~1 attack per <c>AttackCooldownMs</c> it kills a same-level player in
    ///     one or two hits (the level-45 row is 13,934 against a ~20,000 health pool), so a swing
    ///     commits <paramref name="attackDamageFraction" /> of it instead.
    ///     </para>
    ///     <para>
    ///     The result is never 0 when the monster has a rating at all — a monster that can reach you
    ///     always hurts, even at a level the fraction rounds away — and a monster or level the
    ///     database has no row for (<paramref name="levelDamageRating" /> of 0 or less) falls back
    ///     to <paramref name="fallbackDamage" />, which is already a per-swing number and is
    ///     therefore not scaled. A non-positive fallback means "cannot damage anything".
    ///     </para>
    /// </remarks>
    public static int Resolve(int levelDamageRating, int fallbackDamage, float attackDamageFraction)
    {
        if (levelDamageRating <= 0)
        {
            return fallbackDamage > 0 ? fallbackDamage : 0;
        }

        float fraction = Math.Clamp(attackDamageFraction, 0f, 1f);
        int perHit = (int)MathF.Round(levelDamageRating * fraction, MidpointRounding.AwayFromZero);

        return perHit > 0 ? perHit : 1;
    }
}
