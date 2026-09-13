namespace GameServer.Systems.Ai;

/// <summary>
///     Per-NPC overrides for the combat numbers the brain otherwise reads from <see cref="IAiRules" />.
///     Built from a resolved <see cref="NpcAttackProfile" /> so a mob fights with its own weapon's
///     reach and cadence instead of the one-size-fits-all rules values, while a mob with no resolvable
///     weapon (<see cref="None" />, every field zero) keeps exactly the rules behaviour.
/// </summary>
/// <param name="AttackRange">Reach in metres, or 0 to use the rules value.</param>
/// <param name="AttackRangeExit">Distance the attack state lets go at, or 0 to use the rules value.</param>
/// <param name="StandoffRange">Distance the NPC tries to keep, or 0 to use the rules value.</param>
/// <param name="AttackCooldownMs">Milliseconds between attacks, or 0 to use the rules value.</param>
/// <param name="Ranged">Whether the NPC's attack is a projectile attack (widens the height gate to the weapon's range).</param>
public readonly record struct AiCombatTuning(
    float AttackRange,
    float AttackRangeExit,
    float StandoffRange,
    int AttackCooldownMs,
    bool Ranged)
{
    /// <summary>No overrides: every value falls back to <see cref="IAiRules" />.</summary>
    public static readonly AiCombatTuning None = default;

    /// <summary>Builds the overrides from a resolved weapon profile.</summary>
    public static AiCombatTuning FromProfile(NpcAttackProfile profile)
    {
        if (profile == null || !profile.HasWeapon || profile.AttackRange <= 0f)
        {
            return None;
        }

        return new AiCombatTuning(
            profile.AttackRange,
            profile.AttackRangeExit,
            profile.StandoffRange,
            (int)profile.AttackIntervalMs,
            profile.IsRanged);
    }
}
