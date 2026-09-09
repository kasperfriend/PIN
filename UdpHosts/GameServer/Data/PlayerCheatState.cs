namespace GameServer.Data;

/// <summary>
///     Per-player cheat state, toggled by the admin/debug chat commands
///     (<c>setlevel</c>, <c>setframe</c>, <c>hp</c>, <c>dmg</c>, <c>killaura</c>).
///     Values are deliberately not persisted: they exist for testing the running
///     shard only and reset when the player logs out.
/// </summary>
public class PlayerCheatState
{
    /// <summary>
    ///     Multiplier applied to outgoing damage for this player (weapon hits and
    ///     damaging abilities alike). 1 = unmodified; 0 disables the player's damage
    ///     entirely. &lt; 0 (e.g. <c>dmg -1</c>) means "one hit kills everything".
    /// </summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>
    ///     True while the player's damage is set to one-hit-kill everything.
    /// </summary>
    public bool OneHitKill => DamageMultiplier < 0f;

    /// <summary>
    ///     True while the player is in "kill aura" mode: every living enemy character
    ///     within <see cref="KillAuraRadiusMeters"/> is killed once per second.
    /// </summary>
    public bool KillAura { get; set; }

    /// <summary>
    ///     Radius of the kill aura in metres.
    /// </summary>
    public float KillAuraRadiusMeters { get; set; } = 25f;
}
