using System;
using System.Linq;
using System.Numerics;
using GameServer.Systems.Spawning.Population;
using GameServer.Tests.Fakes;
using Serilog;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The plan is where world population decides what belongs where, so these assert on the things
///     the feature stands or falls with: every monster row gets ground of its kind, the habitat and
///     level come from the zone's own data, the chunk rules are honoured, the caps hold, and the same
///     inputs always give the same plan.
/// </summary>
public class WorldPopulationPlannerTests
{
    private static (WorldPopulationPlanner Planner, FakeWorldPopulationDataSource Data, FakeWorldPopulationTerrain Terrain)
        CreatePlanner(StandardWorldPopulationRules rules = null)
    {
        var data = new FakeWorldPopulationDataSource();
        var terrain = new FakeWorldPopulationTerrain();
        var planner = new WorldPopulationPlanner(
            448u,
            rules ?? new StandardWorldPopulationRules(),
            data,
            terrain,
            Log.Logger);

        return (planner, data, terrain);
    }

    private static void Build(WorldPopulationPlanner planner)
    {
        int calls = 0;
        while (!planner.IsComplete)
        {
            _ = planner.Work(100_000);
            Assert.True(++calls < 100, "the planner never finished");
        }
    }

    /// <summary>
    ///     A plane of walkable ground 128 m across, which at the default 32 m cell gives a 4x4 grid
    ///     of 16 cells with four surfaces each.
    /// </summary>
    private static void AddGround(FakeWorldPopulationTerrain terrain) => terrain.AddPlane(Vector3.Zero, 8, 8, 16f);

    [Fact]
    public void Plan_TurnsWalkableGroundIntoCells()
    {
        var (planner, _, terrain) = CreatePlanner();
        AddGround(terrain);
        planner.Work(100_000);

        Build(planner);

        Assert.Equal(16, planner.CellCount);
        Assert.Equal(64, planner.ScannedSurfaces);
        Assert.False(planner.UsedAnchorFallback);

        var cell = planner.Cells.Values.First();
        Assert.Equal(4, cell.SurfaceCount);
    }

    [Fact]
    public void Plan_GivesEveryMonsterRowGroundOfItsKind()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain);
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(16f, 16f, 0f), 40f, WorldPopulationHabitat.Settlement, 1001u));
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(112f, 112f, 0f), 40f, WorldPopulationHabitat.Melding, 0u));
        data.LevelsByBand[1001u] = 12;
        data.AddMonster(10, WorldPopulationHabitat.Wilderness);
        data.AddMonster(11, WorldPopulationHabitat.Settlement);
        data.AddMonster(12, WorldPopulationHabitat.Melding);
        data.AddMonster(13, WorldPopulationHabitat.Settlement | WorldPopulationHabitat.Wilderness);

        Build(planner);

        Assert.Equal(4, planner.RosterCount);
        Assert.Equal(4, planner.PlacedRosterCount);
        Assert.Equal(0, planner.UnplacedRosterCount);

        // Every row is somewhere, and only in ground it fits.
        var placed = planner.Cells.Values.SelectMany(cell => cell.Slots).ToList();
        Assert.Contains(placed, slot => slot.Candidate.MonsterId == 11 && slot.Cell.Habitat == WorldPopulationHabitat.Settlement);
        Assert.Contains(placed, slot => slot.Candidate.MonsterId == 12 && slot.Cell.Habitat == WorldPopulationHabitat.Melding);
        Assert.All(placed, slot => Assert.True(slot.Candidate.Habitat.Accepts(slot.Cell.Habitat)));
    }

    [Fact]
    public void Plan_ReportsRowsTheZoneHasNoGroundFor()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain); // no anchors at all: the whole plane is open field
        data.AddMonster(10, WorldPopulationHabitat.Wilderness);
        data.AddMonster(11, WorldPopulationHabitat.Settlement);
        data.AddMonster(12, WorldPopulationHabitat.Melding);

        Build(planner);

        Assert.Equal(1, planner.PlacedRosterCount);
        Assert.Equal(2, planner.UnplacedRosterCount);
    }

    [Fact]
    public void Plan_TakesHabitatAndLevelFromTheAnchorsAroundACell()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain);
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(16f, 16f, 0f), 40f, WorldPopulationHabitat.Settlement, 1001u));
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(112f, 112f, 0f), 40f, WorldPopulationHabitat.Melding, 1002u));
        data.LevelsByBand[1001u] = 12;
        data.LevelsByBand[1002u] = 29;
        data.DefaultLevel = 7;
        data.AddMonster(10);

        Build(planner);

        Assert.Contains(planner.Cells.Values, cell => cell.Habitat == WorldPopulationHabitat.Settlement && cell.Level == 12);
        Assert.Contains(planner.Cells.Values, cell => cell.Habitat == WorldPopulationHabitat.Melding && cell.Level == 29);

        // Open field takes the band of whichever banded anchor is nearest to it, which is what gives
        // a zone its gradient: the field by the outpost is outpost level, the field by the Melding is
        // Melding level. The zone's own band is only for ground no anchor reaches (next test).
        Assert.Contains(planner.Cells.Values, cell => cell.Habitat == WorldPopulationHabitat.Wilderness && cell.Level == 12);
        Assert.Contains(planner.Cells.Values, cell => cell.Habitat == WorldPopulationHabitat.Wilderness && cell.Level == 29);
    }

    [Fact]
    public void Plan_FallsBackToTheZonesOwnLevelWhenNoAnchorCarriesABand()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain); // no anchors at all: no outpost, no Melding, nothing banded
        data.DefaultLevel = 7;
        data.AddMonster(10);

        Build(planner);

        Assert.All(planner.Cells.Values, cell => Assert.Equal(WorldPopulationHabitat.Wilderness, cell.Habitat));
        Assert.All(planner.Cells.Values, cell => Assert.Equal(7, cell.Level));
    }

    [Fact]
    public void Plan_TakesTheLevelOfTheNearestBandedAnchorHoweverFarAwayItIs()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain);

        // One outpost, far outside its own radius from most of the plane: the level gradient still
        // reaches the whole zone, which is what Coral Forest's per outpost bands do.
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(16f, 16f, 0f), 20f, WorldPopulationHabitat.Settlement, 1001u));
        data.LevelsByBand[1001u] = 5;
        data.DefaultLevel = 30;
        data.AddMonster(10);

        Build(planner);

        Assert.All(planner.Cells.Values, cell => Assert.Equal(5, cell.Level));
    }

    [Fact]
    public void Plan_PrefersASettlementOverTheMeldingAroundIt()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain);
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(16f, 16f, 0f), 400f, WorldPopulationHabitat.Melding, 0u));
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(16f, 16f, 0f), 40f, WorldPopulationHabitat.Settlement, 1001u));
        data.AddMonster(10);

        Build(planner);

        // An outpost inside a Melding perimeter is still a place players respawn in.
        Assert.Contains(planner.Cells.Values, cell => cell.Habitat == WorldPopulationHabitat.Settlement);
        Assert.Contains(planner.Cells.Values, cell => cell.Habitat == WorldPopulationHabitat.Melding);
    }

    [Fact]
    public void Plan_RefusesCellsInChunksTheZoneDoesNotSimulate()
    {
        var (planner, data, terrain) = CreatePlanner();
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f); // four cells, two of them past x=32
        terrain.ChunkOf = position => position.X < 32f ? 100u : 200u;
        data.UnspawnableChunks.Add(200u);
        data.AddMonster(10);

        Build(planner);

        Assert.Equal(2, planner.CellCount);
        Assert.Equal(2, planner.RefusedChunkCells);
        Assert.All(planner.Cells.Values, cell => Assert.Equal(100u, cell.ChunkRecordId));
    }

    [Fact]
    public void Plan_FillsACellNoFurtherThanItsCountCap()
    {
        var rules = new StandardWorldPopulationRules { MaxNpcsPerCell = 2 };
        var (planner, data, terrain) = CreatePlanner(rules);
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f); // four cells
        data.AddMonster(10);

        Build(planner);

        Assert.Equal(4, planner.CellCount);
        Assert.Equal(8, planner.SlotCount);
        Assert.All(planner.Cells.Values, cell => Assert.Equal(2, cell.Slots.Count));
    }

    [Fact]
    public void Plan_StopsFillingACellWhenItsDifficultyBudgetIsSpent()
    {
        var rules = new StandardWorldPopulationRules { MaxNpcsPerCell = 4, MaxDifficultyPerCell = 100 };
        var (planner, data, terrain) = CreatePlanner(rules);
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f); // four cells
        data.AddMonster(10, difficultyCost: 90);

        Build(planner);

        // One 90-cost monster per cell and no room for a second one, however much count is left.
        Assert.Equal(4, planner.SlotCount);
        Assert.All(planner.Cells.Values, cell => Assert.Single(cell.Slots));
        Assert.All(planner.Cells.Values, cell => Assert.Equal(90, cell.SpentDifficulty));
    }

    [Fact]
    public void Plan_GivesAnExpensiveRowItsSlotEvenWhenItBreaksTheBudget()
    {
        var rules = new StandardWorldPopulationRules { MaxNpcsPerCell = 4, MaxDifficultyPerCell = 100 };
        var (planner, data, terrain) = CreatePlanner(rules);
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f);
        data.AddMonster(10, difficultyCost: 50);
        data.AddMonster(99, difficultyCost: 1000); // a raid boss priced above a whole cell

        Build(planner);

        // Coverage is exempt from the budget, so no row is priced out of the zone it belongs to.
        Assert.Contains(planner.Cells.Values.SelectMany(cell => cell.Slots), slot => slot.Candidate.MonsterId == 99);
        Assert.Equal(2, planner.PlacedRosterCount);
        Assert.Equal(0, planner.UnplacedRosterCount);
    }

    [Fact]
    public void Plan_ChargesARowWithNoCostItsNominalOne()
    {
        var rules = new StandardWorldPopulationRules
        {
            MaxNpcsPerCell = 4,
            MaxDifficultyPerCell = 50,
            UnbudgetedDifficultyCost = 25,
        };
        var (planner, data, terrain) = CreatePlanner(rules);
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f);
        data.AddMonster(10, difficultyCost: 0);

        Build(planner);

        // 50 of budget at 25 each: two ambient NPCs per cell, not four.
        Assert.All(planner.Cells.Values, cell => Assert.Equal(2, cell.Slots.Count));
    }

    [Fact]
    public void Plan_StopsAtItsSlotCeiling()
    {
        var rules = new StandardWorldPopulationRules { MaxNpcsPerCell = 4, MaxPlannedSlots = 5 };
        var (planner, data, terrain) = CreatePlanner(rules);
        AddGround(terrain);
        for (uint id = 10; id < 20; id++)
        {
            data.AddMonster(id);
        }

        Build(planner);

        Assert.Equal(5, planner.SlotCount);

        // The ceiling is a real bound on the plan, so the rows that no longer fit are reported
        // rather than quietly dropped.
        Assert.Equal(5, planner.PlacedRosterCount);
        Assert.Equal(5, planner.UnplacedRosterCount);
    }

    [Fact]
    public void Plan_KeepsASlotInsideItsOwnCell()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain);
        for (uint id = 10; id < 30; id++)
        {
            data.AddMonster(id);
        }

        Build(planner);

        foreach (var cell in planner.Cells.Values)
        {
            foreach (var slot in cell.Slots)
            {
                var offset = slot.Anchor - cell.Center;
                Assert.True(MathF.Abs(offset.X) <= 12.81f, $"slot drifted {offset.X} m out of its cell");
                Assert.True(MathF.Abs(offset.Y) <= 12.81f, $"slot drifted {offset.Y} m out of its cell");
                Assert.Equal(0f, offset.Z);

                // A facing is a horizontal unit vector: the spawn code turns it into a yaw.
                Assert.True(MathF.Abs(slot.Facing.Length() - 1f) < 0.001f);
                Assert.Equal(0f, slot.Facing.Z);
            }
        }
    }

    [Fact]
    public void Plan_FallsBackToAuthoredAnchorsWithoutWalkableGround()
    {
        var (planner, data, terrain) = CreatePlanner();
        Assert.False(terrain.HasSurfaces);

        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(100f, 100f, 50f), 200f, WorldPopulationHabitat.Settlement, 1001u));
        data.LevelsByBand[1001u] = 12;
        data.AddMonster(10, WorldPopulationHabitat.Settlement);

        Build(planner);

        Assert.True(planner.UsedAnchorFallback);
        Assert.Equal(1, planner.CellCount);

        var cell = planner.Cells.Values.First();
        Assert.Equal(new Vector3(100f, 100f, 50f), cell.Center);
        Assert.Equal(WorldPopulationHabitat.Settlement, cell.Habitat);
        Assert.Equal(12, cell.Level);
        Assert.Equal(1, planner.PlacedRosterCount);
    }

    [Fact]
    public void Plan_SpreadsItsWorkOverSeveralUpdates()
    {
        var rules = new StandardWorldPopulationRules { PlanWorkPerTick = 4 };
        var (planner, data, terrain) = CreatePlanner(rules);
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f); // 16 surfaces
        data.AddMonster(10);

        int calls = 0;
        while (!planner.IsComplete)
        {
            _ = planner.Work(rules.PlanWorkPerTick);
            Assert.True(++calls < 20, "the planner never finished");
        }

        // Four updates of four surfaces each, then one for the cells and one for the slots.
        Assert.Equal(6, calls);
        Assert.Equal(16, planner.ScannedSurfaces);
    }

    [Fact]
    public void Plan_SpreadsCellBuildingOverSeveralUpdates()
    {
        var rules = new StandardWorldPopulationRules { PlanWorkPerTick = 2 };
        var (planner, data, terrain) = CreatePlanner(rules);
        terrain.AddPlane(Vector3.Zero, 4, 4, 16f); // 16 surfaces, 4 cells
        data.AddMonster(10);

        int calls = 0;
        while (!planner.IsComplete)
        {
            _ = planner.Work(rules.PlanWorkPerTick);
            Assert.True(++calls < 40, "the planner never finished");
        }

        // Eight updates of two surfaces each, two of two cells each, one for the slots: a cell is
        // classified against every anchor near it, so on a real zone this phase is the expensive one
        // and it has to be spread the same way the surface scan is.
        Assert.Equal(11, calls);
        Assert.Equal(4, planner.CellCount);
        Assert.Equal(16, planner.ScannedSurfaces);
    }

    [Fact]
    public void Plan_IsTheSamePlanEveryTime()
    {
        var first = BuildFixedPlan();
        var second = BuildFixedPlan();

        Assert.Equal(first.CellCount, second.CellCount);
        Assert.Equal(first.SlotCount, second.SlotCount);
        Assert.Equal(first.PlacedRosterCount, second.PlacedRosterCount);

        foreach (var (key, cell) in first.Cells)
        {
            Assert.True(second.Cells.TryGetValue(key, out var other), $"cell {key} is missing from the second plan");
            Assert.Equal(cell.Center, other.Center);
            Assert.Equal(cell.Habitat, other.Habitat);
            Assert.Equal(cell.Level, other.Level);
            Assert.Equal(cell.BaseFacing, other.BaseFacing);
            Assert.Equal(cell.Slots.Count, other.Slots.Count);

            for (int i = 0; i < cell.Slots.Count; i++)
            {
                Assert.Equal(cell.Slots[i].Candidate.MonsterId, other.Slots[i].Candidate.MonsterId);
                Assert.Equal(cell.Slots[i].Anchor, other.Slots[i].Anchor);
                Assert.Equal(cell.Slots[i].Facing, other.Slots[i].Facing);
            }
        }
    }

    private static WorldPopulationPlanner BuildFixedPlan()
    {
        var (planner, data, terrain) = CreatePlanner();
        AddGround(terrain);
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(16f, 16f, 0f), 40f, WorldPopulationHabitat.Settlement, 1001u));
        data.Anchors.Add(new WorldPopulationAnchor(new Vector3(112f, 112f, 0f), 40f, WorldPopulationHabitat.Melding, 0u));
        data.LevelsByBand[1001u] = 12;
        data.AddMonster(10, WorldPopulationHabitat.Wilderness, difficultyCost: 20);
        data.AddMonster(11, WorldPopulationHabitat.Settlement);
        data.AddMonster(12, WorldPopulationHabitat.Melding | WorldPopulationHabitat.Wilderness, difficultyCost: 300);
        data.AddMonster(13, WorldPopulationHabitat.Wilderness, difficultyCost: 50);

        Build(planner);
        return planner;
    }
}
