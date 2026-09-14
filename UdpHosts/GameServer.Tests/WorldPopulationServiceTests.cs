using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using GameServer.Entities.Character;
using GameServer.Systems.Spawning.Population;
using GameServer.Tests.Fakes;
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
        bool withPlayer = true)
    {
        var shard = new FakeShard();
        var data = new FakeWorldPopulationDataSource();
        var terrain = new FakeWorldPopulationTerrain();
        var spawner = new FakeWorldPopulationSpawner();

        // Placement is tested on its own; by default these tests want the spawns to happen.
        rules ??= new StandardWorldPopulationRules
        {
            MinPlayerDistance = 0f,
            PlanWorkPerTick = 100_000,
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
        // test plane is within 300 m of the new position, so nothing may go away.
        world.Player.SetPosition(new Vector3(56f + 220f, 56f, 0f));
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
}
