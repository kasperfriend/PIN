using System.Collections.Generic;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The faction relation table, as <c>dbcharacter::FactionRelations</c> writes it: a wildcard row
///     (<c>faction_a = 0, faction_b = 0</c>) that describes every faction against every other one,
///     followed by the specific rows that overwrite the pairs they name. Built through the
///     <see cref="FactionHostility" /> data seam rather than <c>LoadFromSDB</c>, which needs a
///     loaded <c>clientdb.sd2</c>.
/// </summary>
public class FactionHostilityTests
{
    /// <summary>The shipped table's ids: accord, chosen, monster, and a settlement faction.</summary>
    private const uint Accord = 1;
    private const uint Chosen = 2;
    private const uint Monster = 5;
    private const uint Copa = 10;

    private static Faction MakeFaction(uint id, sbyte defaultStance) =>
        new() { Id = id, InternalName = $"faction{id}", DefaultStance = defaultStance };

    /// <summary>Accord and Copa are neutral by default; Chosen and Monster are hostile by default.</summary>
    private static List<Faction> ShippedFactions() =>
        [MakeFaction(Accord, 0), MakeFaction(Chosen, -1), MakeFaction(Monster, -1), MakeFaction(Copa, 0)];

    private static List<FactionRelations> ShippedRelations() =>
        [
            // The wildcard row the shipped table opens with: neutral, both directions, for everyone.
            new() { FactionA = 0, FactionB = 0, HostilityStance = 0, HostilityBidirectional = 1 },

            // A specific row naming two factions.
            new() { FactionA = Accord, FactionB = Chosen, HostilityStance = -2, HostilityBidirectional = 1 },

            // And an alliance.
            new() { FactionA = Copa, FactionB = Accord, HostilityStance = 1, HostilityBidirectional = 1 },
        ];

    private static FactionHostility Create() => new(ShippedFactions(), ShippedRelations());

    [Fact]
    public void TheWildcardRow_SeedsEveryPairFromEachFactionsOwnDefaultStance()
    {
        var hostility = Create();

        // No specific row names monster vs accord, so the pair is the wildcard's: hostile, because
        // the monster faction's own default stance says so.
        Assert.Equal(HostilityStance.Hostile, hostility.GetFactionStance(Monster, Accord));

        // The same pair the other way round is the wildcard expanded with accord as the primary
        // faction, whose default stance is neutral: the table is directional.
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(Accord, Monster));
    }

    [Fact]
    public void ASpecificRow_OverwritesWhatTheWildcardSeeded()
    {
        var hostility = Create();

        Assert.Equal(HostilityStance.Friendly, hostility.GetFactionStance(Copa, Accord));

        // Bidirectional, so the reverse pair carries the same stance.
        Assert.Equal(HostilityStance.Friendly, hostility.GetFactionStance(Accord, Copa));
    }

    [Fact]
    public void RelationsAreResolvedByFactionId_NotByPositionInTheFactionTable()
    {
        // GetFactions() hands back a Dictionary's values, so nothing guarantees the faction with id
        // N sits at index N - 1. Reading the list by position instead of by id silently applied one
        // faction's relations to another (and threw for an id past the end).
        var ascending = new FactionHostility(ShippedFactions(), ShippedRelations());

        var unordered = new List<Faction> { MakeFaction(Copa, 0), MakeFaction(Monster, -1), MakeFaction(Accord, 0), MakeFaction(Chosen, -1) };
        var shuffled = new FactionHostility(unordered, ShippedRelations());

        uint[] ids = [0, Accord, Chosen, Monster, Copa];
        foreach (uint source in ids)
        {
            foreach (uint target in ids)
            {
                Assert.Equal(ascending.GetFactionStance(source, target), shuffled.GetFactionStance(source, target));
            }
        }

        Assert.Equal(HostilityStance.Friendly, shuffled.GetFactionStance(Copa, Accord));
        Assert.Equal(HostilityStance.Hostile, shuffled.GetFactionStance(Monster, Accord));
    }

    [Fact]
    public void ARelationNamingAFactionTheTableDoesNotHave_IsSkippedInsteadOfThrown()
    {
        var relations = ShippedRelations();
        relations.Add(new FactionRelations { FactionA = 99, FactionB = Accord, HostilityStance = -2, HostilityBidirectional = 0 });

        var hostility = new FactionHostility(ShippedFactions(), relations);

        // Nothing for the id the faction table has no row for, and the rest of the table intact.
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(99, Accord));
        Assert.Equal(HostilityStance.Friendly, hostility.GetFactionStance(Copa, Accord));
    }

    [Fact]
    public void AnEntityWithNoFaction_IsNeutralAndNotAMissingRelation()
    {
        var hostility = Create();

        // 17 dbcharacter::Monster rows carry faction_id 0 - the peacetime city wanderers among them.
        // They can never have an entry in the relation table, so asking about one is not a gap in
        // the data and must not be reported as one (the AI asks several times a second per NPC).
        Assert.False(hostility.IsFriendlyFaction(0, Accord));
        Assert.False(hostility.IsHostileFaction(0, Accord));
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(0, Accord));
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(Accord, 0));
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(0, 0));
    }

    [Fact]
    public void AnExplicitlyHostileRow_ReadsThroughTheFactionsOwnDefaultStance()
    {
        var hostility = Create();

        // Documents today's reading of hostility_stance rather than asserting it is the right one:
        // only a stance of 1 or more counts as friendly, and hostility comes from the primary
        // faction's default_stance, so a row that says -2 between two factions whose default stance
        // is neutral leaves the pair Neutral. Accord (default stance 0) vs Chosen is such a pair -
        // and Neutral is damageable, which is what CombatSim and the AI both need it to be.
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(Accord, Chosen));
        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(Chosen, Accord));

        // Where the primary faction's own default stance is hostile, the same row reads as hostile.
        var withHostileFaction = new FactionHostility(
            [MakeFaction(Accord, -1), MakeFaction(Chosen, 0)],
            [new FactionRelations { FactionA = Accord, FactionB = Chosen, HostilityStance = -2, HostilityBidirectional = 1 }]);

        Assert.Equal(HostilityStance.Hostile, withHostileFaction.GetFactionStance(Accord, Chosen));
    }

    [Fact]
    public void ReloadingRebuildsTheTable_InsteadOfLayeringOverThePreviousOne()
    {
        var hostility = new FactionHostility(ShippedFactions(), ShippedRelations());
        Assert.Equal(HostilityStance.Friendly, hostility.GetFactionStance(Copa, Accord));

        // A reload without the alliance row must not keep the old answer for that pair.
        hostility.Load(
            ShippedFactions(),
            [new FactionRelations { FactionA = 0, FactionB = 0, HostilityStance = 0, HostilityBidirectional = 1 }]);

        Assert.Equal(HostilityStance.Neutral, hostility.GetFactionStance(Copa, Accord));
    }
}
