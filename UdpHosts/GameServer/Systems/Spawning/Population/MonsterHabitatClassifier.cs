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
///     This is PIN's conservative procedural-placement policy, not recovered spawn assignments.
///     The client database names behaviors and requirements but provides no per-zone roster.
///     A template with an unsatisfied route, prop, locomotion or vendor assignment must not gain
///     random coverage/density slots. Explicit entity/ability/debug spawning does not use this gate.
///     See Docs/NPC_PLACEMENT_AUDIT.md for the current full census and remaining limits.
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

    // Explicit sets, not substring matches on arbitrary tree/emote names. These requests need
    // context that the procedural planner does not supply. This does not disable explicit spawns.
    private static readonly HashSet<string> _assignedRouteBehaviors = new(StringComparer.OrdinalIgnoreCase)
    {
        "StockShootAndFollowRoute", "OneOff_FollowRoute", "NavigateToLocation", "Arch_Follower",
        "ProtectVehicle", "TestFollowPlayer",
    };

    // Reviewed prod-1962 base poses: seat, chair, console and leaning placement. Do not infer
    // arbitrary emotes containing these words; unknown emotes remain an audit gap.
    private static readonly HashSet<string> _propDependentEmotes = new(StringComparer.OrdinalIgnoreCase)
    {
        "controlseat", "sittingchair01", "sittingchair03", "sittingchair05",
        "townlean1", "townlean2", "townlean4M", "typing", "typing01",
    };

    /// <summary>
    ///     Behaviour names the game gives to settlement inhabitants: civilians and their city
    ///     wander sets, guards, the interactive NPCs a player talks to or uses, and the NPCs that
    ///     stand or emote. This coarse habitat heuristic applies only after the assignment gate;
    ///     a settlement classification is not an original NPC location.
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
    ///     Nonzero means the template exposes a vendor role. Procedural coverage/density has no
    ///     vendor placement or uniqueness assignment, so it must not scatter copies of that role.
    ///     Explicit spawns remain available. The near-universal terminal_type_name=VENDOR default
    ///     is deliberately not used: it would exclude ordinary wildlife as well.
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

        var parameters = NpcBehaviorParams.Parse(behavior);
        string behaviorName = parameters.Name;
        if (behaviorName.Length > 0 && _neverWorldSpawned.Contains(behaviorName))
        {
            exclusion = $"behaviour {behaviorName}";
            return false;
        }

        if (_assignedRouteBehaviors.Contains(behaviorName) ||
            (parameters.Values.TryGetValue("city_prefix", out string prefix) && !string.IsNullOrWhiteSpace(prefix)))
        {
            exclusion = "requires route, named points or follow target";
            return false;
        }

        if ((parameters.TryGetBool("climber", out bool climber) && climber) ||
            (parameters.TryGetBool("grounded", out bool grounded) && !grounded) ||
            (parameters.TryGetBool("inSpawnVolume", out bool inVolume) && inVolume) ||
            (parameters.TryGetFloat("groundOffset", out float offset) && float.IsFinite(offset) && offset > 0f))
        {
            exclusion = "requires unsupported locomotion or spawn volume";
            return false;
        }

        if (behaviorName.Equals("PerformEmoteNoPhysics", StringComparison.OrdinalIgnoreCase) ||
            _propDependentEmotes.Contains(parameters.EmoteName))
        {
            exclusion = "requires assigned prop or pose placement";
            return false;
        }

        if (vendorId != 0)
        {
            exclusion = "requires vendor placement assignment";
            return false;
        }

        WorldPopulationHabitat result = WorldPopulationHabitat.None;

        if (behaviorName.Length > 0 && _settlementBehaviors.Contains(behaviorName))
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
