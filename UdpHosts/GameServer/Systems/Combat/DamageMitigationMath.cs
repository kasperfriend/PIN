using System;

namespace GameServer.Systems.Combat;

/// <summary>
/// Pure damage-response math shared by the combat pipeline and unit tests.
/// </summary>
/// <remarks>
/// Firefall separates a target's general damage response from the response for a
/// particular damage type. A target's active <c>DamageTaken</c> aptitude stat is
/// a third multiplier. Keeping the arithmetic here makes the rules explicit and
/// prevents the network-facing damage systems from each rounding a hit
/// differently.
/// </remarks>
public static class DamageMitigationMath
{
    /// <summary>
    /// Resolves the response multiplier for a damage type.
    /// </summary>
    /// <param name="defaultMultiplier">
    ///     The target response's default multiplier. A missing response uses 1.
    /// </param>
    /// <param name="damageTypeMultiplier">
    ///     The response row for the incoming damage type, when one exists. A
    ///     type-specific row replaces the response default rather than being
    ///     multiplied by it.
    /// </param>
    public static float ResolveResponseMultiplier(float defaultMultiplier, float? damageTypeMultiplier)
    {
        return SanitizeMultiplier(damageTypeMultiplier ?? defaultMultiplier);
    }

    /// <summary>
    /// Applies the target response and its active DamageTaken multiplier.
    /// </summary>
    public static int Apply(
        int rawDamage,
        float damageTakenMultiplier,
        float defaultResponseMultiplier = 1f,
        float? damageTypeMultiplier = null)
    {
        if (rawDamage <= 0)
        {
            return 0;
        }

        float responseMultiplier = ResolveResponseMultiplier(defaultResponseMultiplier, damageTypeMultiplier);
        float totalMultiplier = SanitizeMultiplier(damageTakenMultiplier) * responseMultiplier;
        if (totalMultiplier <= 0f)
        {
            return 0;
        }

        double scaled = rawDamage * (double)totalMultiplier;
        if (double.IsNaN(scaled) || scaled <= 0d)
        {
            return 0;
        }

        if (double.IsInfinity(scaled) || scaled >= int.MaxValue)
        {
            return int.MaxValue;
        }

        // Damage is carried as an integer on the wire. Away-from-zero matches
        // the weapon and aptitude damage paths and keeps 0.5 from disappearing.
        int rounded = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    private static float SanitizeMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || multiplier <= 0f)
        {
            return 0f;
        }

        return multiplier;
    }
}
