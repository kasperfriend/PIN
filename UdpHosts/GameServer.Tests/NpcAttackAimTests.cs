using System.Numerics;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Ai;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class NpcAttackAimTests
{
    private const float Gravity = 9.81f;

    // The parabola ProjectileSim integrates for a parabolic row:
    // start + v0*t + (0,0,-g)*t^2/2, v0 = speed * direction.
    private static Vector3 Simulate(Vector3 origin, Vector3 direction, float speed, float gravity, float time)
    {
        var velocity = direction * speed;
        return origin + (velocity * time) + new Vector3(0f, 0f, -0.5f * gravity * time * time);
    }

    [Fact]
    public void ShotDirection_StraightRow_AimsAtThePointAndReportsTheFlightTime()
    {
        var origin = new Vector3(0f, 0f, 1.6f);
        var aim = new Vector3(20f, 0f, 0.9f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, Vector3.Zero, 100f, 0f, out var flightTime);

        var expected = Vector3.Normalize(aim - origin);
        Assert.True(Vector3.Dot(direction, expected) > 0.9999f);
        Assert.Equal(Vector3.Distance(origin, aim) / 100f, flightTime, 5);
    }

    [Fact]
    public void ShotDirection_StationaryTarget_DoesNotLead()
    {
        var origin = Vector3.Zero;
        var aim = new Vector3(15f, 0f, 1f);

        var withSpeed = NpcAttackAim.ShotDirection(origin, aim, new Vector3(0f, 0f, 0f), 50f, 0f, out _);
        var without = NpcAttackAim.ShotDirection(origin, aim, Vector3.Zero, 50f, 0f, out _);

        Assert.Equal(without, withSpeed);
    }

    [Fact]
    public void ShotDirection_MovingTarget_LeadsByTheFlightTime()
    {
        var origin = Vector3.Zero;
        var aim = new Vector3(20f, 0f, 1f);
        var velocity = new Vector3(10f, 0f, 0f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, velocity, 100f, 0f, out var flightTime);

        // The shot must pass through where the target will be: aim + v * flight time, where the
        // flight time is the distance to that same point (the helper's two-iteration settle).
        var expectedPoint = aim + (velocity * flightTime);
        var expectedDirection = Vector3.Normalize(expectedPoint - origin);
        Assert.True(Vector3.Dot(direction, expectedDirection) > 0.9999f);

        // Leading a target running along the line of sight shifts the aim ahead of it.
        Assert.True(direction.X > Vector3.Normalize(aim - origin).X);
    }

    [Fact]
    public void ShotDirection_BadVelocity_IsClampedToMaxLead()
    {
        var origin = Vector3.Zero;
        var aim = new Vector3(20f, 0f, 1f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, new Vector3(1000f, 0f, 0f), 100f, 0f, out _);
        var expected = Vector3.Normalize(aim + new Vector3(NpcAttackAim.MaxLeadMetres, 0f, 0f) - origin);

        Assert.True(Vector3.Dot(direction, expected) > 0.9999f);
    }

    [Fact]
    public void ShotDirection_Parabolic_LandsOnThePointAtTheSameHeight()
    {
        var origin = new Vector3(0f, 0f, 1.6f);
        var aim = new Vector3(20f, 0f, 1.6f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, Vector3.Zero, 100f, Gravity, out var flightTime);

        var landed = Simulate(origin, direction, 100f, Gravity, flightTime);
        Assert.True(Vector3.Distance(landed, aim) < 0.01f, $"the round must land on the aim, it landed {landed}");
    }

    [Fact]
    public void ShotDirection_Parabolic_LandsOnAPointAboveTheMuzzle()
    {
        var origin = new Vector3(0f, 0f, 1f);
        var aim = new Vector3(10f, 0f, 8f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, Vector3.Zero, 50f, Gravity, out var flightTime);

        var landed = Simulate(origin, direction, 50f, Gravity, flightTime);
        Assert.True(Vector3.Distance(landed, aim) < 0.01f, $"the round must land on the aim, it landed {landed}");
    }

    [Fact]
    public void ShotDirection_Parabolic_LandsOnAPointBelowTheMuzzle()
    {
        var origin = new Vector3(0f, 0f, 5f);
        var aim = new Vector3(15f, 0f, 0f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, Vector3.Zero, 40f, Gravity, out var flightTime);

        var landed = Simulate(origin, direction, 40f, Gravity, flightTime);
        Assert.True(Vector3.Distance(landed, aim) < 0.01f, $"the round must land on the aim, it landed {landed}");
    }

    [Fact]
    public void ShotDirection_ParabolicWithLead_StillLandsOnTheLedPoint()
    {
        var origin = Vector3.Zero;
        var aim = new Vector3(20f, 0f, 1f);
        var velocity = new Vector3(8f, 0f, 0f);

        var direction = NpcAttackAim.ShotDirection(origin, aim, velocity, 100f, Gravity, out var flightTime);

        var landed = Simulate(origin, direction, 100f, Gravity, flightTime);
        var expectedPoint = aim + (velocity * flightTime);
        Assert.True(Vector3.Distance(landed, expectedPoint) < 0.01f);
    }

    [Fact]
    public void ShotDirection_UnreachableArc_FallsBackToTheStraightShot()
    {
        var origin = Vector3.Zero;
        var aim = new Vector3(50f, 0f, 0f);

        // 5 m/s cannot carry 50 m: no arc exists, so the old straight behaviour stands.
        var direction = NpcAttackAim.ShotDirection(origin, aim, Vector3.Zero, 5f, Gravity, out var flightTime);

        var expected = Vector3.Normalize(aim - origin);
        Assert.True(Vector3.Dot(direction, expected) > 0.9999f);
        Assert.Equal(Vector3.Distance(origin, aim) / 5f, flightTime, 5);
    }

    [Fact]
    public void ShotDirection_DegenerateInput_ReturnsZero()
    {
        Assert.Equal(Vector3.Zero, NpcAttackAim.ShotDirection(Vector3.Zero, Vector3.Zero, Vector3.Zero, 100f, 0f, out _));
        Assert.Equal(Vector3.Zero, NpcAttackAim.ShotDirection(Vector3.Zero, new Vector3(5f, 0f, 0f), Vector3.Zero, 0f, 0f, out _));
        Assert.Equal(Vector3.Zero, NpcAttackAim.ShotDirection(Vector3.Zero, new Vector3(5f, 0f, 0f), Vector3.Zero, float.NaN, 0f, out _));
    }

    [Fact]
    public void AimPoint_NoCollision_UsesTheDefaultMidTorso()
    {
        var shard = new FakeShard();
        var target = new CharacterEntity(shard, shard.GetNextGuid(0));
        target.SetPosition(new Vector3(3f, 4f, 5f));

        var point = NpcAttackAim.AimPoint(null, target);
        // 1.8 * 0.5 is not bit-exactly 0.9 in float, so compare by distance, not by value.
        Assert.True(Vector3.Distance(point, new Vector3(3f, 4f, 5f + NpcAttackAim.DefaultAimHeight)) < 0.001f,
            $"expected the mid-torso default at (3, 4, {5f + NpcAttackAim.DefaultAimHeight}), got {point}");
    }

    [Fact]
    public void AimPoint_WithPoseHeight_UsesItsMiddle()
    {
        var shard = new FakeShard();
        var target = new CharacterEntity(shard, shard.GetNextGuid(0));
        target.SetPosition(new Vector3(0f, 0f, 5f));
        target.Collision = new CharacterCollisionComponent
        {
            PoseTypeRecord = new PoseType { PhysicsHeight = 1.8f },
        };

        // The middle of the 1.8 m physics capsule: half the database's own height, not a guess.
        var point = NpcAttackAim.AimPoint(null, target);
        Assert.True(Vector3.Distance(point, new Vector3(0f, 0f, 5f + 1.8f * 0.5f)) < 0.001f,
            $"expected the capsule middle at (0, 0, {5f + 1.8f * 0.5f}), got {point}");
    }

    [Fact]
    public void AimPoint_SentinelOrNegativeHeight_FallsBackToTheDefaultMidTorso()
    {
        var shard = new FakeShard();
        var target = new CharacterEntity(shard, shard.GetNextGuid(0));
        target.SetPosition(Vector3.Zero);
        target.Collision = new CharacterCollisionComponent
        {
            // -1 is the database's "no value" sentinel.
            PoseTypeRecord = new PoseType { PhysicsHeight = -1f },
        };

        var point = NpcAttackAim.AimPoint(null, target);
        Assert.True(Vector3.Distance(point, new Vector3(0f, 0f, NpcAttackAim.DefaultAimHeight)) < 0.001f,
            $"expected the mid-torso default at (0, 0, {NpcAttackAim.DefaultAimHeight}), got {point}");
    }
}
