using System;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.NpcDeath;

/// <summary>
///     Resolves the gib visuals a corpse plays, the way the static database describes it: a battleframe's
///     <c>dbitems::Battleframe.gibset_id</c> is a reference into <c>dbcharacter::GibVisuals</c>, and that row is what
///     the client applies - together with the time of death - to play the death animation and the gib visuals.
/// </summary>
/// <remarks>
///     <para>
///     The database's default row - <c>dbcharacter::GibVisuals</c> id 0 - is a row like any other, and
///     <c>gibset_id</c> 0 is by far the most common value in the build (804 of the 1,676 battleframes and 3,816 of the
///     3,902 deployables). It is a value, not an absence, and the sibling death path always treated it as one: a dead
///     deployable reports <c>dbcharacter::Deployable.gibset_id</c> as-is, 0 included (<c>DamageSystem</c>). A monster
///     whose battleframe says 0 therefore reports gib visuals 0 at its death time instead of staying silent, which is
///     what the 1,806 monsters with no explicit gib set used to do. Nothing is invented: the id is the one the
///     battleframe row names, and a client that treats row 0 as the default plays the default death, which is what
///     those monsters play in the original game.
///     </para>
///     <para>
///     This only fails where the database genuinely has nothing to say: a character with no chassis (89 of the 3,109
///     monster rows are legacy entries with <c>chassis_id</c> 0) or a chassis without a <c>dbitems::Battleframe</c> row
///     (23 further rows). The 54 monsters whose battleframe names a <c>GibVisuals</c> id the build does not ship still
///     resolve to that id: the server reports what the database says and the client resolves it against its own copy.
///     </para>
/// </remarks>
public static class GibVisualsResolution
{
    /// <summary>Resolves the gib visuals id for a character, or false when the database has none for it.</summary>
    /// <param name="chassisId">The character's chassis id, 0 when it has none (monsters carry theirs in <c>chassis_id</c>).</param>
    /// <param name="getBattleframe">The static database's battleframe lookup.</param>
    /// <param name="gibVisualsId">The battleframe's <c>gibset_id</c>, 0 included.</param>
    public static bool TryResolve(uint chassisId, Func<uint, Battleframe> getBattleframe, out uint gibVisualsId)
    {
        gibVisualsId = 0;
        if (chassisId == 0)
        {
            return false;
        }

        var battleframe = getBattleframe(chassisId);
        if (battleframe == null)
        {
            return false;
        }

        gibVisualsId = battleframe.GibsetId;
        return true;
    }
}
