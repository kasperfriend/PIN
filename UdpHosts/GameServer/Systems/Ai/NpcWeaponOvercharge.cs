namespace GameServer.Systems.Ai;

/// <summary>
///     Whether a weapon's overcharge hook runs for one attack. The columns are
///     <c>dbitems::WeaponTemplates.overcharge_ability</c> and <c>ms_overcharge_delay</c>:
///     the delay is how long a charge must be held before the overcharge VFX applies. An NPC
///     charges for <c>ms_chargeup</c> and then fires, so the hook runs when that charge is at
///     least the delay (plasma 12129: 4000 ms charge, 2500 ms delay). There is no hold-past-max
///     event in this build, and a delay of 0 means the row does not overcharge.
/// </summary>
public static class NpcWeaponOvercharge
{
    /// <summary>
    ///     Whether the overcharge ability should run with this attack: the template names one, the
    ///     delay is non-zero, and the charge the weapon describes is long enough to have crossed it.
    /// </summary>
    public static bool ShouldActivate(uint overchargeAbilityId, uint msOverchargeDelay, uint chargeUpMs)
    {
        return overchargeAbilityId != 0 && msOverchargeDelay > 0 && chargeUpMs >= msOverchargeDelay;
    }
}
