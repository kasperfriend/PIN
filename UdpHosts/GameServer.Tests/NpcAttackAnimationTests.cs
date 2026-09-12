using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

public class NpcAttackAnimationTests
{
    [Fact]
    public void ResolveDurationMs_UsesTheWeaponsOwnBurstTiming()
    {
        // NPC Guard Rifle: ms_per_burst 100, while the behaviour pulls the trigger every 2,500 ms.
        Assert.Equal(100u, NpcAttackAnimation.ResolveDurationMs(100, 2500));
    }

    [Fact]
    public void ResolveDurationMs_ClampsToTheAttackCycle()
    {
        // A reload-length burst column must not outlive the attack that started it, or the end marker
        // would land after the next burst's start marker.
        Assert.Equal(2000u, NpcAttackAnimation.ResolveDurationMs(5000, 2000));
    }

    [Fact]
    public void ResolveDurationMs_WithoutAWeaponRow_UsesTheUnarmedSwing()
    {
        Assert.Equal(NpcAttackAnimation.DefaultDurationMs, NpcAttackAnimation.ResolveDurationMs(0, 1200));
    }

    [Fact]
    public void ResolveDurationMs_WithoutACadence_KeepsTheWeaponTiming()
    {
        // NPC Melee Medium (Spyder): a 1,600 ms swing.
        Assert.Equal(1600u, NpcAttackAnimation.ResolveDurationMs(1600, 0));
    }

    [Fact]
    public void Start_EndsAfterTheDuration()
    {
        var animation = NpcAttackAnimation.Start(1_000, 500);

        Assert.Equal(1_000ul, animation.StartTime);
        Assert.Equal(1_500ul, animation.EndTime);
        Assert.True(animation.IsActive(1_499));
        Assert.False(animation.IsActive(1_500));
    }

    [Fact]
    public void Start_WithAZeroDuration_StillOpensAWindow()
    {
        var animation = NpcAttackAnimation.Start(1_000, 0);

        Assert.True(animation.IsActive(1_000));
        Assert.False(animation.IsActive(1_001));
    }

    [Fact]
    public void None_IsNeverActive()
    {
        Assert.False(NpcAttackAnimation.None.IsActive(0));
        Assert.False(NpcAttackAnimation.None.IsActive(60_000));
    }
}
