using System;
using System.Collections.Generic;
using GameServer.Systems.Ai;

namespace GameServer.Systems.Spawning.Population;

/// <summary>
///     Reads a <c>dbcharacter::Monster</c> row and answers the two questions world population
///     needs: may this row be spawned as ambient world content at all, and if so which kinds of
///     ground does it belong to? Pure and side effect free so it can be tested without a loaded
///     database; <see cref="SdbWorldPopulationDataSource"/> supplies the row fields.
/// </summary>
/// <remarks>
///     <para>
///         Every rule here is derived from columns that exist in the shipped database. What the
///         database does <b>not</b> contain is which zone a given monster was meant for: the live
///         server's spawn groups held that, and they never shipped in <c>clientdb.sd2</c> (there is
///         no table with per-zone monster positions, and the faction tables carry no zone link).
///         So the zone filter is what the data can answer for: the habitat a cell has (does this
///         zone contain outposts / Melding at all?) and the level band the area carries. A monster
///         that only belongs to settlements is therefore only placed in zones that have settlements.
///     </para>
///     <para>
///         Against the 3,109 monster rows of the shipped database this admits 2,853: 89 rows carry
///         neither a chassis nor a posetype (there is nothing to render or collide), and 167 rows
///         ask for behaviour that is not a free roaming world entity. Of the admitted rows 1,023
///         fit settlements, 1,731 the wilderness and 351 the Melding.
///     </para>
/// </remarks>
public static class MonsterHabitatClassifier
{
    /// <summary>
    ///     <c>dbcharacter::Faction.internal_name</c> of the Melding. Its creatures (and every row
    ///     whose behaviour is one of the Melding sets) live at the Melding.
    /// </summary>
    public const string MeldingFactionName = "melding";

    /// <summary>
    ///     <c>dbcharacter::Faction.internal_name</c> of the Chosen, who arrive through the Melding
    ///     and push out into the field: their rows fit both.
    /// </summary>
    public const string ChosenFactionName = "chosen";

    /// <summary>
    ///     Behaviour names that are not free roaming world content. Each entry is a behaviour the
    ///     game attaches to something that is not an inhabitant of the zone:
    ///     <list type="bullet">
    ///         <item><c>Null</c> (173 rows) — the row explicitly asks for no AI at all.</item>
    ///         <item>
    ///             <c>PlayerPet</c>, <c>PassivePet</c>, <c>Pet_Earthbreaker</c>, <c>TestElfPet</c>,
    ///             <c>TestFollowPlayer</c> — pets follow their owner; they are created by the owner's
    ///             ability, not by the world.
    ///         </item>
    ///         <item>
    ///             <c>EngineerTurret</c>, <c>EngineerTurretTeleporter</c>,
    ///             <c>TurretTeleporterDropshipCannon</c>, <c>TurretTeleporterTarget</c> — turret and
    ///             teleporter props, deployed by a player or owned by another entity.
    ///         </item>
    ///         <item>
    ///             <c>Elevator</c>, <c>DoorUpInteract</c> — fixtures of a level, driven by the level's
    ///             own logic.
    ///         </item>
    ///         <item>
    ///             <c>AvoidMatt</c>, <c>CraterTest</c>, <c>Config</c>, <c>Meta</c>, <c>MRU</c>,
    ///             <c>_inst</c> — development leftovers, named for the developer and the tool.
    ///         </item>
    ///     </list>
    /// </summary>
    private static readonly HashSet<string> _neverWorldSpawned = new(StringComparer.OrdinalIgnoreCase)
    {
        "Null",
        "PlayerPet",
        "PassivePet",
        "Pet_Earthbreaker",
        "TestElfPet",
        "TestFollowPlayer",
        "EngineerTurret",
        "EngineerTurretTeleporter",
        "TurretTeleporterDropshipCannon",
        "TurretTeleporterTarget",
        "Elevator",
        "DoorUpInteract",
        "AvoidMatt",
        "CraterTest",
        "Config",
        "Meta",
        "MRU",
        "_inst",
    };

    /// <summary>
    ///     Behaviour names the game gives to settlement inhabitants: civilians and their city
    ///     wander sets, guards, the interactive NPCs a player talks to or uses, and the NPCs that
    ///     stand, pose or emote where they were put. Together with the vendor rule below these are
    ///     1,023 of the 3,109 rows.
    /// </summary>
    private static readonly HashSet<string> _settlementBehaviors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alert",
        "AlertAndInteractive",
        "AlertAndLookAtPlayer",
        "BasicCivilian",
        "BasicCivilian_Stationary",
        "CivilianDialog",
        "GuardCityWanderer",
        "InteractiveWithEmote",
        "PeacetimeCityWanderer",
        "PeacetimeCityWandererCore",
        "PeacetimeCityWandererWithHealing",
        "PerformEmote",
        "PerformEmoteNoPhysics",
        "Stand",
        "StandStill",
        "StationaryCivilianDialog",
        "TraumaDoc",
        "UseAbilityOnInteract",
        "UseAbilityOnInteract_Dialog",
        "WanderWithEmoteVocalized",
    };

    /// <summary>
    ///     Behaviour names that name the Melding outright (<c>MeldingWyrm</c>,
    ///     <c>MeldingTornado</c>, <c>GiantMeldingSalamander</c>, ...). Rows carrying them live at
    ///     the Melding whatever faction the row says, because a couple of the Melding's creatures
    ///     are filed under other factions (<c>MeldingAcolyte</c> under gaea, <c>MeldingPuker</c>
    ///     under chosen).
    /// </summary>
    private const string MeldingBehaviorPrefix = "Melding";

    /// <summary>
    ///     Classifies one monster row.
    /// </summary>
    /// <param name="behavior">
    ///     The raw <c>dbcharacter::Monster.behavior</c> string. Its name (the part before the
    ///     first <c>(</c>) is what decides the habitat; it is read with
    ///     <see cref="NpcBehaviorParams.Parse"/>, the same parser the AI uses, so a behaviour and
    ///     its arguments are understood identically everywhere.
    /// </param>
    /// <param name="vendorId">
    ///     <c>dbcharacter::Monster.vendor_id</c>. A row that sells something is a settlement NPC:
    ///     vendors stand behind their counter in a POI, never in the field. 102 rows carry one.
    ///     (<c>terminal_type_name</c> is not usable as a signal on its own: 3,087 of the 3,109
    ///     rows carry its <c>VENDOR</c> default, including the wildlife.)
    /// </param>
    /// <param name="factionInternalName">
    ///     <c>dbcharacter::Faction.internal_name</c> of the row's <c>faction_id</c>, or null when
    ///     the row has no faction.
    /// </param>
    /// <param name="hasRepresentation">
    ///     Whether the row has a chassis or a posetype. A row with neither has nothing the client
    ///     can draw and nothing the server can collide with, so it is not spawnable content —
    ///     <see cref="Entities.Character.CharacterEntity.LoadMonster"/> keeps such a row alive with
    ///     a synthesized sphere, which is a debug affordance, not a monster.
    /// </param>
    /// <param name="habitat">The ground kinds the row fits, or <see cref="WorldPopulationHabitat.None"/>.</param>
    /// <param name="exclusion">Why the row was refused; null when it was admitted.</param>
    /// <returns>Whether the row is world population at all.</returns>
    public static bool TryClassify(
        string behavior,
        uint vendorId,
        string factionInternalName,
        bool hasRepresentation,
        out WorldPopulationHabitat habitat,
        out string exclusion)
    {
        habitat = WorldPopulationHabitat.None;
        exclusion = null;

        if (!hasRepresentation)
        {
            exclusion = "no chassis and no posetype";
            return false;
        }

        string behaviorName = NpcBehaviorParams.Parse(behavior).Name;
        if (behaviorName.Length > 0 && _neverWorldSpawned.Contains(behaviorName))
        {
            exclusion = $"behaviour {behaviorName}";
            return false;
        }

        WorldPopulationHabitat result = WorldPopulationHabitat.None;

        if ((behaviorName.Length > 0 && _settlementBehaviors.Contains(behaviorName)) || vendorId != 0)
        {
            result |= WorldPopulationHabitat.Settlement;
        }

        bool meldingFaction = string.Equals(factionInternalName, MeldingFactionName, StringComparison.OrdinalIgnoreCase);
        bool meldingBehavior = behaviorName.StartsWith(MeldingBehaviorPrefix, StringComparison.OrdinalIgnoreCase);
        if (meldingFaction || meldingBehavior)
        {
            result |= WorldPopulationHabitat.Melding;
        }

        if (string.Equals(factionInternalName, ChosenFactionName, StringComparison.OrdinalIgnoreCase))
        {
            // The Chosen are the Melding's army: they come through it and they patrol the field
            // around it, so their rows fit both.
            result |= WorldPopulationHabitat.Melding | WorldPopulationHabitat.Wilderness;
        }

        if (result == WorldPopulationHabitat.None)
        {
            // Nothing above claimed the row, so it is field content: wildlife, wanderers,
            // minibosses and roaming military. This is also where rows with an empty behaviour
            // string land (1,068 rows, among them real monsters such as the Melded Wyrm and the
            // Chosen Sniper — an empty behaviour only means the AI set is not named, and PIN's AI
            // reads its tuning from the row's other columns).
            result = WorldPopulationHabitat.Wilderness;
        }

        habitat = result;
        return true;
    }
}
