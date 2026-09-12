namespace GameServer.Systems.Ai;

/// <summary>
///     One NPC attack animation: the window an NPC is showing its attack in, replicated to every
///     client in scope through <c>CombatView.WeaponBurstFired</c> and
///     <c>WeaponBurstEnded</c>/<c>WeaponBurstCancelled</c>.
/// </summary>
/// <remarks>
///     <para>
///     A player's weapon animation is driven by the client itself: the client sends <c>FireBurst</c>
///     when a burst starts and <c>FireEnd</c>/<c>FireCancel</c> when it ends, and the server only
///     relays the three times to the other clients, which play the animation of the weapon the
///     replicated equipment says the character holds. An NPC has no client to send those commands, so
///     the server has to produce them - that is what this type is for, and why a mob that never
///     animated its attacks now does.
///     </para>
///     <para>
///     The window length is the weapon's own burst timing (<c>ms_burst_duration</c>, else
///     <c>ms_per_burst</c>), clamped so an animation can never outlive the attack cycle that started
///     it. A mob with no resolvable weapon has no row to read, so it uses
///     <see cref="DefaultDurationMs" /> - the unarmed swing - which is documented as the one number
///     here that is not database driven.
///     </para>
/// </remarks>
/// <param name="StartTime">Server time (ms) the attack animation started at.</param>
/// <param name="EndTime">Server time (ms) the attack animation ends at.</param>
public readonly record struct NpcAttackAnimation(ulong StartTime, ulong EndTime)
{
    /// <summary>
    ///     Animation window used when the database has no burst timing for the attack (a monster the
    ///     database gives no weapon at all): a half-second swing. Every weapon template an NPC uses in
    ///     build prod-1962 carries burst timing, so this is the AI's only animation length that is a
    ///     constant rather than a row; it keeps the unarmed swing inside the 1,200 ms rules cooldown.
    /// </summary>
    public const uint DefaultDurationMs = 500;

    /// <summary>An animation that is not running.</summary>
    public static readonly NpcAttackAnimation None = default;

    /// <summary>Whether the animation is still playing at <paramref name="now" />.</summary>
    public bool IsActive(ulong now) => StartTime < EndTime && now < EndTime;

    /// <summary>
    ///     Starts an animation at <paramref name="now" /> lasting <paramref name="durationMs" />. A
    ///     zero duration becomes a single millisecond so the window is never empty (an "end marker" at
    ///     the same instant as the start marker).
    /// </summary>
    public static NpcAttackAnimation Start(ulong now, uint durationMs)
    {
        return new NpcAttackAnimation(now, now + (durationMs > 0 ? durationMs : 1));
    }

    /// <summary>
    ///     Resolves how long one attack's animation runs: the weapon's own burst timing when it has
    ///     any, otherwise the unarmed default, never longer than the attack cycle that started it (so
    ///     the end marker cannot land after the next burst's start marker).
    /// </summary>
    /// <param name="weaponBurstMs">
    ///     The resolved weapon animation window (<c>ms_burst_duration</c> else <c>ms_per_burst</c>), or
    ///     0 for an attack with no weapon row.
    /// </param>
    /// <param name="attackIntervalMs">Milliseconds between two attacks of this NPC (<see cref="NpcAttackProfile.AttackIntervalMs" />).</param>
    public static uint ResolveDurationMs(uint weaponBurstMs, uint attackIntervalMs)
    {
        uint duration = weaponBurstMs > 0 ? weaponBurstMs : DefaultDurationMs;
        return attackIntervalMs > 0 && duration > attackIntervalMs ? attackIntervalMs : duration;
    }
}
