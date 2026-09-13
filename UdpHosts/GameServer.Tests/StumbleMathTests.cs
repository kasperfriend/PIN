using System.Collections.Generic;
using System.Numerics;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests;

public class StumbleMathTests
{
    private static readonly Vector3 North = new(0f, 1f, 0f);

    [Fact]
    public void AnimSubstate_HitFromInFront_IsFront()
    {
        Assert.Equal(StumbleMath.Front, StumbleMath.AnimSubstate(North, new Vector3(0f, 2f, 0f)));
    }

    [Fact]
    public void AnimSubstate_HitFromTheRight_IsRight()
    {
        // Model +X is right of a +Y facing (CharacterEntity.CalculateProjectileOrigin).
        Assert.Equal(StumbleMath.Right, StumbleMath.AnimSubstate(North, new Vector3(2f, 0f, 0f)));
    }

    [Fact]
    public void AnimSubstate_HitFromTheLeft_IsLeft()
    {
        Assert.Equal(StumbleMath.Left, StumbleMath.AnimSubstate(North, new Vector3(-2f, 0f, 0f)));
    }

    [Fact]
    public void AnimSubstate_HitFromBehind_IsBack()
    {
        Assert.Equal(StumbleMath.Back, StumbleMath.AnimSubstate(North, new Vector3(0f, -2f, 0f)));
    }

    [Fact]
    public void AnimSubstate_DegenerateFacing_IsFront()
    {
        Assert.Equal(StumbleMath.Front, StumbleMath.AnimSubstate(Vector3.Zero, North));
        Assert.Equal(StumbleMath.Front, StumbleMath.AnimSubstate(North, Vector3.Zero));
    }

    [Fact]
    public void CanApply_ZeroCooldown_AlwaysAllows()
    {
        Assert.True(StumbleMath.CanApply(1_000, 999, cooldownMs: 0, onlyOnce: false, alreadyApplied: false));
    }

    [Fact]
    public void CanApply_OnlyOnce_BlocksASecondPlay()
    {
        Assert.False(StumbleMath.CanApply(10_000, 0, cooldownMs: 0, onlyOnce: true, alreadyApplied: true));
        Assert.True(StumbleMath.CanApply(10_000, 0, cooldownMs: 0, onlyOnce: true, alreadyApplied: false));
    }

    [Fact]
    public void CanApply_Cooldown_BlocksUntilItHasRunOut()
    {
        Assert.False(StumbleMath.CanApply(1_500, 1_000, cooldownMs: 2_000, onlyOnce: false, alreadyApplied: false));
        Assert.True(StumbleMath.CanApply(3_000, 1_000, cooldownMs: 2_000, onlyOnce: false, alreadyApplied: false));
    }

    [Fact]
    public void CombatStumble_PicksTheLowestIdThatHasDirections()
    {
        var stumbles = new Dictionary<uint, Stumble>
        {
            [40] = new() { Id = 40 },
            [1] = new() { Id = 1 },
            [7] = new() { Id = 7 },
        };
        var directions = new Dictionary<uint, IReadOnlyList<StumbleDirection>>
        {
            [40] = [new StumbleDirection { StumbleId = 40, AnimSubstate = 0 }],
            [7] = [new StumbleDirection { StumbleId = 7, AnimSubstate = 0 }],
        };

        var picked = StumbleMath.CombatStumble(stumbles, directions);

        Assert.NotNull(picked);
        Assert.Equal(7u, picked.Id);
    }

    [Fact]
    public void DirectionFor_PicksTheFirstMatchingSubstate()
    {
        var rows = new List<StumbleDirection>
        {
            new() { AnimSubstate = 2, Duration = 100 },
            new() { AnimSubstate = 1, Duration = 200 },
            new() { AnimSubstate = 1, Duration = 300 },
        };

        var match = StumbleMath.DirectionFor(rows, StumbleMath.Right);

        Assert.NotNull(match);
        Assert.Equal(200u, match.Duration);
        Assert.Null(StumbleMath.DirectionFor(rows, StumbleMath.Front));
    }
}
