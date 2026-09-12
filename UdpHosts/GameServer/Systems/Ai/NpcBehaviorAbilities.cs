using System.Collections.Generic;

namespace GameServer.Systems.Ai;

/// <summary>
///     What one of a behaviour set's ability modules does, as far as the AI is concerned: the parsed
///     <c>am1</c>/<c>am2</c> parameters, the ability the module runs and what the chains behind it carry,
///     read the same way a weapon's ability ids are (<see cref="NpcWeaponAbilities.ScanAbility" />).
/// </summary>
/// <param name="Module">The module's own parameters, exactly as the behaviour string states them.</param>
/// <param name="AbilityId">
///     The <c>apt::AbilityData</c> the module runs, resolved through <c>dbitems::AbilityModule</c> (0 when
///     neither the module nor the id itself has an ability row).
/// </param>
/// <param name="ClientFeedback">
///     A command the client runs (<c>apt::CommandType.environment</c> = <c>client</c>) is reachable in the
///     ability's chains.
/// </param>
/// <param name="DeliversDamage">An <c>aptfs::InflictDamageCommandDef</c> or <c>FireProjectileCommandDef</c> is.</param>
public readonly record struct NpcAbilityModuleScan(
    NpcAbilityModule Module,
    uint AbilityId,
    bool ClientFeedback,
    bool DeliversDamage)
{
    /// <summary>
    ///     Whether the engine runs this module: only a chain that carries something a client draws or plays
    ///     produces anything for the server to do, which is the same rule the weapon chains are gated on.
    /// </summary>
    public bool Runnable => ClientFeedback;
}

/// <summary>
///     Resolves the ability modules of a monster behaviour string into what the engine will run. Pure: it
///     reads the module rows and the chains through <see cref="INpcAttackDataSource" />, so it is unit
///     tested without a shard or a database file.
/// </summary>
/// <remarks>
///     <para>
///         The resolution is the database's own two steps: a behaviour string's <c>am*Id</c> names a
///         <c>dbitems::AbilityModule</c> row, and that row's <c>ability_chain_id</c> names the
///         <c>apt::AbilityData</c> to run. Of the 26 module ids the build's monsters name, 25 have a
///         <c>dbitems::AbilityModule</c> row (86132 -&gt; ability 36817, 88159 -&gt; 37359, 82621 -&gt; 35942,
///         ...), and 33812 - the second module of the dodge pair, named by 39 monster rows - has none: the
///         module that points at ability 33812 is 77388, and the string names the ability. That one value is
///         therefore read as an ability id, which the data requires and 25 other values do not need.
///     </para>
///     <para>
///         22 of the 26 modules reach a <c>tfAbilityAnimationCommandDef</c> (animation indices 1-28) through
///         the effect their chains apply, one of them (86132) also reaching a <c>tfPerformEmoteCommandDef</c>
///         with the emote name <c>roar</c>, and 11 of them deliver their own damage; only 120937's chain is
///         server-side alone and is left to the weapon path. See Docs/NPC_AI.md for the full table.
///     </para>
/// </remarks>
public static class NpcBehaviorAbilities
{
    /// <summary>Resolves the modules a behaviour set configures, in the order the database spells them (am1, am2).</summary>
    /// <param name="behavior">A parsed <c>dbcharacter::Monster</c> behaviour string.</param>
    /// <param name="data">The database the module rows and ability chains are read through.</param>
    /// <returns>
    ///     One entry per configured module, empty when the set configures none - the usual case for 3,049 of
    ///     the build's 3,109 monster rows.
    /// </returns>
    public static IReadOnlyList<NpcAbilityModuleScan> Resolve(NpcBehaviorParams behavior, INpcAttackDataSource data)
    {
        var modules = new List<NpcAbilityModuleScan>();
        if (behavior == null || data == null)
        {
            return modules;
        }

        foreach (string prefix in NpcAbilityModule.Prefixes)
        {
            if (!behavior.TryGetAbilityModule(prefix, out NpcAbilityModule module) || module.ModuleId == 0)
            {
                continue;
            }

            // The module table first, the id itself second: 33812 is the one value the build's strings
            // write as an ability id rather than a module id (see the remarks).
            uint abilityId = data.ResolveAbilityModule(module.ModuleId);
            if (abilityId == 0)
            {
                abilityId = module.ModuleId;
            }

            var scan = NpcWeaponAbilities.ScanAbility(data, abilityId);
            modules.Add(new NpcAbilityModuleScan(module, abilityId, scan.ClientFeedback, scan.DeliversDamage));
        }

        return modules;
    }
}
