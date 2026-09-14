using System;
using GameServer.Entities.Character;
using GameServer.Extensions;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Aptitude;

namespace GameServer.Systems.ProjectileSim;

/// <summary>
///     Which <c>dbitems::Ammo</c> ability columns fire at which moment of a simulated round.
///     The names are the triggers: <c>ability_id</c> is the terminal impact, <c>touch_ability_id</c>
///     is every contact (a bounce included), <c>airburst_ability_id</c> is a lifetime that ran out
///     without a terminal hit, and <c>period_ability_id</c> ticks every <c>period_ability_ms</c>
///     while the round is still in flight.
/// </summary>
public static class AmmoAbilityHooks
{
    /// <summary><c>touch_ability_id</c> of a contact, or 0 when the row names none.</summary>
    public static uint TouchAbility(Ammo ammo) => ammo?.TouchAbilityId ?? 0;

    /// <summary><c>ability_id</c> of a terminal impact, or 0 when the row names none.</summary>
    public static uint ImpactAbility(Ammo ammo) => ammo?.AbilityId ?? 0;

    /// <summary><c>airburst_ability_id</c> of a lifetime expiry, or 0 when the row names none.</summary>
    public static uint AirburstAbility(Ammo ammo) => ammo?.AirburstAbilityId ?? 0;

    /// <summary>
    ///     Whether a period tick is due: the ammo names a period ability and at least one whole
    ///     <c>period_ability_ms</c> has passed since the last one (or since the muzzle).
    /// </summary>
    public static bool TryPeriod(Ammo ammo, uint elapsedMs, ref uint lastPeriodMs, out uint abilityId)
    {
        abilityId = 0;
        uint periodMs = ammo?.PeriodAbilityMs ?? 0;
        uint periodAbility = ammo?.PeriodAbilityId ?? 0;
        if (periodMs == 0 || periodAbility == 0 || elapsedMs < lastPeriodMs + periodMs)
        {
            return false;
        }

        lastPeriodMs += periodMs;
        if (lastPeriodMs > elapsedMs)
        {
            lastPeriodMs = elapsedMs;
        }

        abilityId = periodAbility;
        return true;
    }

    /// <summary>
    ///     Runs one ammo ability through the shard's aptitude system. No-op when there is no
    ///     system (a test shard), no source, or the ammo named none.
    /// </summary>
    public static void Activate(IShard shard, CharacterEntity source, uint abilityId, IAptitudeTarget target = null)
    {
        if (shard?.Abilities == null || source == null || abilityId == 0)
        {
            return;
        }

        // Ammo rows are data supplied by the client database. An incomplete chain must make this
        // one hook fail, not abort ProjectileSim.Tick before the terminal projectile can be retired.
        // Without this boundary one malformed impact ability re-ran the same collision, damage and
        // stack trace every projectile update and starved the shard's network loop.
        try
        {
            shard.Abilities.HandleActivateAbility(
                shard,
                source,
                abilityId,
                shard.CurrentTime,
                target != null ? new AptitudeTargets(target) : new AptitudeTargets());
        }
        catch (Exception ex)
        {
            // A repeating automatic weapon can legitimately hit the same broken ability many times;
            // log enough to identify the SDB row without turning its diagnostics into another load
            // source for the overloaded server.
            if (OnceLog.ShouldLog((nameof(AmmoAbilityHooks), abilityId, ex.GetType().FullName)))
            {
                shard.Logger.Warning(
                    ex,
                    "Projectile ammo ability {AbilityId} threw and was ignored for this projectile",
                    abilityId);
            }
        }
    }
}
