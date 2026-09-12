using System;

namespace GameServer.Systems.Ai;

/// <summary>
///     The magazine an armed NPC fires from and the reload it performs when it runs dry - the state the
///     <c>CombatView.WeaponReloaded</c> and <c>WeaponReloadCancelled</c> markers are replicated from.
/// </summary>
/// <remarks>
///     <para>
///     A player's reload is client driven: the client sends <c>ReloadWeapon</c> (or <c>CancelReload</c>), the
///     server relays the time through <c>CombatView</c>, and every watching client plays the reload of the
///     weapon the replicated equipment says the character holds (<c>dbitems::WeaponTemplates.anim_reload_type</c>,
///     which 53 of the 85 templates the build's mobs use carry). An NPC has no client to send them, so an armed
///     mob used to fire forever without ever reloading; the engine now spends the magazine the database gives the
///     weapon and announces the reload itself.
///     </para>
///     <para>
///     Every number here is a column: the magazine is the weapon item's attribute 956
///     (<c>WeaponMagazineSize</c>) when it carries one, otherwise the template's <c>base_clip_size</c>; one attack
///     spends <c>ammo_per_burst</c> (the ammo column) when it is set, otherwise <c>rounds_per_burst</c> - a shotgun
///     fires its 16 rounds for one shell - and the reload takes the template's <c>reload_time</c>, the same window
///     the client plays the reload animation in. A one-round clip (the charge sniper rifle) therefore reloads after
///     every shot, and only a row without a reload time at all fires forever.
///     </para>
/// </remarks>
/// <param name="Rounds">Rounds left in the magazine.</param>
/// <param name="ReloadEndTime">Server time (ms) the running reload finishes at, or 0 when none is running.</param>
/// <param name="Tracked">
///     Whether this weapon keeps a magazine at all: <see cref="None" /> is not tracked, and being untracked is
///     what makes it fire forever (every melee row, and the ranged rows the database gives no magazine).
/// </param>
public readonly record struct NpcWeaponMagazine(int Rounds, ulong ReloadEndTime, bool Tracked)
{
    /// <summary>A weapon that does not reload (no magazine, or a melee row): it always fires.</summary>
    public static readonly NpcWeaponMagazine None = default;

    /// <summary>
    ///     Resolves how many rounds the weapon's magazine holds: the item's attribute 956 when it has one,
    ///     otherwise the template's <c>base_clip_size</c>.
    /// </summary>
    /// <param name="attributeMagazineSize">The weapon item's attribute 956 (0 when the row has none).</param>
    /// <param name="templateBaseClipSize">The template's <c>base_clip_size</c>.</param>
    public static int ResolveCapacity(float attributeMagazineSize, ushort templateBaseClipSize)
        => attributeMagazineSize > 0f ? (int)Math.Round(attributeMagazineSize) : templateBaseClipSize;

    /// <summary>
    ///     Resolves how many rounds one attack spends: <c>ammo_per_burst</c> when the template carries it,
    ///     otherwise <c>rounds_per_burst</c>, and never less than one round per attack.
    /// </summary>
    /// <param name="ammoPerBurst">The template's <c>ammo_per_burst</c> (0 when the row has none).</param>
    /// <param name="roundsPerBurst">The template's <c>rounds_per_burst</c>.</param>
    public static int ResolveCost(int ammoPerBurst, int roundsPerBurst)
        => Math.Max(1, ammoPerBurst > 0 ? ammoPerBurst : roundsPerBurst);

    /// <summary>
    ///     Whether this weapon reloads at all: a ranged weapon (projectile firing, not a melee row) whose
    ///     magazine holds at least one round and that the database gives a reload time. Every melee row
    ///     keeps swinging without one, as do the ranged rows whose weapon has no reload time.
    /// </summary>
    /// <param name="isRanged">Whether the resolved attack is a projectile attack (see <see cref="NpcAttackMode" />).</param>
    /// <param name="capacity">Rounds the magazine holds (<see cref="ResolveCapacity" />).</param>
    /// <param name="reloadTimeMs">The template's <c>reload_time</c>.</param>
    public static bool Reloads(bool isRanged, int capacity, uint reloadTimeMs)
        => isRanged && capacity >= 1 && reloadTimeMs > 0;

    /// <summary>A magazine loaded to <paramref name="capacity" />.</summary>
    public static NpcWeaponMagazine Loaded(int capacity) => new(capacity, 0, true);

    /// <summary>Whether a reload is still running at <paramref name="now" />.</summary>
    public bool IsReloading(ulong now) => Tracked && ReloadEndTime != 0 && now < ReloadEndTime;

    /// <summary>Whether a burst of <paramref name="cost" /> rounds can be fired at <paramref name="now" />.</summary>
    public bool CanFire(int cost, ulong now) => !Tracked || (!IsReloading(now) && Rounds >= cost);

    /// <summary>The magazine after one burst of <paramref name="cost" /> rounds.</summary>
    public NpcWeaponMagazine Spend(int cost)
        => Tracked ? this with { Rounds = Math.Max(0, Rounds - cost) } : this;

    /// <summary>The magazine with a reload of <paramref name="reloadTimeMs" /> started at <paramref name="now" />.</summary>
    public NpcWeaponMagazine StartReload(ulong now, uint reloadTimeMs)
        => Tracked && reloadTimeMs > 0 ? this with { ReloadEndTime = now + reloadTimeMs } : this;

    /// <summary>Whether a started reload has run out by <paramref name="now" /> (the caller then refills).</summary>
    public bool HasFinishedReloading(ulong now) => Tracked && ReloadEndTime != 0 && now >= ReloadEndTime;

    /// <summary>The magazine refilled to <paramref name="capacity" />, with no reload running.</summary>
    public NpcWeaponMagazine FinishReload(int capacity) => Tracked ? new NpcWeaponMagazine(capacity, 0, true) : this;

    /// <summary>
    ///     The magazine with a running reload dropped and nothing refilled - the server side of the client's
    ///     <c>CancelReload</c> (the NPC died or despawned mid-reload).
    /// </summary>
    public NpcWeaponMagazine CancelReload() => Tracked ? this with { ReloadEndTime = 0 } : this;
}
