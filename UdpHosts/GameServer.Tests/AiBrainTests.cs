using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class AiBrainTests
{
    private const ulong TargetId = 42;

    private static StandardAiRules Rules => new()
    {
        AggroRadius = 50f,
        AttackRange = 30f,
        AttackRangeExit = 35f,
        StandoffRange = 4f,
        LeashRadius = 100f,
        HomeArrivalRadius = 2f,
        AttackCooldownMs = 1000,
        TargetLostTimeoutMs = 3000,
    };

    /// <summary>A perception where the target is alive and clearly visible.</summary>
    private static AiPerception Seen(float distanceToTarget, float distanceToHome, ulong now)
        => new(TargetId, true, true, distanceToTarget, distanceToHome, now);

    /// <summary>A perception where the target is alive but hidden behind something.</summary>
    private static AiPerception Hidden(float distanceToTarget, float distanceToHome, ulong now)
        => new(TargetId, true, false, distanceToTarget, distanceToHome, now);

    /// <summary>A perception with nobody to fight.</summary>
    private static AiPerception Empty(float distanceToHome, ulong now)
        => new(0, false, false, float.MaxValue, distanceToHome, now);

    [Fact]
    public void Idle_WithoutTarget_StaysIdle()
    {
        var brain = new AiBrain(Rules, 0);

        var decision = brain.Decide(Empty(0, 100));

        Assert.Equal(AiBrainState.Idle, decision.State);
        Assert.Equal(AiMovementIntent.None, decision.Movement);
        Assert.False(decision.Attack);
    }

    [Fact]
    public void Idle_TargetInsideAggroRadius_StartsChasing()
    {
        var brain = new AiBrain(Rules, 0);

        var decision = brain.Decide(Seen(20f, 0f, 100));

        Assert.Equal(AiBrainState.Chase, decision.State);
        Assert.Equal(AiMovementIntent.TowardTarget, decision.Movement);
        Assert.True(brain.WantsTarget);
    }

    [Fact]
    public void Idle_TargetOutsideAggroRadius_StaysIdle()
    {
        var brain = new AiBrain(Rules, 0);

        var decision = brain.Decide(Seen(60f, 0f, 100));

        Assert.Equal(AiBrainState.Idle, decision.State);
        Assert.Equal(AiMovementIntent.None, decision.Movement);
        Assert.False(brain.WantsTarget);
    }

    [Fact]
    public void Chase_TargetInsideAttackRange_SwitchesToAttack()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100)); // Idle -> Chase

        var decision = brain.Decide(Seen(20f, 0f, 200));

        Assert.Equal(AiBrainState.Attack, decision.State);
        Assert.True(decision.FaceTarget);
    }

    [Fact]
    public void Attack_FiresOncePerCooldown()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100)); // Idle -> Chase

        var first = brain.Decide(Seen(20f, 0f, 200)); // Chase -> Attack, first shot
        var tooSoon = brain.Decide(Seen(20f, 0f, 700));
        var readyAgain = brain.Decide(Seen(20f, 0f, 1200));

        Assert.True(first.Attack);
        Assert.False(tooSoon.Attack);
        Assert.True(readyAgain.Attack);
    }

    [Fact]
    public void Attack_TargetBeyondExitRange_FallsBackToChase()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));
        brain.Decide(Seen(20f, 0f, 200)); // now attacking

        // Still inside the entry range would flip-flop, the exit band is what keeps it stable.
        var insideExitBand = brain.Decide(Seen(33f, 0f, 300));
        var outsideExitBand = brain.Decide(Seen(40f, 0f, 400));

        Assert.Equal(AiBrainState.Attack, insideExitBand.State);
        Assert.Equal(AiBrainState.Chase, outsideExitBand.State);
    }

    [Fact]
    public void Attack_LosesLineOfSight_FallsBackToChase()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));
        brain.Decide(Seen(20f, 0f, 200));

        var decision = brain.Decide(Hidden(20f, 0f, 300));

        Assert.Equal(AiBrainState.Chase, decision.State);
        Assert.False(decision.Attack);
    }

    [Fact]
    public void Attack_WithinStandoff_HoldsPosition()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));
        brain.Decide(Seen(20f, 0f, 200));

        var decision = brain.Decide(Seen(2f, 0f, 300));

        Assert.Equal(AiMovementIntent.None, decision.Movement);
    }

    [Fact]
    public void Chase_TargetDies_DropsBackToIdle()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));
        Assert.Equal(AiBrainState.Chase, brain.State);

        var decision = brain.Decide(Empty(0f, 200));

        Assert.Equal(AiBrainState.Idle, decision.State);
        Assert.False(brain.WantsTarget);
    }

    [Fact]
    public void Chase_TargetUnseenWithinTimeout_KeepsChasing()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));

        var decision = brain.Decide(Hidden(20f, 0f, 1000));

        Assert.Equal(AiBrainState.Chase, decision.State);
    }

    [Fact]
    public void Chase_TargetUnseenBeyondTimeout_GivesUp()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));

        var decision = brain.Decide(Hidden(20f, 0f, 4000));

        Assert.Equal(AiBrainState.Idle, decision.State);
        Assert.False(brain.WantsTarget);
    }

    [Fact]
    public void AnyState_DraggedPastLeash_WalksHome()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));
        Assert.Equal(AiBrainState.Chase, brain.State);

        var decision = brain.Decide(Seen(20f, 150f, 200));

        Assert.Equal(AiBrainState.Return, decision.State);
        Assert.Equal(AiMovementIntent.TowardHome, decision.Movement);
        Assert.False(brain.WantsTarget);
        Assert.False(decision.Attack);
    }

    [Fact]
    public void Return_ArrivingHome_GoesIdle()
    {
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 150f, 100));
        Assert.Equal(AiBrainState.Return, brain.State);

        var decision = brain.Decide(Empty(1f, 200));

        Assert.Equal(AiBrainState.Idle, decision.State);
        Assert.Equal(AiMovementIntent.None, decision.Movement);
    }

    [Fact]
    public void Aggro_FromIdle_EngagesBeyondAggroRadius()
    {
        var brain = new AiBrain(Rules, 0);

        brain.Aggro(100);
        var decision = brain.Decide(Seen(200f, 0f, 200));

        Assert.Equal(AiBrainState.Chase, decision.State);
        Assert.Equal(AiMovementIntent.TowardTarget, decision.Movement);
    }

    [Fact]
    public void Aggro_AfterDeath_IsIgnored()
    {
        var brain = new AiBrain(Rules, 0);
        brain.OnDeath();

        brain.Aggro(100);

        Assert.Equal(AiBrainState.Dead, brain.State);
    }

    [Fact]
    public void Dead_NeverDecidesAgain()
    {
        var brain = new AiBrain(Rules, 0);
        brain.OnDeath();

        var decision = brain.Decide(Seen(5f, 0f, 100));

        Assert.Equal(AiBrainState.Dead, decision.State);
        Assert.Equal(AiMovementIntent.None, decision.Movement);
        Assert.False(decision.Attack);
        Assert.False(brain.WantsTarget);
    }
}
