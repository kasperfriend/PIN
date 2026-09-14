using System.Collections.Generic;
using GameServer.Extensions;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using Serilog;

namespace GameServer.Systems.Combat;

public class FactionHostility
{
    /// <summary>
    ///     The faction id an entity carries when it has no faction at all. It is not a gap in the
    ///     relation table: 17 <c>dbcharacter::Monster</c> rows ship it (the peacetime city wanderers
    ///     among them), so every stance question about one of them used to miss the table and log a
    ///     warning per lookup - twice a second per NPC, from the AI's target scan, which is a wall
    ///     of text that buries everything else in the log.
    /// </summary>
    private const uint NoFaction = 0;

    private readonly Dictionary<(uint, uint), bool> _factionFriendlyDict;
    private readonly Dictionary<(uint, uint), bool> _factionHostileDict;
    private readonly ILogger _logger;

    public FactionHostility()
    {
        _logger = Log.ForContext<FactionHostility>();
        _factionFriendlyDict = [];
        _factionHostileDict = [];
        LoadFromSDB();
    }

    /// <summary>
    ///     Builds the table from explicit data: the seam the tests use, because
    ///     <see cref="LoadFromSDB" /> reads the static database and a unit test has none.
    /// </summary>
    internal FactionHostility(IEnumerable<Faction> factions, IEnumerable<FactionRelations> relations)
    {
        _logger = Log.ForContext<FactionHostility>();
        _factionFriendlyDict = [];
        _factionHostileDict = [];
        Load(factions, relations);
    }

    public void LoadFromSDB() => Load(SDBInterface.GetFactions(), SDBInterface.GetFactionRelations());

    public bool IsFriendlyFaction(uint sourceFactionId, uint targetFactionId) =>
        Lookup(_factionFriendlyDict, sourceFactionId, targetFactionId);

    public bool IsHostileFaction(uint sourceFactionId, uint targetFactionId) =>
        Lookup(_factionHostileDict, sourceFactionId, targetFactionId);

    public HostilityStance GetFactionStance(uint sourceFactionId, uint targetFactionId)
    {
        bool isFriendly = IsFriendlyFaction(sourceFactionId, targetFactionId);
        if (isFriendly)
        {
            return HostilityStance.Friendly;
        }

        bool isHostile = IsHostileFaction(sourceFactionId, targetFactionId);
        if (isHostile)
        {
            return HostilityStance.Hostile;
        }

        return HostilityStance.Neutral;
    }

    /*
    public static void ComputePersonalFactionStance(uint factionId)
    {
        var factions = SDBInterface.GetFactions();
        var totalBytes = (((uint)factions.Count >> 6) + 1) << 3; // 8
        var byteIndex = 0;
        var bitIndex = 0;
        var friendly = new byte[totalBytes];
        var hostile = new byte[totalBytes];
        foreach (var faction in factions)
        {

        }
    }
    */

    internal void Load(IEnumerable<Faction> factions, IEnumerable<FactionRelations> relations)
    {
        _factionFriendlyDict.Clear();
        _factionHostileDict.Clear();

        // By id, not by position in a list. The relation rows name faction ids, and GetFactions()
        // hands back a Dictionary's values, whose order nothing guarantees to be 1..N: indexing
        // that list with id - 1 read whichever faction happened to land at that slot (and threw
        // outright for an id past the end, which would take the shard down while constructing it).
        Dictionary<uint, Faction> factionsById = [];
        if (factions != null)
        {
            foreach (var faction in factions)
            {
                factionsById[faction.Id] = faction;
            }
        }

        // Ids a relation row names that the faction table has no row for, so the count reported
        // below is of distinct ids however many rows or wildcard expansions mention them.
        HashSet<uint> unknownFactions = [];

        if (relations != null)
        {
            foreach (var relation in relations)
            {
                // A faction id of 0 on a relation row is the table's wildcard: the row describes
                // every faction against every other one. The shipped table's first row is exactly
                // that, so it seeds the whole matrix and the later, specific rows overwrite the
                // pairs they name.
                //
                // A row that wildcards *both* sides already expands to every ordered pair, so its
                // bidirectional write only re-derives pairs the expansion produces itself - and
                // derives them from the other faction's default stance. Honouring it made the
                // table depend on the order factions happen to come out of the SDB dictionary:
                // over the shipped rows, 201 different faction orders produced 201 different
                // tables, 375 of the 2500 pairs flipping between them. The expansion is symmetric,
                // so skip the mirror there and let every pair be written exactly once.
                var bidirectional = relation.HostilityBidirectional == 1
                    && !(relation.FactionA == NoFaction && relation.FactionB == NoFaction);

                foreach (var primaryFaction in Expand(relation.FactionA, factionsById, unknownFactions))
                {
                    foreach (var secondaryFaction in Expand(relation.FactionB, factionsById, unknownFactions))
                    {
                        ProcessFactionRelation(primaryFaction, secondaryFaction, relation, bidirectional);
                    }
                }
            }
        }

        if (unknownFactions.Count > 0)
        {
            _logger.Warning(
                "FactionHostility: {Count} faction ids named by dbcharacter::FactionRelations have no dbcharacter::Faction row; those relations were skipped",
                unknownFactions.Count);
        }

        _logger.Debug("FactionHostility initalized: {Pairs} faction pairs", _factionFriendlyDict.Count);
    }

    private static IEnumerable<Faction> Expand(uint factionId, Dictionary<uint, Faction> factionsById, HashSet<uint> unknownFactions)
    {
        if (factionId == NoFaction)
        {
            return factionsById.Values;
        }

        if (factionsById.TryGetValue(factionId, out var faction))
        {
            return [faction];
        }

        _ = unknownFactions.Add(factionId);
        return [];
    }

    private bool Lookup(Dictionary<(uint, uint), bool> table, uint sourceFactionId, uint targetFactionId)
    {
        if (table.TryGetValue((sourceFactionId, targetFactionId), out bool result))
        {
            return result;
        }

        // No entry. An entity with no faction is neither friendly nor hostile by faction, so it
        // resolves to Neutral without a word; a pair of real factions the table has no row for is
        // worth reporting, but once - the AI asks the same question several times a second per NPC.
        if (sourceFactionId != NoFaction && targetFactionId != NoFaction &&
            OnceLog.ShouldLog((nameof(FactionHostility), sourceFactionId, targetFactionId)))
        {
            _logger.Warning(
                "No faction relation for {SourceFactionId} - {TargetFactionId}; treating the pair as neutral",
                sourceFactionId,
                targetFactionId);
        }

        return false;
    }

    private void ProcessFactionRelation(Faction primaryFaction, Faction secondaryFaction, FactionRelations relation, bool bidirectional)
    {
        var key = (primaryFaction.Id, secondaryFaction.Id);
        bool friendly = false;
        bool hostile = false;

        if (relation.HostilityStance >= 1)
        {
            friendly = true;
        }
        else if (primaryFaction.DefaultStance <= -1)
        {
            hostile = true;
        }

        ProcessFactionRelationSet(key, friendly, hostile);

        if (bidirectional)
        {
            var bikey = (secondaryFaction.Id, primaryFaction.Id);
            ProcessFactionRelationSet(bikey, friendly, hostile);
        }
    }

    private void ProcessFactionRelationSet((uint, uint) key, bool friendly, bool hostile)
    {
        _factionFriendlyDict[key] = friendly;
        _factionHostileDict[key] = hostile;
    }
}
