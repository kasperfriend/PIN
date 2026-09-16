using System;
using System.Collections.Generic;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Data;

/// <summary>
/// What one char-create loadout carries in the slots a character wears: how many
/// of them hold a default PvE module, and the highest level those modules ask
/// for.
/// </summary>
/// <param name="EquippedModuleCount">
/// Default PvE modules in slots the GameServer equips (weapons, abilities,
/// chassis gear). Zero means the loadout gears nothing a PvE character wears.
/// </param>
/// <param name="HighestRequiredLevel">
/// The highest <c>dbitems::RootItem.required_level</c> among those modules: the
/// tier the kit belongs to (1 for a starter kit, 20 for an
/// <c>Elite ... Reward</c> kit, 40 for a <c>Migration 40</c> one).
/// </param>
public readonly record struct StockLoadoutKit(int EquippedModuleCount, int HighestRequiredLevel);

/// <summary>
/// Picks the char-create loadout a battleframe ships with for a player.
///
/// The static database carries several <c>dbcharacter::CharCreateLoadout</c> rows
/// per frame and they are not variations of each other — they are different tiers
/// of the same frame:
/// <list type="bullet">
/// <item><c>Accord &lt;frame&gt; - Player</c> (the five Accord frames, flagged
/// <c>is_starting_loadout</c>): the starter kit, required level 1.</item>
/// <item><c>&lt;frame&gt; Test Stage N Loadout</c>: another required-level-1 kit,
/// and the only complete one the advanced frames have in the slots PIN
/// equips.</item>
/// <item><c>Elite &lt;frame&gt; Reward Loadout</c>: the endgame unlock kit, every
/// module at required level 20 — the one the dev sandbox hands out, and the
/// reason a new character used to look level-capped.</item>
/// <item><c>Migration 40 &lt;frame&gt; Loadout</c>: the same at level 40.</item>
/// <item><c>Astrek "&lt;frame&gt;" - Player</c> / <c>ODM "&lt;frame&gt;" -
/// Player</c>: the advanced frames' PvP kit. Its PvE gear sits in slots 140-156
/// (a per-frame-level ladder from required level 21 upwards) and its weapon and
/// ability slots carry PvP modules only, so a PvE character built from it would
/// be unarmed.</item>
/// </list>
/// The rule is "the lowest tier that still gears a player": a starting loadout
/// wins outright, otherwise the kit whose modules ask for the lowest level does,
/// then the fuller kit, then a <c>- Player</c> name, then the lowest id.
///
/// Pure so the rule can be unit tested without the static database; the same rule
/// is precomputed into <c>ChassisStockLoadouts</c> for the web hosts, which
/// cannot read the SDB at runtime.
/// </summary>
public static class CharCreateLoadoutPicker
{
    /// <summary>Suffix the player-facing loadouts carry, e.g. 'Astrek "Firecat" - Player'.</summary>
    private const string PlayerNameSuffix = "- Player";

    /// <summary>
    /// The loadout a character of this frame starts with, or null when the frame
    /// has no non-dev loadout that gears a PvE character.
    /// </summary>
    /// <param name="loadouts">The frame's char-create loadouts.</param>
    /// <param name="kit">
    /// What each loadout carries in the slots a character wears; a loadout that
    /// equips nothing is skipped, because it would leave the character bare.
    /// </param>
    public static CharCreateLoadout Pick(IEnumerable<CharCreateLoadout> loadouts, Func<CharCreateLoadout, StockLoadoutKit> kit)
    {
        if (loadouts == null)
        {
            return null;
        }

        CharCreateLoadout best = null;
        StockLoadoutKit bestKit = default;

        foreach (var loadout in loadouts)
        {
            if (loadout == null || loadout.IsDev != 0)
            {
                continue;
            }

            var current = kit == null ? default : kit(loadout);
            if (current.EquippedModuleCount <= 0)
            {
                continue;
            }

            if (best == null || Compare(loadout, current, best, bestKit) < 0)
            {
                best = loadout;
                bestKit = current;
            }
        }

        return best;
    }

    /// <summary>
    /// Order: starting loadout, then the lowest tier, then the fuller kit, then a
    /// <c>- Player</c> name, then the lowest id.
    /// </summary>
    private static int Compare(CharCreateLoadout left, StockLoadoutKit leftKit, CharCreateLoadout right, StockLoadoutKit rightKit)
    {
        var starting = (left.IsStartingLoadout != 0 ? 0 : 1).CompareTo(right.IsStartingLoadout != 0 ? 0 : 1);
        if (starting != 0)
        {
            return starting;
        }

        var tier = leftKit.HighestRequiredLevel.CompareTo(rightKit.HighestRequiredLevel);
        if (tier != 0)
        {
            return tier;
        }

        var fullness = rightKit.EquippedModuleCount.CompareTo(leftKit.EquippedModuleCount);
        if (fullness != 0)
        {
            return fullness;
        }

        var player = (IsPlayerNamed(left) ? 0 : 1).CompareTo(IsPlayerNamed(right) ? 0 : 1);
        if (player != 0)
        {
            return player;
        }

        return left.Id.CompareTo(right.Id);
    }

    private static bool IsPlayerNamed(CharCreateLoadout loadout)
    {
        return loadout.Name != null && loadout.Name.EndsWith(PlayerNameSuffix, StringComparison.Ordinal);
    }
}
