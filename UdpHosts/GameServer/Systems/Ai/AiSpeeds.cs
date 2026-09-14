namespace GameServer.Systems.Ai;

/// <summary>
///     Resolves the movement speeds an NPC should use. Monster rows carry
///     <c>normal_speed</c> / <c>fast_speed</c>; most prod-1962 rows use the -1 inherit
///     sentinel (see Docs/NpcMovement). Until the species/pose defaults are recovered,
///     the existing trusted range and configured fallbacks remain compatibility policy.
/// </summary>
public static class AiSpeeds
{
    /// <summary>Whether a speed read from the static database is plausible as metres per second.</summary>
    public static bool IsTrusted(float sdbSpeed, IAiRules rules)
    {
        if (rules == null || float.IsNaN(sdbSpeed) || float.IsInfinity(sdbSpeed))
        {
            return false;
        }

        return sdbSpeed >= rules.MinTrustedSpeed && sdbSpeed <= rules.MaxTrustedSpeed;
    }

    /// <summary>The static database speed when it is plausible, otherwise the fallback.</summary>
    public static float Resolve(float sdbSpeed, float fallback, IAiRules rules)
    {
        return IsTrusted(sdbSpeed, rules) ? sdbSpeed : fallback;
    }
}
