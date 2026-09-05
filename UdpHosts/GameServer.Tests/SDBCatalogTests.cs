using GameServer.StaticDB;
using GameServer.Systems.Spawning;
using Xunit;

namespace GameServer.Tests;

/// <summary>
/// Tests for the parts of the static-database catalog / spawn commands that do
/// not need a loaded `clientdb.sd2`: kind parsing, table mapping and the
/// argument validation of `spawn` / `sdb` / `sdbinfo`.
/// </summary>
public class SDBCatalogTests
{
    [Theory]
    [InlineData("monster", SDBCatalogKind.Monster)]
    [InlineData("Monsters", SDBCatalogKind.Monster)]
    [InlineData("npc", SDBCatalogKind.Monster)]
    [InlineData("MOB", SDBCatalogKind.Monster)]
    [InlineData("character", SDBCatalogKind.Monster)]
    [InlineData("deployable", SDBCatalogKind.Deployable)]
    [InlineData("dep", SDBCatalogKind.Deployable)]
    [InlineData("vehicle", SDBCatalogKind.Vehicle)]
    [InlineData("veh", SDBCatalogKind.Vehicle)]
    [InlineData("carryable", SDBCatalogKind.Carryable)]
    [InlineData("carry", SDBCatalogKind.Carryable)]
    [InlineData("turret", SDBCatalogKind.Turret)]
    [InlineData("turrets", SDBCatalogKind.Turret)]
    public void TryParseKind_AcceptsAliasesCaseInsensitively(string input, SDBCatalogKind expected)
    {
        Assert.True(SDBCatalog.TryParseKind(input, out var kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("weapon")]
    [InlineData("ability")]
    [InlineData(null)]
    public void TryParseKind_RejectsUnknownKinds(string input)
    {
        Assert.False(SDBCatalog.TryParseKind(input, out _));
    }

    [Theory]
    [InlineData(SDBCatalogKind.Monster, "dbcharacter::Monster")]
    [InlineData(SDBCatalogKind.Deployable, "dbcharacter::Deployable")]
    [InlineData(SDBCatalogKind.Vehicle, "vcs::VehicleInfo")]
    [InlineData(SDBCatalogKind.Carryable, "dbitems::CarryableObject")]
    [InlineData(SDBCatalogKind.Turret, "dbcharacter::Turret")]
    public void GetTableName_MapsEveryKindToItsSdbTable(SDBCatalogKind kind, string expected)
    {
        Assert.Equal(expected, SDBCatalog.GetTableName(kind));
    }

    [Fact]
    public void CatalogEntry_FormatsIdNameFactionAndSummary()
    {
        var entry = new SDBCatalogEntry
        {
            Kind = SDBCatalogKind.Monster,
            Id = 2435,
            Name = "Aranha Queen",
            Faction = "gaea",
            Summary = "behavior=none",
        };

        var text = entry.ToString();
        Assert.Contains("2435", text);
        Assert.Contains("Aranha Queen", text);
        Assert.Contains("[gaea]", text);
        Assert.Contains("behavior=none", text);
    }

    [Fact]
    public void CatalogEntry_FallsBackToUnnamedPlaceholder()
    {
        var entry = new SDBCatalogEntry { Kind = SDBCatalogKind.Carryable, Id = 3 };
        Assert.Equal("<unnamed>", entry.DisplayName);
        Assert.DoesNotContain("[", entry.ToString());
    }

    [Fact]
    public void Spawn_WithoutEnoughArguments_ReturnsUsage()
    {
        Assert.StartsWith("Usage: spawn", SDBSpawner.Spawn(null, null, null));
        Assert.StartsWith("Usage: spawn", SDBSpawner.Spawn([], null, null));
        Assert.StartsWith("Usage: spawn", SDBSpawner.Spawn(["monster"], null, null));
    }

    [Fact]
    public void Spawn_WithUnknownKind_ReportsAcceptedKinds()
    {
        var message = SDBSpawner.Spawn(["weapon", "123"], null, null);
        Assert.Contains("Unknown kind", message);
        Assert.Contains(SDBCatalog.KindList, message);
    }

    [Fact]
    public void Browse_WithUnknownKind_ReportsAcceptedKinds()
    {
        Assert.Contains("Unknown kind", SDBSpawner.Browse(["nope"]));
    }

    [Fact]
    public void Info_WithoutEnoughArguments_ReturnsUsage()
    {
        Assert.StartsWith("Usage: sdbinfo", SDBSpawner.Info(null));
        Assert.StartsWith("Usage: sdbinfo", SDBSpawner.Info([]));
        Assert.StartsWith("Usage: sdbinfo", SDBSpawner.Info(["monster"]));
    }

    [Fact]
    public void Info_WithUnknownKind_ReportsAcceptedKinds()
    {
        Assert.Contains("Unknown kind", SDBSpawner.Info(["nope", "1"]));
    }

    [Fact]
    public void KindList_MentionsEveryKind()
    {
        foreach (var kind in System.Enum.GetValues<SDBCatalogKind>())
        {
            Assert.Contains(kind.ToString().ToLowerInvariant(), SDBCatalog.KindList);
        }
    }
}
