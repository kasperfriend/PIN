using GameServer.Systems.Ai;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The magazine an armed NPC fires from and the reload it announces when it is empty: every number is a
///     column (attribute 956 else <c>base_clip_size</c>, <c>ammo_per_burst</c> else <c>rounds_per_burst</c>,
///     <c>reload_time</c>), and the reload is the window the client's <c>anim_reload_type</c> plays in.
/// </summary>
public class NpcWeaponMagazineTests
{
    [Fact]
    public void ResolveCapacity_PrefersTheItemsMagazineAttribute()
    {
        // NPC Guard Rifle's item carries attribute 956 = 60; the template's base_clip_size is 20.
        Assert.Equal(60, NpcWeaponMagazine.ResolveCapacity(60f, 20));
    }

    [Fact]
    public void ResolveCapacity_WithoutTheAttribute_UsesTheTemplateClip()
    {
        Assert.Equal(20, NpcWeaponMagazine.ResolveCapacity(0f, 20));
    }

    [Fact]
    public void ResolveCost_AmmoPerBurstWinsOverRoundsPerBurst()
    {
        // NPC Chosen Shotgun: rounds_per_burst 16 (its pellets) for ammo_per_burst 1 (its shell).
        Assert.Equal(1, NpcWeaponMagazine.ResolveCost(1, 16));

        // NPC Guard Rifle: ammo_per_burst 4 of rounds_per_burst 4.
        Assert.Equal(4, NpcWeaponMagazine.ResolveCost(4, 4));
    }

    [Fact]
    public void ResolveCost_WithoutAmmoPerBurst_UsesTheRounds()
    {
        // HMG and every row with ammo_per_burst 0 spends its rounds_per_burst.
        Assert.Equal(1, NpcWeaponMagazine.ResolveCost(0, 1));

        // A row the database gives neither still spends one round per attack.
        Assert.Equal(1, NpcWeaponMagazine.ResolveCost(0, 0));
    }

    [Fact]
    public void Reloads_NeedsARangedWeaponWithAMagazineAndAReloadTime()
    {
        Assert.True(NpcWeaponMagazine.Reloads(isRanged: true, capacity: 20, reloadTimeMs: 700));

        // A one-round clip reloads after every shot: the NPC Charge Sniper Rifle's clip is 1 and its
        // reload_time is 1,000 ms - the weapon is meant to be single shot.
        Assert.True(NpcWeaponMagazine.Reloads(isRanged: true, capacity: 1, reloadTimeMs: 1000));

        Assert.False(NpcWeaponMagazine.Reloads(isRanged: false, capacity: 20, reloadTimeMs: 700)); // melee row
        Assert.False(NpcWeaponMagazine.Reloads(isRanged: true, capacity: 20, reloadTimeMs: 0));    // no reload_time
        Assert.False(NpcWeaponMagazine.Reloads(isRanged: true, capacity: 0, reloadTimeMs: 700));   // nothing to load
    }

    [Fact]
    public void SingleRoundMagazine_IsEmptyAfterEveryBurst()
    {
        // The charge sniper rifle: one round, one round per attack, so the clip is dry as soon as it fires.
        var magazine = NpcWeaponMagazine.Loaded(1).Spend(1);

        Assert.False(magazine.CanFire(1, 1_000));

        magazine = magazine.StartReload(1_000, 1_000);
        Assert.False(magazine.CanFire(1, 1_500));

        magazine = magazine.FinishReload(1);
        Assert.True(magazine.CanFire(1, 2_000));
    }

    [Fact]
    public void Spend_TakesTheBurstFromTheMagazineAndNeverGoesNegative()
    {
        var magazine = NpcWeaponMagazine.Loaded(3).Spend(1);

        Assert.Equal(2, magazine.Rounds);

        Assert.Equal(0, magazine.Spend(4).Rounds);
    }

    [Fact]
    public void CanFire_IsFalseWhileTheMagazineIsEmptyOrTheReloadIsRunning()
    {
        var magazine = NpcWeaponMagazine.Loaded(2);

        Assert.True(magazine.CanFire(1, 1_000));

        magazine = magazine.Spend(1).Spend(1);
        Assert.Equal(0, magazine.Rounds);
        Assert.False(magazine.CanFire(1, 1_000));

        // The reload runs from 1,000 to 1,700 and blocks firing for all of it.
        magazine = magazine.StartReload(1_000, 700);
        Assert.True(magazine.IsReloading(1_699));
        Assert.False(magazine.CanFire(1, 1_699));
        Assert.False(magazine.HasFinishedReloading(1_699));

        Assert.True(magazine.HasFinishedReloading(1_700));
        Assert.False(magazine.IsReloading(1_700));

        // Whoever finishes the reload refills the magazine: the weapon fires again.
        magazine = magazine.FinishReload(2);
        Assert.Equal(2, magazine.Rounds);
        Assert.True(magazine.CanFire(1, 1_700));
    }

    [Fact]
    public void StartReload_WithoutAReloadTime_OpensNoWindow()
    {
        var magazine = NpcWeaponMagazine.Loaded(2).StartReload(1_000, 0);

        Assert.False(magazine.IsReloading(1_000));
        Assert.False(magazine.HasFinishedReloading(10_000));
    }

    [Fact]
    public void CancelReload_DropsTheWindowWithoutRefilling()
    {
        // The server side of the client's own CancelReload: the mob died mid-reload, so nothing is refilled.
        var magazine = NpcWeaponMagazine.Loaded(2).Spend(2).StartReload(1_000, 700).CancelReload();

        Assert.Equal(0, magazine.Rounds);
        Assert.False(magazine.IsReloading(1_100));
        Assert.False(magazine.HasFinishedReloading(1_700));
    }

    [Fact]
    public void None_IsInertAndNeverBlocksFiring()
    {
        // What a weapon with no magazine carries - every melee row. Nothing it is asked to do can make it
        // stop firing: that is the "fires forever" half of the rule.
        Assert.False(NpcWeaponMagazine.None.IsReloading(1_000));
        Assert.False(NpcWeaponMagazine.None.HasFinishedReloading(1_000));
        Assert.True(NpcWeaponMagazine.None.CanFire(1, 1_000));
        Assert.True(NpcWeaponMagazine.None.CanFire(110, 1_000));

        var spent = NpcWeaponMagazine.None.Spend(1).StartReload(1_000, 500).CancelReload().FinishReload(30);

        Assert.Equal(NpcWeaponMagazine.None, spent);
        Assert.True(spent.CanFire(1, 1_500));
    }
}
