using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using GameServer.Systems.Spawning.Population;
using GameServer.Tests.Fakes;
using Serilog;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The classifier decides which of the 3,109 <c>dbcharacter::Monster</c> rows are world
///     population and where each belongs, so its rules are asserted on the behaviours and factions
///     the shipped database actually carries.
/// </summary>
public class MonsterHabitatClassifierTests
{
    private static bool Classify(
        string behavior,
        out WorldPopulationHabitat habitat,
        uint vendorId = 0,
        string faction = null,
        bool hasRepresentation = true) =>
        MonsterHabitatClassifier.TryClassify(behavior, vendorId, faction, hasRepresentation, out habitat, out _);

    private static string Exclusion(string behavior, uint vendorId = 0, string faction = null, bool hasRepresentation = true)
    {
        MonsterHabitatClassifier.TryClassify(
            behavior,
            vendorId,
            faction,
            hasRepresentation,
            out _,
            out string exclusion);
        return exclusion;
    }

    [Fact]
    public void RefusesARowThatHasNothingToRenderOrCollideWith()
    {
        Assert.False(Classify("AggressiveWanderer", out _, hasRepresentation: false));
        Assert.Equal("no chassis and no posetype", Exclusion("AggressiveWanderer", hasRepresentation: false));
    }

    [Theory]
    [InlineData("Null")]
    [InlineData("PlayerPet")]
    [InlineData("PassivePet")]
    [InlineData("Pet_Earthbreaker")]
    [InlineData("TestElfPet")]
    [InlineData("EngineerTurret")]
    [InlineData("EngineerTurretTeleporter")]
    [InlineData("TurretTeleporterTarget")]
    [InlineData("Elevator")]
    [InlineData("DoorUpInteract")]
    [InlineData("AvoidMatt")]
    [InlineData("CraterTest")]
    public void RefusesBehavioursThatAreNotWorldInhabitants(string behavior)
    {
        Assert.False(Classify(behavior, out _));
    }

    [Fact]
    public void RefusesABehaviourWithItsArgumentsAttached()
    {
        // The classifier reads the behaviour through the same parser the AI uses, so a row that
        // carries parameters is refused on its name, not on the whole string.
        Assert.False(Classify("Null(someFlag=1)", out _));
        Assert.False(Classify("PlayerPet(followDistance=3)", out _));
    }

    [Theory]
    [InlineData("PeacetimeCityWanderer")]
    [InlineData("GuardCityWanderer")]
    [InlineData("BasicCivilian_Stationary")]
    [InlineData("StationaryCivilianDialog")]
    [InlineData("AlertAndInteractive(interactionType=\"HolsterTalk\")")]
    [InlineData("UseAbilityOnInteract_Dialog")]
    [InlineData("PerformEmote")]
    [InlineData("TraumaDoc")]
    public void PutsSettlementBehavioursInSettlements(string behavior)
    {
        Assert.True(Classify(behavior, out var habitat, faction: "accord"));
        Assert.Equal(WorldPopulationHabitat.Settlement, habitat);
    }

    [Fact]
    public void VendorRolesRequireExplicitPlacementsInsteadOfRandomCoverageCopies()
    {
        Assert.False(Classify("AggressiveWanderer", out var habitat, vendorId: 4242));
        Assert.Equal(WorldPopulationHabitat.None, habitat);
        Assert.Equal("requires vendor placement assignment", Exclusion("AggressiveWanderer", vendorId: 4242));
    }

    [Fact]
    public void PutsTheMeldingsFactionAtTheMelding()
    {
        Assert.True(Classify("AggressiveWanderer", out var habitat, faction: "melding"));
        Assert.Equal(WorldPopulationHabitat.Melding, habitat);
    }

    [Fact]
    public void PutsANamedMeldingBehaviourAtTheMeldingWhateverItsFaction()
    {
        // MeldingAcolyte is filed under gaea and MeldingPuker under chosen in the shipped database.
        Assert.True(Classify("MeldingAcolyte", out var habitat, faction: "gaea"));
        Assert.Equal(WorldPopulationHabitat.Melding, habitat);
    }

    [Fact]
    public void PutsTheChosenAtTheMeldingAndInTheField()
    {
        Assert.True(Classify("ChosenTrooper", out var habitat, faction: "chosen"));
        Assert.Equal(WorldPopulationHabitat.Melding | WorldPopulationHabitat.Wilderness, habitat);
    }

    [Fact]
    public void UnionsTheHabitatsARowQualifiesForSeveralTimesOver()
    {
        Assert.True(Classify("AlertAndInteractive", out var habitat, faction: "chosen"));
        Assert.Equal(
            WorldPopulationHabitat.Settlement | WorldPopulationHabitat.Melding | WorldPopulationHabitat.Wilderness,
            habitat);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("AggressiveWanderer")]
    [InlineData("Arch_MedRangedHumanoid_Base(triggerPullTime=1500,fireRestDuration=1000)")]
    [InlineData("EliteWanderer")]
    [InlineData("SwarmWanderer")]
    [InlineData("RaiderBaronMiniBoss")]
    [InlineData("Brontodon")]
    public void PutsEverythingElseInTheField(string behavior)
    {
        Assert.True(Classify(behavior, out var habitat, faction: "gaea"));
        Assert.Equal(WorldPopulationHabitat.Wilderness, habitat);
    }

    [Theory]
    [InlineData("StockShootAndFollowRoute")]
    [InlineData("OneOff_FollowRoute")]
    [InlineData("NavigateToLocation")]
    [InlineData("Arch_Follower(offset=[0,0,-.1])")]
    [InlineData("ProtectVehicle(vehicleType=35)")]
    [InlineData("PeacetimeCityWanderer(city_prefix=\"WanderPoint\")")]
    [InlineData("Stand(stationary=true,city_prefix=Town)")]
    public void RouteAssignmentsAreRequiredEvenForStationaryOrSettlementTemplates(string behavior)
    {
        Assert.False(Classify(behavior, out var habitat, faction: "chosen"));
        Assert.Equal(WorldPopulationHabitat.None, habitat);
        Assert.Equal("requires route, named points or follow target", Exclusion(behavior));
    }

    [Theory]
    [InlineData("Mosquito(grounded=false)")]
    [InlineData("Wander(grounded=0)")]
    [InlineData("Stand(climber=true)")]
    [InlineData("Wander(inSpawnVolume=1)")]
    [InlineData("UseWorkDeployables(function=Rummage,groundOffset=1.6,inSpawnVolume=true,climber=true)")]
    [InlineData("OneOff_StandStill(groundOffset=0.2)")]
    public void GroundPopulationDoesNotFabricateOtherLocomotionOrSpawnVolumes(string behavior)
    {
        Assert.False(Classify(behavior, out _));
        Assert.Equal("requires unsupported locomotion or spawn volume", Exclusion(behavior));
    }

    [Theory]
    [InlineData("PerformEmoteNoPhysics(emote=guard)")]
    [InlineData("AlertAndLookAtPlayer(emote=sittingchair05)")]
    [InlineData("AlertAndInteractive(emote=CONTROLSEAT)")]
    [InlineData("UseAbilityOnInteract_Dialog(emote=townlean2)")]
    [InlineData("AlertAndLookAtPlayer(emote=typing01)")]
    public void ReviewedPropDependentPosesRequireAPlacement(string behavior)
    {
        Assert.False(Classify(behavior, out _));
        Assert.Equal("requires assigned prop or pose placement", Exclusion(behavior));
    }

    [Theory]
    [InlineData("Wander(grounded=true,climber=false,inSpawnVolume=0,groundOffset=0)")]
    [InlineData("Wander(groundOffset=NaN)")]
    [InlineData("Wander(groundOffset=Infinity)")]
    [InlineData("Wander(groundOffset=-1)")]
    [InlineData("Wander(child=Other(climber=true,grounded=false))")]
    [InlineData("PeacetimeCityWanderer(city_prefix=\"\",restFunction=Work)")]
    [InlineData("GuardCityWanderer(restFunction=Guard)")]
    [InlineData("BasicCivilian_Stationary(emote=guard)")]
    [InlineData("AlertAndInteractive(emote=townstand4)")]
    [InlineData("AlertAndInteractive(emote=not_a_reviewed_chair_pose)")]
    [InlineData("SomeFutureFollowRoute")]
    public void NoSubstringOrNestedParameterExclusionsAndOrdinaryRoutinesRemainAvailable(string behavior)
    {
        Assert.True(Classify(behavior, out _));
        Assert.Null(Exclusion(behavior));
    }

    [Fact]
    public void AllShippedPlacementRowsMatchTheRuntimeAndFilteredRowsNeverEnterCoverageOrDensity()
    {
        using var stream = typeof(MonsterHabitatClassifierTests).Assembly.GetManifestResourceStream("NpcPlacementReference.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);
        Assert.Equal("prod-1962", document.RootElement.GetProperty("patch").GetString());
        var rows = document.RootElement.GetProperty("monsters");
        Assert.Equal(3109, rows.GetArrayLength());
        var ids = new HashSet<uint>();
        var refused = new HashSet<uint>();
        var data = new FakeWorldPopulationDataSource();
        foreach (var row in rows.EnumerateArray())
        {
            uint id = row.GetProperty("monster_id").GetUInt32();
            Assert.True(ids.Add(id));
            bool admitted = MonsterHabitatClassifier.TryClassify(
                row.GetProperty("behavior").GetString(), row.GetProperty("vendor_id").GetUInt32(),
                row.GetProperty("faction").GetString(), row.GetProperty("has_representation").GetBoolean(),
                out var habitat, out string exclusion);
            string expected = row.GetProperty("habitat").GetString();
            Assert.Equal(expected != "Excluded", admitted);
            Assert.Equal(row.GetProperty("exclusion").GetString(), exclusion ?? string.Empty);
            Assert.Equal(expected == "Excluded" ? WorldPopulationHabitat.None
                : Enum.Parse<WorldPopulationHabitat>(expected.Replace("|", ",")), habitat);
            if (admitted)
            {
                data.AddMonster(id, habitat);
            }
            else
            {
                refused.Add(id);
            }
        }

        // Same admission gate as SdbWorldPopulationDataSource, real planner, controlled terrain.
        // Enough cells for all three habitats and density after coverage; not a real zone claim.
        var terrain = new FakeWorldPopulationTerrain();
        terrain.AddPlane(Vector3.Zero, 64, 64, 32f);
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(512, 512, 0), 600,
            WorldPopulationHabitat.Settlement, 0));
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(1536, 1536, 0), 600,
            WorldPopulationHabitat.Melding, 0));
        var planner = new WorldPopulationPlanner(448, new StandardWorldPopulationRules
            { OutpostSettlementRadius = 0 }, data, terrain, Log.Logger);
        for (int calls = 0; !planner.IsComplete; calls++)
        {
            Assert.True(calls < 100);
            planner.Work(100_000);
        }

        Assert.True(planner.SlotCount > data.Candidates.Count);
        var placed = new HashSet<uint>();
        foreach (var cell in planner.Cells.Values)
        {
            foreach (var slot in cell.Slots)
            {
                Assert.DoesNotContain(slot.Candidate.MonsterId, refused);
                Assert.True(slot.Candidate.Habitat.Accepts(cell.Habitat));
                placed.Add(slot.Candidate.MonsterId);
            }
        }

        Assert.Contains(785u, placed); // normal guard still gets population
        Assert.Contains(2587u, placed); // normal civilian still gets population
        Assert.Contains(769u, placed); // non-vendor work visitor still gets population
        Assert.Contains(110u, refused); // vendor role
        Assert.Contains(459u, refused); // named points
        Assert.Contains(1249u, refused); // climber / spawn volume
        Assert.Contains(829u, refused); // seated no-physics actor
        Assert.Contains(3222u, refused); // Chosen follow-route actor
    }

    [Fact]
    public void AnEmptyBehaviourIsStillWorldPopulation()
    {
        // 1,068 rows carry no behaviour string, among them real monsters the game shipped (the
        // Melded Wyrm, the Chosen Sniper). An unnamed AI set is not a reason to leave them out.
        Assert.True(Classify(string.Empty, out var habitat, hasRepresentation: true));
        Assert.Equal(WorldPopulationHabitat.Wilderness, habitat);
    }
}
