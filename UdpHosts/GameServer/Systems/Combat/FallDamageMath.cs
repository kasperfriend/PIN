using System;

namespace GameServer.Systems.Combat;

/// <summary>
///     Pure description of a landing, consumed by <see cref="FallDamageMath" />.
/// </summary>
public readonly record struct FallImpactContext(
    float ImpactSpeed,
    float AirTimeMs,
    bool InWater,
    bool UsedThrusterOrGlider,
    bool KnockdownFall,
    bool ImmuneToFallDamage);

public readonly record struct FallDamageResult(
    int Damage,
    bool Lethal,
    float ExcessSpeed);

/// <summary>
///     Pure fall damage math so the tuning can be unit tested without entities.
///     A fall deals damage when it is long enough, the impact speed exceeds the
///     safe speed, and nothing negated it (water, thrusters/gliders, knockdowns,
///     the immune falldamage combat flag).
/// </summary>
public static class FallDamageMath
{
    public static FallDamageResult Evaluate(in FallImpactContext context, IFallDamageRules rules, float damageTakenMultiplier = 1f)
    {
        if (rules == null || !rules.Enabled || context.ImpactSpeed <= 0f)
        {
            return default;
        }

        if (context.InWater || context.UsedThrusterOrGlider || context.KnockdownFall || context.ImmuneToFallDamage)
        {
            return default;
        }

        if (context.AirTimeMs < rules.MinAirTimeMs)
        {
            return default;
        }

        var excess = context.ImpactSpeed - rules.SafeImpactSpeed;
        if (excess <= 0f)
        {
            return default;
        }

        bool lethal = context.ImpactSpeed >= rules.LethalImpactSpeed;
        var damage = (int)MathF.Round(excess * rules.DamagePerSpeed * damageTakenMultiplier);

        return new FallDamageResult(damage, lethal, excess);
    }
}
