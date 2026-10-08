using System;
using System.Numerics;
using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcCombatPositioningTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OpponentMovingAroundCoverOrBlockingAFiringPositionCancelsGoal(bool cover, bool nowHidden)
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, cover, point => new[] { point }, _ => cover);
        Assert.NotNull(positioning.Goal(200));
        Assert.Null(positioning.ValidatedGoal(300, new Vector3(20f, 0f, 0f), 50f, _ => nowHidden));
        Assert.False(positioning.CanSearch(300));
        Assert.True(positioning.CanSearch(4100));
    }

    [Fact]
    public void OutOfReachOpponentInvalidatesGoalWithoutBypassingCooldown()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, false, point => new[] { point }, _ => false);
        var goal = positioning.Goal(200);
        Assert.NotNull(goal);
        Assert.Equal(goal, positioning.ValidatedGoal(200, new Vector3(20f, 0f, 0f), 50f, _ => false));
        Assert.Null(positioning.ValidatedGoal(300, new Vector3(100f, 0f, 0f), 50f, _ => false));
        Assert.False(positioning.CanSearch(300));
    }

    [Theory]
    [InlineData(0f, 0f, false)]
    [InlineData(0.2f, 0.19f, true)]
    [InlineData(0.2f, 0.2f, false)]
    [InlineData(1f, 0.99f, true)]
    public void AuthoredChanceIsEvaluatedOncePerSearch(float chance, float roll, bool moves)
    {
        var positioning = new NpcCombatPositioning();
        int queries = 0;
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, false,
            point => { queries++; return new[] { point }; }, _ => false,
            new NpcCombatMovementProfile { MoveChance = chance }, roll);
        Assert.Equal(moves, positioning.Goal(200).HasValue);
        Assert.Equal(moves, queries > 0);
        Assert.False(positioning.CanSearch(200));
        Assert.True(positioning.CanSearch(4100));
    }

    [Fact]
    public void AuthoredMaxMoveBoundsTravelAndDetours()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, false,
            point => new[] { point }, _ => false,
            new NpcCombatMovementProfile { MaxMove = 3f });
        Assert.True(positioning.Goal(200).HasValue);
        Assert.InRange(positioning.Goal(200).Value.Length(), 2f, 3.001f);

        positioning.Reset();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, false,
            point => new[] { new Vector3(0f, 4f, 0f), point }, _ => false,
            new NpcCombatMovementProfile { MaxMove = 3f });
        Assert.Null(positioning.Goal(200));
    }

    [Fact]
    public void NonGroundInvocationsDoNotUseHumanGroundTactics()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, true,
            _ => throw new InvalidOperationException("must not query ground paths"), _ => true,
            new NpcCombatMovementProfile { GroundTactics = false });
        Assert.Null(positioning.Goal(200));
    }

    [Fact]
    public void UnderFireSelectsReachableOccludedCoverAndThenReleasesIt()
    {
        var positioning = new NpcCombatPositioning();
        int queries = 0;
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, true,
            point => { queries++; return new[] { point }; },
            point => point.Y > 2f);

        var goal = positioning.Goal(200);
        Assert.True(goal.HasValue);
        Assert.True(goal.Value.Y > 2f);
        Assert.InRange(queries, 1, 8);
        Assert.False(positioning.CanSearch(200));
        Assert.Null(positioning.Goal(2600));
        Assert.False(positioning.CanSearch(2600));
        Assert.True(positioning.CanSearch(4100));
    }

    [Fact]
    public void AnEmptyPathCannotBecomeCover()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, true,
            _ => Array.Empty<Vector3>(), _ => true);
        Assert.Null(positioning.Goal(200));
    }

    [Fact]
    public void OpenTerrainDoesNotCountAsCover()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, true,
            point => new[] { point }, _ => false);
        Assert.Null(positioning.Goal(200));
    }

    [Fact]
    public void ATooCloseRangedEnemyMovesAwayInsteadOfFurtherIntoThePlayer()
    {
        var positioning = new NpcCombatPositioning();
        var target = new Vector3(3f, 0f, 0f);
        positioning.Search(100, 1, Vector3.Zero, target,
            Vector3.Zero, 120f, 15f, 50f, false,
            point => new[] { point }, _ => false);
        Assert.True(positioning.Goal(200).HasValue);
        Assert.True(Vector3.Distance(positioning.Goal(200).Value, target) > 4f);
    }

    [Fact]
    public void TacticalPositionsRespectTheLeash()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 2f, 15f, 50f, false,
            point => new[] { point }, _ => false);
        Assert.Null(positioning.Goal(200));
    }

    [Fact]
    public void OrdinaryRepositioningPreservesLineOfFire()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, false,
            point => new[] { point }, _ => true);
        Assert.Null(positioning.Goal(200));
    }

    [Fact]
    public void AnExcessivelyLongCoverDetourIsRejected()
    {
        var positioning = new NpcCombatPositioning();
        positioning.Search(100, 1, Vector3.Zero, new Vector3(20f, 0f, 0f),
            Vector3.Zero, 120f, 15f, 50f, true,
            point => new[] { new Vector3(0f, 30f, 0f), point }, _ => true);
        Assert.Null(positioning.Goal(200));
    }
}
