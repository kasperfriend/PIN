using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Systems.Spawning.Population;
using GameServer.Tests.Fakes;
using Serilog;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The service is the half of world population that has to be safe on a live server: it may only
///     spawn where players are, only as fast as its budget allows, only up to its cap, and it has to
///     take everything away again when the players leave or the feature is turned off.
/// </summary>
public class WorldPopulationServiceTests
{
    private const ulong StartTime = 60_000;

    /// <summary>The rules' update interval, so every <see cref="Tick"/> does one update.</summary>
    private const int Interval = 250;

    private sealed class World
    {
        public FakeShard Shard;
        public FakeWorldPopulationDataSource Data;
        public FakeWorldPopulationTerrain Terrain;
        public FakeWorldPopulationSpawner Spawner;
        public WorldPopulationService Service;
        public StandardWorldPopulationRules Rules;
        public CharacterEntity Player;
        public FakeNetworkPlayer Client;
        public ulong Time = StartTime;
    }

    /// <summary>
    ///     A plane of walkable ground 128 m across (16 cells at the default cell size) and five
    ///     monster rows, which plans 64 slots: enough that the budgets, not the plan, are what limits
    ///     a test.
    /// </summary>
    private static void AddGroundAndRoster(World world)
    {
        world.Terrain.AddPlane(Vector3.Zero, 8, 8, 16f);
        for (uint id = 10; id < 15; id++)
        {
            world.Data.AddMonster(id);
        }
    }

    private static World CreateWorld(
        StandardWorldPopulationRules rules = null,
        Vector3? playerAt = null,
        bool withPlayer = true,
        ILogger logger = null)
    {
        var shard = new FakeShard();

        // Before the service is built: it reads the shard's logger in its constructor.
        shard.Logger = logger ?? Log.Logger;

        var data = new FakeWorldPopulationDataSource();
        var terrain = new FakeWorldPopulationTerrain();
        var spawner = new FakeWorldPopulationSpawner();

        // Placement is tested on its own; by default these tests want the spawns to happen.
        rules ??= new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,

            // These service tests exercise the full 64-slot plan in a short test window;
            // production's intentionally gentler default spawn rate is covered by the
            // explicit budget tests below.
            SpawnBudget = 12,
        };

        var service = new WorldPopulationService(shard, rules, data, terrain, spawner);

        var world = new World
        {
            Shard = shard,
            Data = data,
            Terrain = terrain,
            Spawner = spawner,
            Service = service,
            Rules = rules,
        };

        if (withPlayer)
        {
            world.Player = CreateCharacter(shard, playerAt ?? new Vector3(56f, 56f, 0f));
            world.Client = new FakeNetworkPlayer(shard) { CharacterEntity = world.Player };
            shard.Clients[world.Client.SocketId] = world.Client;
        }

        return world;
    }

    private static CharacterEntity CreateCharacter(FakeShard shard, Vector3 position)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetPosition(position);
        return character;
    }

    private static void Tick(World world, int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            world.Time += (ulong)Interval;
            world.Shard.CurrentTimeLong = world.Time;
            world.Service.Tick(Interval, world.Time, CancellationToken.None);
        }
    }

    [Fact]
    public void Tick_WithoutPlayersDoesNothingAtAll()
    {
        var world = CreateWorld(withPlayer: false);
        AddGroundAndRoster(world);

        Tick(world, 10);

        Assert.Empty(world.Spawner.Spawned);
        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(0, world.Service.ActiveCellCount);

        // Not even the plan is built: a zone nobody is in costs nothing.
        Assert.False(world.Service.Plan.IsComplete);
        Assert.Equal(0, world.Data.AnchorCalls);
    }

    [Fact]
    public void Tick_BuildsThePlanAndThenSpawnsAroundThePlayer()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);

        // One update per phase: surfaces, cells, slots. The update that finishes the plan also
        // activates the cells and starts spawning.
        Tick(world, 3);

        Assert.True(world.Service.Plan.IsComplete);
        Assert.Equal(16, world.Service.ActiveCellCount);
        Assert.Equal(world.Rules.SpawnBudget, world.Service.LiveCount);
        Assert.Equal(world.Rules.SpawnBudget, world.Spawner.Spawned.Count);

        Tick(world, 10);

        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(64, world.Service.Plan.SlotCount);
        Assert.All(world.Spawner.Spawned, spawn => Assert.True(spawn.Position.Length() < 200f));
    }

    [Fact]
    public void Tick_KeepsToTheSpawnBudgetPerWindow()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 2,
            SpawnBudgetWindowMs = 100,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 4);

        // Two per window, and every update is a fresh window at this interval.
        Assert.Equal(4, world.Service.LiveCount);
        Assert.Equal(4, world.Spawner.Spawned.Count);
    }

    [Fact]
    public void Tick_NeverGoesPastTheLiveCap()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            MaxLiveNpcs = 3,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 20);

        Assert.Equal(3, world.Service.LiveCount);
        Assert.Equal(3, world.Spawner.Spawned.Count);
        Assert.Equal(3, world.Spawner.Alive.Count);
    }

    [Fact]
    public void Tick_DoesNotSpawnOutsideTheActivationRadius()
    {
        var world = CreateWorld(playerAt: new Vector3(5_000f, 5_000f, 0f));
        AddGroundAndRoster(world);

        Tick(world, 8);

        Assert.True(world.Service.Plan.IsComplete);
        Assert.Equal(0, world.Service.ActiveCellCount);
        Assert.Equal(0, world.Service.LiveCount);
        Assert.Empty(world.Spawner.Spawned);
    }

    [Fact]
    public void Tick_DespawnsEverythingWhenThePlayerGoesAway()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);
        Assert.Equal(64, world.Service.LiveCount);

        // Past the deactivation radius, not just past the activation one.
        world.Player.SetPosition(new Vector3(5_000f, 5_000f, 0f));
        Tick(world);

        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(0, world.Service.ActiveCellCount);
        Assert.Equal(64, world.Spawner.Despawned.Count);
        Assert.Empty(world.Spawner.Alive);
    }

    [Fact]
    public void Tick_KeepsAnActiveCellWhileThePlayerIsInsideTheHysteresis()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);

        int live = world.Service.LiveCount;
        Assert.Equal(16, world.Service.ActiveCellCount);

        // Past the activation radius but inside the deactivation one: the cells stay active rather
        // than blinking out and back in behind a player who is walking around. Every cell of the
        // test plane remains within the default 225 m deactivation radius, so nothing may go away.
        world.Player.SetPosition(new Vector3(56f + 160f, 56f, 0f));
        Tick(world);

        Assert.Equal(live, world.Service.LiveCount);
        Assert.Equal(16, world.Service.ActiveCellCount);
    }

    [Fact]
    public void Tick_DespawnsEverythingWhenTheLastPlayerLeaves()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);

        world.Shard.Clients.Clear();
        Tick(world);

        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(0, world.Service.ActiveCellCount);
        Assert.Equal(64, world.Spawner.Despawned.Count);

        // The plan survives, so the zone comes back without being planned again: the roster was
        // read once, when the plan was built.
        Assert.True(world.Service.Plan.IsComplete);
        Assert.Equal(1, world.Data.CandidateCalls);
    }

    [Fact]
    public void TurningTheFeatureOffRemovesWhatItSpawned()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);
        Assert.Equal(64, world.Service.LiveCount);

        world.Service.Enabled = false;
        Tick(world);

        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(64, world.Spawner.Despawned.Count);
        Assert.Equal(0, world.Service.OccupancyCount);

        world.Service.Enabled = true;
        Tick(world, 2);

        Assert.True(world.Service.LiveCount > 0, "the zone did not fill back in");
    }

    [Fact]
    public void Tick_RefillsASlotAfterItsNpcDies()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
            RespawnDelayMs = 1_000,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);
        Tick(world, 8);

        int live = world.Service.LiveCount;
        ulong victim = world.Spawner.Alive.First();

        world.Spawner.Kill(victim);
        Tick(world);

        // Noticed, and waiting out the respawn delay rather than replaced on the spot.
        Assert.Equal(live - 1, world.Service.LiveCount);
        Assert.Equal(1, world.Service.LostTotal);

        Tick(world, 5);

        Assert.Equal(live, world.Service.LiveCount);
        Assert.Equal(65, world.Spawner.Spawned.Count);
    }

    [Fact]
    public void Tick_HonoursTheRowsOwnSpawnDelay()
    {
        var world = CreateWorld();
        world.Terrain.AddPlane(Vector3.Zero, 4, 4, 16f); // four cells
        world.Data.AddMonster(10, spawnDelayMs: 2_000);

        // The plan finishes on the third update, which is also where the cells are activated: the
        // row then asks for two seconds before its NPCs are active.
        Tick(world, 4);
        Assert.Equal(0, world.Service.LiveCount);
        Assert.Empty(world.Spawner.Spawned);

        Tick(world, 8);
        Assert.Equal(16, world.Service.LiveCount);
    }

    [Fact]
    public void Tick_DoesNotSpawnOnTopOfAPlayer()
    {
        var rules = new StandardWorldPopulationRules
        {
            PlanWorkPerTick = 100_000,
            MinPlayerDistance = 30f,
        };
        var world = CreateWorld(rules, playerAt: new Vector3(8f, 8f, 0f));
        world.Terrain.AddPlane(Vector3.Zero, 2, 2, 16f); // one cell, centred on the player
        world.Data.AddMonster(10);

        Tick(world, 6);

        Assert.Equal(0, world.Service.LiveCount);
        Assert.Empty(world.Spawner.Spawned);
        Assert.True(world.Service.RefusedPlacements > 0);

        // A player standing in the cell is worth waiting for, not giving up over.
        Assert.Equal(0, world.Service.ParkedSlotCount);
    }

    [Fact]
    public void Tick_ParksASlotWhoseGroundWillNotTakeIt()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            MaxPlacementFailures = 2,
            PlacementRetryDelayMs = 0,
        };
        var world = CreateWorld(rules);
        world.Terrain.AddPlane(Vector3.Zero, 2, 2, 16f); // one cell, four slots
        world.Terrain.AcceptPlacements = false;
        world.Data.AddMonster(10);

        Tick(world, 6);

        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(4, world.Service.ParkedSlotCount);
        Assert.True(world.Service.RefusedPlacements >= 4);

        // Parked means parked: no spin, and no more refusals to count.
        int parked = world.Service.ParkedSlotCount;
        int refused = world.Service.RefusedPlacements;
        int placementCalls = world.Terrain.PlacementCalls;
        Tick(world, 6);

        Assert.Equal(parked, world.Service.ParkedSlotCount);
        Assert.Equal(refused, world.Service.RefusedPlacements);
        Assert.Equal(placementCalls, world.Terrain.PlacementCalls);
    }

    [Fact]
    public void Tick_ParksASlotWhoseGroundIsCoveredFromAbove()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            MaxPlacementFailures = 2,
            PlacementRetryDelayMs = 0,
        };
        var world = CreateWorld(rules);
        world.Terrain.AddPlane(Vector3.Zero, 2, 2, 16f); // one cell, four slots
        world.Data.AddMonster(10);

        // Only the cell's own centre is in the open: the mesh found ground there, so the plan keeps
        // the cell - but every spot a body could actually stand, the centre plus any jitter, is
        // covered from above. A cave the cell's ground dips into.
        world.Terrain.ExposedToSky = position => position == new Vector3(8f, 8f, 0f);

        Tick(world, 6);

        // The covered ground is permanent, so the slots give up on it and are reported, not spun on.
        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(4, world.Service.ParkedSlotCount);
        Assert.True(world.Service.RefusedPlacements >= 4);

        // Parked means parked: no spin, and no more ground to ask.
        int parked = world.Service.ParkedSlotCount;
        int placementCalls = world.Terrain.PlacementCalls;
        Tick(world, 6);

        Assert.Equal(parked, world.Service.ParkedSlotCount);
        Assert.Equal(placementCalls, world.Terrain.PlacementCalls);
    }

    [Fact]
    public void Tick_ParkingSlots_SaysSoInTheLog_InsteadOfGoingQuiet()
    {
        var logger = new CapturingLogger();
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            MaxPlacementFailures = 2,
            PlacementRetryDelayMs = 0,
        };
        var world = CreateWorld(rules, logger: logger.Logger);
        world.Terrain.AddPlane(Vector3.Zero, 2, 2, 16f); // one cell, four slots
        world.Data.AddMonster(10);

        // The #112 cave shape: the cell's centre is in the open, so the plan keeps the cell, and
        // every spot a body could stand on is covered. The four slots park - and the log has to
        // say that a slot gave up, and why, because a world that emptied itself into parked slots
        // used to leave no trace of itself at all.
        world.Terrain.ExposedToSky = position => position == new Vector3(8f, 8f, 0f);

        Tick(world, 6);

        Assert.Equal(4, world.Service.ParkedSlotCount);

        // The first park names the slot and its reason: the zone covers the spot from above.
        Assert.Equal(1, logger.CountContaining("gave up on its ground"));
        Assert.Contains(
            "covers from above",
            logger.Messages.First(message => message.Contains("gave up on its ground", StringComparison.Ordinal)));
    }

    [Fact]
    public void Tick_AZoneWhoseSkyCheckKeepsSomeOpenGround_KeepsTheCheckTrusted()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        // The eastern half of the plane sits under cover: the two eastern cell columns are refused
        // by the sky check, the zone's open ground remains, and so the check is working, not
        // failing - it is trusted, the kept cells keep refusing cover at placement (that half is
        // the parking tests', whose kept cells hold covered spots), and the open half populates.
        world.Terrain.ExposedToSky = position => position.X < 64f;

        Tick(world, 10);

        Assert.False(world.Service.Plan.CoverCheckSuspect);
        Assert.True(world.Terrain.CoverRefusalsEnabled);
        Assert.Equal(8, world.Service.Plan.RefusedCoveredCells);

        // The open half spawns, and nothing parked: the kept cells lie entirely on exposed ground,
        // so no slot ever meets the cover check at placement in this zone.
        Assert.True(world.Service.LiveCount > 0);
        Assert.Equal(0, world.Service.ParkedSlotCount);
        Assert.Equal(0, world.Service.CoverRefusedPlacements);
    }

    [Fact]
    public void Tick_AZoneWhoseSkyCheckRefusesEverything_StillPopulates()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        // A zone whose collision covers every walkable spot from above - a proxy dome, sentinel
        // zone bounds, canopy cover over the whole map. The check agreeing with none of the zone's
        // own ground is the check being wrong, and a plan that obeyed it was the plan of nothing:
        // the zone went empty with nothing in the log to point at. The plan now overrides the
        // check, keeps the cells, and placement follows - populated beats empty, caves included.
        world.Terrain.ExposedToSky = _ => false;

        Tick(world, 6);

        Assert.True(world.Service.Plan.CoverCheckSuspect);
        Assert.False(world.Terrain.CoverRefusalsEnabled);
        Assert.Equal(16, world.Service.Plan.RefusedCoveredCells);
        Assert.Equal(16, world.Service.Plan.CellCount);

        // The zone fills, and nothing parked: cover refuses nothing here any more.
        Assert.NotEmpty(world.Spawner.Spawned);
        Assert.True(world.Service.LiveCount > 0);
        Assert.Equal(0, world.Service.ParkedSlotCount);
        Assert.Equal(0, world.Service.CoverRefusedPlacements);

        // The status says the check was overridden, so an operator looking at \population status
        // sees why this zone's plan kept its covered cells.
        Assert.Contains("was overridden", world.Service.DescribeStatus());
    }

    [Fact]
    public void Status_SaysSoWhenCellsAreUnderCover()
    {
        var world = CreateWorld();
        world.Terrain.AddPlane(Vector3.Zero, 8, 8, 16f); // 16 cells, the right half in a cave
        world.Terrain.ExposedToSky = position => position.X < 64f;
        world.Data.AddMonster(10);

        Tick(world, 4);

        Assert.True(world.Service.Plan.IsComplete);
        Assert.Equal(8, world.Service.Plan.RefusedCoveredCells);

        // A zone whose ground is largely covered says why its plan is smaller than its mesh.
        Assert.Contains("8 cells under cover", world.Service.DescribeStatus());
    }

    [Fact]
    public void Tick_SurvivesAZoneWhoseDataBlowsUpAndTurnsItselfOff()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        world.Terrain.ThrowOnPlacement = new InvalidOperationException("this zone's ground does not like being asked");

        // The shard's tick has to survive whatever one zone's data does: the feature gives up on
        // itself rather than throwing four times a second for the life of the process.
        Tick(world, 8);

        Assert.False(world.Service.Enabled);
        Assert.Equal(0, world.Service.LiveCount);
        Assert.Empty(world.Spawner.Spawned);
    }

    [Fact]
    public void Tick_SeparatesTheNpcsItSpawns()
    {
        var rules = new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            MaxNpcsPerCell = 2,
        };
        var world = CreateWorld(rules, playerAt: new Vector3(8f, 8f, 0f));
        world.Terrain.AddPlane(Vector3.Zero, 2, 2, 16f); // one cell, two slots

        // A body so wide that the cell cannot hold two of them: the second slot has to wait instead
        // of being stacked on the first.
        world.Data.AddMonster(10, bodyRadius: 30f, bodyHeight: 4f);

        Tick(world, 8);

        Assert.Equal(1, world.Service.LiveCount);
        Assert.Single(world.Spawner.Spawned);
        Assert.Equal(0, world.Service.ParkedSlotCount);
    }

    [Fact]
    public void Tick_SpawnsWithTheLevelOfTheAreaItIsIn()
    {
        var world = CreateWorld();
        world.Terrain.AddPlane(Vector3.Zero, 4, 4, 16f);
        world.Data.Anchors.Add(new WorldPopulationAnchor(
            new Vector3(16f, 16f, 0f),
            400f,
            WorldPopulationHabitat.Settlement,
            1001u));
        world.Data.LevelsByBand[1001u] = 12;
        world.Data.AddMonster(10, WorldPopulationHabitat.Settlement);

        Tick(world, 6);

        Assert.NotEmpty(world.Spawner.Spawned);
        Assert.All(world.Spawner.Spawned, spawn => Assert.Equal(12, spawn.Level));
    }

    [Fact]
    public void Tick_RegistersTheEntitiesThatWereAlreadyThere()
    {
        var world = CreateWorld(playerAt: new Vector3(8f, 8f, 0f));
        world.Terrain.AddPlane(Vector3.Zero, 2, 2, 16f);
        world.Data.AddMonster(10);

        // One of the zone's own entities, in the world before the plan was finished.
        var existing = CreateCharacter(world.Shard, new Vector3(200f, 200f, 0f));
        world.Shard.Entities[existing.EntityId] = existing;

        Tick(world, 4);

        Assert.True(world.Service.OccupancyCount > world.Service.LiveCount);
    }

    [Fact]
    public void Tick_DoesNothingWhenTheRulesSayTheFeatureIsOff()
    {
        var rules = new StandardWorldPopulationRules
        {
            Enabled = false,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 8);

        Assert.False(world.Service.Enabled);
        Assert.Equal(0, world.Service.LiveCount);
        Assert.False(world.Service.Plan.IsComplete);
    }

    [Fact]
    public void ListLiveNear_FindsTheNpcsAroundAPointNearestFirst()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);

        var near = world.Service.ListLiveNear(new Vector3(8f, 8f, 0f), 60f);

        Assert.NotEmpty(near);
        Assert.All(near, entry => Assert.True(entry.Distance <= 60f));
        for (int i = 1; i < near.Count; i++)
        {
            Assert.True(near[i - 1].Distance <= near[i].Distance);
        }

        Assert.Empty(world.Service.ListLiveNear(new Vector3(5_000f, 5_000f, 0f), 60f));
    }

    [Fact]
    public void Status_DescribesWhatTheServiceIsDoing()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);

        var status = world.Service.DescribeStatus();

        Assert.Contains("World population: on", status);
        Assert.Contains("Plan: 16 cells", status);
        Assert.Contains("Streaming: 16 active cells", status);
        Assert.Contains("64 spawned", status);

        world.Service.Enabled = false;
        Assert.StartsWith("World population: off", world.Service.DescribeStatus());
    }

    [Fact]
    public void Command_TogglesTheServiceAndReportsIt()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        world.Shard.WorldPopulation = world.Service;

        var lines = new List<string>();

        PopulationCommand.Run(world.Shard, ["off"], world.Player.Position, lines.Add);
        Assert.False(world.Service.Enabled);
        Assert.Contains(lines, line => line.StartsWith("World population off"));

        lines.Clear();
        PopulationCommand.Run(world.Shard, ["on"], world.Player.Position, lines.Add);
        Assert.True(world.Service.Enabled);
        Assert.Contains(lines, line => line.StartsWith("World population on"));

        lines.Clear();
        PopulationCommand.Run(world.Shard, [], null, lines.Add);
        Assert.Contains(lines, line => line.StartsWith("World population: on"));

        lines.Clear();
        PopulationCommand.Run(world.Shard, ["nonsense"], null, lines.Add);
        Assert.Contains(lines, line => line.Contains("Unknown population action"));

        lines.Clear();
        PopulationCommand.Run(world.Shard, ["near"], null, lines.Add);
        Assert.Contains(lines, line => line.Contains("no position in the world"));
    }

    [Fact]
    public void Command_SaysSoWhenTheShardHasNoPopulation()
    {
        var shard = new FakeShard();
        var lines = new List<string>();

        PopulationCommand.Run(shard, ["status"], null, lines.Add);

        Assert.Contains(lines, line => line.Contains("not available on this shard"));
    }

    [Fact]
    public void SpawningNothingBecauseTheFeatureIsOff_SaysSoOnce_AndNamesTheSetting()
    {
        var logger = new CapturingLogger();
        var world = CreateWorld(
            new StandardWorldPopulationRules { Enabled = false, PlanWorkPerTick = 100_000 },
            logger: logger.Logger);
        AddGroundAndRoster(world);

        Tick(world, 10);

        Assert.Empty(world.Spawner.Spawned);

        // Once, not once per update: this runs a few times a second for the life of the process.
        Assert.Equal(1, logger.CountContaining("SpawnWorldPopulation is false in the server settings"));
    }

    [Fact]
    public void SpawningNothingBecauseNobodyIsInTheZone_SaysSoOnce()
    {
        var logger = new CapturingLogger();
        var world = CreateWorld(withPlayer: false, logger: logger.Logger);
        AddGroundAndRoster(world);

        Tick(world, 10);

        Assert.Empty(world.Spawner.Spawned);
        Assert.Equal(1, logger.CountContaining("no player counts as present yet"));
    }

    [Fact]
    public void Tick_IgnoresPlayersInOtherZones()
    {
        var world = CreateWorld();

        // The shard runs New Eden (448); the only player picked Sertao in the zone picker.
        world.Client.CurrentZone = new Zone { ID = 1030, Name = "Sertao" };
        AddGroundAndRoster(world);

        Tick(world, 10);

        // Nobody here to stream to: not even the plan is built, exactly as if nobody were
        // connected at all - planning New Eden's ground for a player standing in Sertao would
        // only spend the plan work and then refuse every placement against the wrong map.
        Assert.Empty(world.Spawner.Spawned);
        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(0, world.Service.ActiveCellCount);
        Assert.False(world.Service.Plan.IsComplete);
        Assert.Equal(0, world.Data.AnchorCalls);
        Assert.Equal(0, world.Service.PlayerCount);
        Assert.Equal(1, world.Service.PlayersElsewhereCount);
        Assert.Contains("Sertao (1030)", world.Service.PlayersElsewhereZones);
    }

    [Fact]
    public void SpawningNothingBecausePlayersAreInOtherZones_SaysSoOnce_AndNamesTheFix()
    {
        var logger = new CapturingLogger();
        var world = CreateWorld(logger: logger.Logger);
        world.Client.CurrentZone = new Zone { ID = 1030, Name = "Sertao" };
        AddGroundAndRoster(world);

        Tick(world, 10);

        Assert.Empty(world.Spawner.Spawned);

        // Once, not once per update, naming the shard's zone, the player's zone and the fix.
        Assert.Equal(1, logger.CountContaining("spawning nothing in zone 448"));
        Assert.Equal(1, logger.CountContaining("are in other zones (Sertao (1030))"));
        Assert.Equal(1, logger.CountContaining("set ZoneId to its id in the server config and restart"));

        // And it is not the "nobody connected" message: there is a player, just not here.
        Assert.Equal(0, logger.CountContaining("no player counts as present yet"));
    }

    [Fact]
    public void Tick_PopulatesAroundInZonePlayersWhileIgnoringElsewhereOnes()
    {
        var world = CreateWorld();
        world.Client.CurrentZone = new Zone { ID = 448, Name = "New Eden" };

        var elsewherePlayer = CreateCharacter(world.Shard, new Vector3(56f, 56f, 0f));
        var elsewhereClient = new FakeNetworkPlayer(world.Shard)
        {
            SocketId = 2,
            CharacterEntity = elsewherePlayer,
            CurrentZone = new Zone { ID = 1030, Name = "Sertao" },
        };
        world.Shard.Clients[elsewhereClient.SocketId] = elsewhereClient;
        AddGroundAndRoster(world);

        Tick(world, 8);

        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(1, world.Service.PlayerCount);
        Assert.Equal(1, world.Service.PlayersElsewhereCount);

        var status = world.Service.DescribeStatus();
        Assert.Contains("1 players (1 in other zones: Sertao (1030))", status);
    }

    [Fact]
    public void Tick_ClearsWhenTheLastInZonePlayerLeavesForAnotherZone()
    {
        var world = CreateWorld();
        AddGroundAndRoster(world);
        Tick(world, 8);
        Assert.Equal(64, world.Service.LiveCount);

        // The player is still connected, but now in Sertao: nobody left to populate for.
        world.Client.CurrentZone = new Zone { ID = 1030, Name = "Sertao" };
        Tick(world);

        Assert.Equal(0, world.Service.LiveCount);
        Assert.Equal(0, world.Service.ActiveCellCount);
        Assert.Equal(64, world.Spawner.Despawned.Count);
        Assert.Empty(world.Spawner.Alive);
        Assert.Equal(1, world.Service.PlayersElsewhereCount);
    }

    [Fact]
    public void APlanThatTakesAWhileToBuild_SaysItIsStillBuilding_InsteadOfGoingQuiet()
    {
        var logger = new CapturingLogger();

        // One unit of plan work an update, over a plane big enough that the plan needs a few
        // hundred of them: the shape a zone takes when its navigation mesh is large.
        var world = CreateWorld(
            new StandardWorldPopulationRules { MinPlayerDistance = 0f, PlanWorkPerTick = 1 },
            logger: logger.Logger);
        world.Terrain.AddPlane(Vector3.Zero, 16, 16, 16f);
        for (uint id = 10; id < 15; id++)
        {
            world.Data.AddMonster(id);
        }

        Tick(world, 3);
        Assert.False(world.Service.Plan.IsComplete);
        Assert.Empty(world.Spawner.Spawned);

        // A plan that is getting along is not news yet.
        Assert.Equal(0, logger.CountContaining("still building the plan"));

        // Past the announcement interval it says so, with how far it has got, and keeps saying so:
        // this is the one reason that resolves on its own, so a plan that has stopped advancing
        // has to be visible in the log rather than indistinguishable from a feature switched off.
        Tick(world, 37);
        Assert.False(world.Service.Plan.IsComplete);
        Assert.Equal(1, logger.CountContaining("still building the plan"));
        Assert.Equal(1, logger.CountContaining("walkable surfaces scanned"));

        Tick(world, 40);
        Assert.Equal(2, logger.CountContaining("still building the plan"));
    }

    [Fact]
    public void AZoneThatSpawnsNormally_DoesNotReportThatItIsSpawningNothing()
    {
        var logger = new CapturingLogger();
        var world = CreateWorld(logger: logger.Logger);
        AddGroundAndRoster(world);

        Tick(world, 3);

        Assert.NotEmpty(world.Spawner.Spawned);
        Assert.Equal(0, logger.CountContaining("spawning nothing"));
        Assert.Equal(0, logger.CountContaining("still building the plan"));
    }

    [Fact]
    public void Tick_SpawnFullZone_SpawnsAllPlannedSlotsAcrossWholeZoneWithoutDistanceRestriction()
    {
        var rules = new StandardWorldPopulationRules
        {
            SpawnFullZone = true,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules, playerAt: new Vector3(5_000f, 5_000f, 0f));
        AddGroundAndRoster(world);

        Tick(world, 10);

        Assert.True(world.Service.Plan.IsComplete);
        Assert.Equal(16, world.Service.ActiveCellCount);
        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(64, world.Spawner.Spawned.Count);
    }

    [Fact]
    public void Tick_SpawnFullZone_DoesNotDespawnWhenPlayerMovesAway()
    {
        var rules = new StandardWorldPopulationRules
        {
            SpawnFullZone = true,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 10);
        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(16, world.Service.ActiveCellCount);

        world.Player.SetPosition(new Vector3(5_000f, 5_000f, 0f));
        Tick(world, 5);

        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(16, world.Service.ActiveCellCount);
        Assert.Empty(world.Spawner.Despawned);
    }

    [Fact]
    public void Tick_SpawnFullZone_SpawnsAndKeepsPopulationEvenWithoutPlayers()
    {
        var rules = new StandardWorldPopulationRules
        {
            SpawnFullZone = true,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules, withPlayer: false);
        AddGroundAndRoster(world);

        Tick(world, 10);

        Assert.True(world.Service.Plan.IsComplete);
        Assert.Equal(16, world.Service.ActiveCellCount);
        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(64, world.Spawner.Spawned.Count);
        Assert.Empty(world.Spawner.Despawned);
    }

    [Fact]
    public void Tick_SpawnFullZone_DoesNotDespawnWhenLastPlayerLeaves()
    {
        var rules = new StandardWorldPopulationRules
        {
            SpawnFullZone = true,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 10);
        Assert.Equal(64, world.Service.LiveCount);

        world.Shard.Clients.Clear();
        Tick(world, 5);

        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(16, world.Service.ActiveCellCount);
        Assert.Empty(world.Spawner.Despawned);
    }

    [Fact]
    public void Tick_SpawnFullZone_KilledNpcRespawns()
    {
        var rules = new StandardWorldPopulationRules
        {
            SpawnFullZone = true,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
            RespawnDelayMs = 1_000,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 10);
        Assert.Equal(64, world.Service.LiveCount);

        ulong victim = world.Spawner.Alive.First();
        world.Spawner.Kill(victim);
        Tick(world);

        Assert.Equal(63, world.Service.LiveCount);
        Assert.Equal(1, world.Service.LostTotal);

        Tick(world, 5);
        Assert.Equal(64, world.Service.LiveCount);
        Assert.Equal(65, world.Spawner.Spawned.Count);
    }

    [Fact]
    public void Status_DescribesFullZoneStreamingMode()
    {
        var rules = new StandardWorldPopulationRules
        {
            SpawnFullZone = true,
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
            SpawnBudget = 12,
        };
        var world = CreateWorld(rules);
        AddGroundAndRoster(world);

        Tick(world, 10);

        var status = world.Service.DescribeStatus();
        Assert.Contains("Streaming: 16 active cells", status);
        Assert.Contains("full zone (persistent)", status);
    }
}
