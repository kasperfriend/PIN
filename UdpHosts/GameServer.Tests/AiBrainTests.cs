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

    /// <summary>The shipped melee tuning, for the tests that are about reach.</summary>
    private static StandardAiRules MeleeRules => new()
    {
        AggroRadius = 55f,
        AttackRange = 3.5f,
        AttackRangeExit = 5f,
        MaxAttackHeightDelta = 2.5f,
        StandoffRange = 2f,
        LeashRadius = 120f,
        HomeArrivalRadius = 2f,
        AttackCooldownMs = 1000,
        TargetLostTimeoutMs = 3000,
    };

    /// <summary>A perception where the target is alive and clearly visible.</summary>
    private static AiPerception Seen(float distanceToTarget, float distanceToHome, ulong now)
        => new(TargetId, true, true, distanceToTarget, distanceToHome, now);

    /// <summary>
    ///     A perception that separates the flat distance from the straight-line one: what a target
    ///     standing above (or below) the NPC looks like.
    /// </summary>
    private static AiPerception SeenAt(float flatDistance, float attackDistance, float heightDelta, float distanceToHome, ulong now)
        => new(TargetId, true, true, flatDistance, attackDistance, heightDelta, distanceToHome, now);

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
    public void GivenUp_TargetInsideAggroRange_StaysForgottenUntilSeenAgain()
    {
        // Regression: the give-up path drops the target to Idle, and the Idle acquisition
        // arm used to re-adopt it in the same decision because the target was merely alive.
        // A mob hidden behind cover inside the aggro radius therefore never gave up, it just
        // forgot and rediscovered the player every TargetLostTimeoutMs.
        var brain = new AiBrain(Rules, 0);
        brain.Decide(Seen(20f, 0f, 100));
        brain.Decide(Hidden(20f, 0f, 4000)); // gives up

        var stillHidden = brain.Decide(Hidden(20f, 0f, 5000));
        Assert.Equal(AiBrainState.Idle, stillHidden.State);

        var stillHiddenLater = brain.Decide(Hidden(20f, 0f, 20000));
        Assert.Equal(AiBrainState.Idle, stillHiddenLater.State);

        // One sighting is enough to re-engage.
        var seenAgain = brain.Decide(Seen(20f, 0f, 20100));
        Assert.Equal(AiBrainState.Chase, seenAgain.State);
        Assert.True(brain.WantsTarget);
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

    // --- Melee reach (PIN has no NPC projectiles) -------------------------

    [Fact]
    public void Chase_TargetFartherThanMeleeReach_NeverAttacks()
    {
        // Regression: the shipped attack range used to be 45 m, so a mob opened fire across the
        // whole aggro band even though an attack here is a direct hitscan with nothing to dodge.
        var brain = new AiBrain(MeleeRules, 0);

        brain.Decide(SeenAt(19.2f, 19.2f, 0f, 0f, 100)); // Idle -> Chase

        var decision = brain.Decide(SeenAt(19.2f, 19.2f, 0f, 0f, 200));

        Assert.Equal(AiBrainState.Chase, decision.State);
        Assert.False(decision.Attack);
        Assert.Equal(AiMovementIntent.TowardTarget, decision.Movement);
    }

    [Fact]
    public void Chase_TargetStraightAbove_NeverAttacks()
    {
        // A player standing on the platform over the mob's head is within a hair of it horizontally
        // and 8 m away in the only distance a swing is measured over.
        var brain = new AiBrain(MeleeRules, 0);

        brain.Decide(SeenAt(0.5f, 8f, 8f, 0f, 100));
        var decision = brain.Decide(SeenAt(0.5f, 8f, 8f, 0f, 200));

        Assert.Equal(AiBrainState.Chase, decision.State);
        Assert.False(decision.Attack);
        Assert.True(decision.FaceTarget, "the mob may still glare up at you");
    }

    [Fact]
    public void Chase_TargetAboveALowCrate_StillAttacks()
    {
        // The height band is not a "feet must be level" rule: someone on a crate, or mid jump, is
        // still in reach, and so is a mob that is standing slightly below you on a slope.
        var brain = new AiBrain(MeleeRules, 0);

        brain.Decide(SeenAt(1.2f, 1.6f, 1.1f, 0f, 100));
        var decision = brain.Decide(SeenAt(1.2f, 1.6f, 1.1f, 0f, 200));

        Assert.Equal(AiBrainState.Attack, decision.State);
        Assert.True(decision.Attack);
    }

    [Fact]
    public void Attack_TargetGainsHeight_FallsBackToChase()
    {
        var brain = new AiBrain(MeleeRules, 0);

        brain.Decide(SeenAt(1.5f, 1.5f, 0f, 0f, 100));
        var attacking = brain.Decide(SeenAt(1.5f, 1.5f, 0f, 0f, 200));
        Assert.True(attacking.Attack);

        var jumped = brain.Decide(SeenAt(1.5f, 9f, 9f, 0f, 300));

        Assert.Equal(AiBrainState.Chase, jumped.State);
        Assert.False(jumped.Attack);
    }

    [Fact]
    public void Attack_HopsBackInAndOutOfReach_DoesNotRefireEveryTick()
    {
        // The cooldown is armed by the attack that landed, so a target that steps out of reach and
        // back in a moment later must not be hit again for free.
        var brain = new AiBrain(MeleeRules, 0);

        brain.Decide(SeenAt(1.5f, 1.5f, 0f, 0f, 100));
        Assert.True(brain.Decide(SeenAt(1.5f, 1.5f, 0f, 0f, 200)).Attack);   // first swing, next at 1200
        Assert.Equal(AiBrainState.Chase, brain.Decide(SeenAt(6f, 6f, 0f, 0f, 300)).State);

        var backInRange = brain.Decide(SeenAt(1.5f, 1.5f, 0f, 0f, 400));

        Assert.Equal(AiBrainState.Attack, backInRange.State);
        Assert.False(backInRange.Attack);
    }

    /// <summary>A rifle's tuning, as <c>AiCombatTuning.FromProfile</c> builds it.</summary>
    private static AiCombatTuning RangedCombat => new(
        AttackRange: 30f,
        AttackRangeExit: 34.5f,
        StandoffRange: 20f,
        AttackCooldownMs: 2500,
        Ranged: true);

    [Fact]
    public void RangedWeapon_EngagesAtItsOwnRange_NotTheRulesReach()
    {
        var brain = new AiBrain(MeleeRules, 0, RangedCombat);

        Assert.Equal(30f, brain.AttackRange);
        Assert.Equal(2500, brain.AttackCooldownMs);

        brain.Decide(SeenAt(20f, 20f, 0f, 0f, 100));
        var decision = brain.Decide(SeenAt(20f, 20f, 0f, 0f, 200));

        Assert.Equal(AiBrainState.Attack, decision.State);
        Assert.True(decision.Attack);

        // A weaponless mob on the same rules keeps the 3.5 m melee reach.
        var melee = new AiBrain(MeleeRules, 0);
        melee.Decide(SeenAt(20f, 20f, 0f, 0f, 100));
        var meleeDecision = melee.Decide(SeenAt(20f, 20f, 0f, 0f, 200));

        Assert.Equal(AiBrainState.Chase, meleeDecision.State);
        Assert.False(meleeDecision.Attack);
    }

    [Fact]
    public void RangedWeapon_HoldsItsBehaviourStandoff()
    {
        var brain = new AiBrain(MeleeRules, 0, RangedCombat);

        var closing = brain.Decide(SeenAt(25f, 25f, 0f, 0f, 100));
        Assert.Equal(AiMovementIntent.TowardTarget, closing.Movement);

        var parked = brain.Decide(SeenAt(20f, 20f, 0f, 0f, 200));
        Assert.Equal(AiMovementIntent.None, parked.Movement);
    }

    [Fact]
    public void RangedWeapon_ShootsOverAHeightAMeleeSwingCouldNot()
    {
        // 10 m up, 12 m away: out of a melee swing's 2.5 m band, but inside a rifle's 30 m reach.
        var melee = new AiBrain(MeleeRules, 0);
        melee.Decide(SeenAt(10f, 12f, 10f, 0f, 100));
        var meleeDecision = melee.Decide(SeenAt(10f, 12f, 10f, 0f, 200));
        Assert.Equal(AiBrainState.Chase, meleeDecision.State);
        Assert.False(meleeDecision.Attack);

        var ranged = new AiBrain(MeleeRules, 0, RangedCombat);
        ranged.Decide(SeenAt(10f, 12f, 10f, 0f, 100));
        var rangedDecision = ranged.Decide(SeenAt(10f, 12f, 10f, 0f, 200));
        Assert.Equal(AiBrainState.Attack, rangedDecision.State);
        Assert.True(rangedDecision.Attack);
    }
}
