using AeroMessages.GSS;
using GameServer.Entities.Character;

namespace GameServer.Systems.CombatLog;

/// <summary>
///     Emits the server's combat log rows to the clients that need them.
///
///     The live server reports every status effect it applies to or removes from a character through both
///     <c>PublicCombatLog</c> (BaseController and ObserverView) and <c>PrivateCombatLog</c> (BaseController
///     only). The rows are not cosmetic: the 2014-09-19 gameplay capture shows the scope (ADS) answer as
///     the fire mode, the status effect slot, and then
///     <c>{ SourceType = Weapon, StatusFxApplied, &lt;scope statusfx&gt;, &lt;client UseScope time&gt; }</c>,
///     and the scope status effect's duration chain carries a <c>tfRequireServerConfirmed</c> gate that only
///     that row releases on the client. Without the row, the client's own predicted scope effect is removed
///     by its duration chain about a second after the sights come up — the ADS flicker.
///
///     Only the owner-facing emitters are implemented so far; that is the route the capture verifies and
///     the one the confirmation logic consumes. ObserverView-bound rows (so scoped-in players see other's
///     effect bursts in the combat bar) are still open.
/// </summary>
public interface ICombatLogSink
{
    /// <summary>
    ///     Report a status effect application on a character, as live does (Public + Private, owner only).
    /// </summary>
    /// <param name="target">The character the effect landed on; rows go to its controlling player.</param>
    /// <param name="source">The row's source attribution (Weapon for the scope effect, like the capture).</param>
    /// <param name="effectId">The statusfx id.</param>
    /// <param name="time">The event time, the same value the status effect slot carries.</param>
    void EmitApplyToOwner(CharacterEntity target, CombatLogRow.CombatSourceType source, uint effectId, uint time);

    /// <summary>
    ///     Report a status effect removal on a character, as live does (Public + Private, owner only).
    ///     The row carries the server's clear time, the same value the slot clear is stamped with.
    /// </summary>
    void EmitRemoveFromOwner(CharacterEntity target, CombatLogRow.CombatSourceType source, uint effectId, uint serverTime);
}
