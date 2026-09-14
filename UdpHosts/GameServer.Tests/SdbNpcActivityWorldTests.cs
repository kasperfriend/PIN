using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Ai;
using GameServer.Systems.Emotes;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class SdbNpcActivityWorldTests
{
    [Fact]
    public void JoinsFunctionToThePlacedEntity()
    {
        var world = new World();
        var station = world.Add(116, new Vector3(2f, 3f, 0f));
        Assert.True(world.Reserve(10, "Work", out var spot));
        Assert.Equal(station.EntityId, spot.EntityId);
        Assert.Equal(station.Position, spot.Position);
        Assert.Equal((ushort)60, spot.EmoteId);
        Assert.Equal(3000, spot.DurationMs);
        Assert.False(spot.Holster);
    }

    [Fact]
    public void ATemplateWithoutAPlacedEntityCannotBeReserved()
    {
        var world = new World();
        Assert.False(world.Reserve(10, "Work", out _));
    }

    [Fact]
    public void OneStationHasOneOwnerAndAnotherNpcChoosesTheNextAvailableStation()
    {
        var world = new World();
        var nearest = world.Add(116, new Vector3(1f, 0f, 0f));
        var further = world.Add(116, new Vector3(3f, 0f, 0f));
        Assert.True(world.Reserve(10, "Work", out var first));
        Assert.True(world.Reserve(11, "Work", out var second));
        Assert.Equal(nearest.EntityId, first.EntityId);
        Assert.Equal(further.EntityId, second.EntityId);
        Assert.False(world.Reserve(12, "Work", out _));
        world.Activities.Release(12); // not the owner: cannot free either reservation
        Assert.True(world.Activities.IsValid(10, first));
        world.Activities.Release(10);
        Assert.True(world.Reserve(12, "Work", out var third));
        Assert.Equal(nearest.EntityId, third.EntityId);
    }

    [Fact]
    public void RemovedDeadOrMovedStationInvalidatesTheReservation()
    {
        var world = new World();
        var station = world.Add(116, Vector3.UnitX);
        Assert.True(world.Reserve(10, "Work", out var spot));
        station.SetPosition(Vector3.UnitY);
        Assert.False(world.Activities.IsValid(10, spot));
        station.SetPosition(Vector3.UnitX);
        station.MarkDead();
        Assert.False(world.Activities.IsValid(10, spot));
        world.Shard.Entities.Remove(station.EntityId);
        Assert.False(world.Activities.IsValid(10, spot));
    }

    [Theory]
    [InlineData("DynamicEmote(emote=\"does-not-exist\")")]
    [InlineData("DynamicEmote(emote=\"utility\",emoteDuration=NaN)")]
    [InlineData("DynamicEmote(emote=\"utility\",emoteDuration=-20)")]
    [InlineData("WaitForNextRestPosition(restFunction=\"Line Rest 1\")")]
    [InlineData("WaterPather")]
    [InlineData("PerformEmote(statusEffect=4381,stopEmoteOnExit=0)")]
    public void UnsupportedActivitySemanticsAreNotSubstituted(string behavior)
    {
        var world = new World();
        world.Rows[116].Behavior = behavior;
        world.Add(116, Vector3.UnitX);
        Assert.False(world.Reserve(10, "Work", out _));
    }

    [Fact]
    public void FiniteDurationsAndHolsteredEndEmoteComeFromTheDeployableBehavior()
    {
        var world = new World();
        world.Rows[116].Behavior = "DynamicEmoteHolstered(emote=\"utility\",emoteDuration=6000,endWithEmote=\"calm\")";
        world.Add(116, Vector3.UnitX);
        Assert.True(world.Reserve(10, "work", out var spot));
        Assert.Equal(6000, spot.DurationMs);
        Assert.True(spot.Holster);
        Assert.Equal((ushort)1062, spot.EndEmoteId);
    }

    [Theory]
    [InlineData(100f, 0f)]
    [InlineData(1f, 4f)]
    [InlineData(float.NaN, 0f)]
    public void DistantOtherFloorOrNonfiniteLocationsAreNotActivities(float x, float z)
    {
        var world = new World();
        world.Add(116, new Vector3(x, 0f, z));
        Assert.False(world.Reserve(10, "Work", out _));
    }

    [Fact]
    public void FunctionMustMatchAndPlayerOwnedObjectsAreNotBorrowed()
    {
        var world = new World();
        var station = world.Add(116, Vector3.UnitX);
        Assert.False(world.Reserve(10, "Bar", out _));
        station.Player = new FakeNetworkPlayer(world.Shard);
        Assert.False(world.Reserve(10, "Work", out _));
    }

    [Fact]
    public void ClearReleasesAllStationsAndReselectionCanAvoidThePreviousOne()
    {
        var world = new World();
        var first = world.Add(116, Vector3.UnitX);
        var second = world.Add(116, new Vector3(3f, 0f, 0f));
        Assert.True(world.Reserve(10, "Work", out _));
        world.Activities.Clear();
        Assert.True(world.Activities.TryReserve(11, "Work", Vector3.Zero, Vector3.Zero, 30f, first.EntityId, out var spot));
        Assert.Equal(second.EntityId, spot.EntityId);
    }

    private sealed class World
    {
        public World()
        {
            Activities = new SdbNpcActivityWorld(Shard, new EmoteService(new FakeEmoteDataSource()),
                deployables: id => Rows.GetValueOrDefault(id),
                functions: id => id == 1 ? new DeployableFunction { Id = 1, Name = "Work" } : null);
        }

        public FakeShard Shard { get; } = new();
        public Dictionary<uint, Deployable> Rows { get; } = new()
        {
            [116] = new Deployable { Id = 116, Function = 1, Behavior = "DynamicEmote(emote=\"utility\",emoteDuration=3000)" },
        };
        public SdbNpcActivityWorld Activities { get; }

        public DeployableEntity Add(uint type, Vector3 position)
        {
            var entity = new DeployableEntity(Shard, Shard.GetNextGuid(), type, 0);
            entity.SetPosition(position);
            Shard.Entities[entity.EntityId] = entity;
            return entity;
        }

        public bool Reserve(ulong npcId, string function, out NpcActivitySpot spot)
            => Activities.TryReserve(npcId, function, Vector3.Zero, Vector3.Zero, 30f, 0, out spot);
    }
}
