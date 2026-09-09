using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The pure half of NPC level resolution: turning a <c>dbitems::LevelBand</c>
///     (the min/max range a zone or outpost is balanced around) into the single level the
///     NPC's <c>dbcharacter::MonsterScaling</c> stats are looked up at. The zone-lookup
///     overload (<see cref="SDBUtils.ResolveNpcLevel(uint)"/>) additionally reads
///     <c>dbzonemetadata::ZoneRecord</c> and needs a loaded <c>clientdb.sd2</c>, so it is
///     not unit tested here.
/// </summary>
public class NpcLevelResolutionTests
{
    public static TheoryData<LevelBand, byte> Bands => new()
    {
        // No band at all / no usable bounds: 0 tells the caller to apply its fallback level.
        { null, 0 },
        { new LevelBand { Min = 0, Max = 0 }, 0 },

        // A normal band resolves to its top: the zone's intended difficulty ceiling.
        { new LevelBand { Min = 1, Max = 1 }, 1 },
        { new LevelBand { Min = 11, Max = 14 }, 14 },
        { new LevelBand { Min = 39, Max = 40 }, 40 },
        { new LevelBand { Min = 44, Max = 45 }, 45 },
        { new LevelBand { Min = 1, Max = 30 }, 30 },
        { new LevelBand { Min = 45, Max = 45 }, 45 },
        { new LevelBand { Min = 41, Max = 50 }, 50 },

        // Max == 255 is the "no upper bound" sentinel; the band only pins a floor, so the
        // floor is what the monster scales at.
        { new LevelBand { Min = 1, Max = 255 }, 1 },
        { new LevelBand { Min = 31, Max = 255 }, 31 },
        { new LevelBand { Min = 40, Max = 255 }, 40 },
        { new LevelBand { Min = 71, Max = 255 }, 71 },

        // dbcharacter::MonsterScaling stops at 80 rows, so everything above is capped there.
        { new LevelBand { Min = 80, Max = 80 }, 80 },
        { new LevelBand { Min = 71, Max = 90 }, 80 },
        { new LevelBand { Min = 90, Max = 255 }, 80 },
    };

    [Theory]
    [MemberData(nameof(Bands))]
    public void ResolveNpcLevel_PicksTheLevelTheBandIsBalancedAt(LevelBand band, byte expected)
    {
        Assert.Equal(expected, SDBUtils.ResolveNpcLevel(band));
    }

    [Fact]
    public void MaxMonsterLevel_MatchesTheScalingTableSize()
    {
        // The cap is a contract with dbcharacter::MonsterScaling (80 rows in build prod-1962):
        // bumping it without extending that table would produce lookups that always miss.
        Assert.Equal(80, SDBUtils.MaxMonsterLevel);
    }

    [Fact]
    public void DefaultNpcLevel_IsTheDefaultPlayerLevel()
    {
        // Zones without a level band (test zone 12, the mission pockets whose difficulty the
        // live game set server-side) resolve their NPCs here. The contract is "fight on the
        // default player's terms": a fresh battleframe starts at progression level 1 and PIN
        // has no XP economy yet, so the anchor is 1 — not the retired hardcoded 45. Spawn
        // authors override per entry via character_spawn.json `level`.
        Assert.Equal(1, SDBUtils.DefaultNpcLevel);
        Assert.True(SDBUtils.DefaultNpcLevel <= SDBUtils.MaxMonsterLevel);
    }
}
