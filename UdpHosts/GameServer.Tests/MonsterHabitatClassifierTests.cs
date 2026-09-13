using GameServer.Systems.Spawning.Population;
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
    public void PutsAVendorInASettlementWhateverItsBehaviour()
    {
        Assert.True(Classify("AggressiveWanderer", out var habitat, vendorId: 4242));
        Assert.Equal(WorldPopulationHabitat.Settlement, habitat);
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

    [Fact]
    public void AnEmptyBehaviourIsStillWorldPopulation()
    {
        // 1,068 rows carry no behaviour string, among them real monsters the game shipped (the
        // Melded Wyrm, the Chosen Sniper). An unnamed AI set is not a reason to leave them out.
        Assert.True(Classify(string.Empty, out var habitat, hasRepresentation: true));
        Assert.Equal(WorldPopulationHabitat.Wilderness, habitat);
    }
}
