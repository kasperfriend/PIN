using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The walk over a weapon's ability chains: the database puts an attack's animation (and, for some
///     weapons, the attack's own damage) inside the status effects the ability applies, so finding them
///     means following apply/remove/update/duration chains, branches and called abilities. The chains used
///     here are the shapes the build's monster weapons actually have - see <c>Docs/NPC_AI.md</c>.
/// </summary>
public class NpcWeaponAbilitiesTests
{
    private const ushort PlayAnimation = (ushort)CommandType.PlayAnimation;
    private const ushort AbilityAnimation = (ushort)CommandType.AbilityAnimation;
    private const ushort InflictDamage = (ushort)CommandType.InflictDamage;
    private const ushort FireProjectile = (ushort)CommandType.FireProjectile;
    private const ushort ImpactApplyEffect = (ushort)CommandType.ImpactApplyEffect;
    private const ushort ConditionalBranch = (ushort)CommandType.ConditionalBranch;
    private const ushort Call = (ushort)CommandType.Call;

    [Fact]
    public void AWeaponWithNoAbilities_ScansToNothing()
    {
        var data = new FakeNpcAttackDataSource();

        var scan = NpcWeaponAbilities.Scan(data, 0, 0);

        Assert.False(scan.Animates);
        Assert.False(scan.DeliversDamage);
    }

    [Fact]
    public void AnAnimationInTheChains_IsFound()
    {
        // Melee - Shadowstrike's shape: the burst ability applies the swing effect, whose apply chain is
        // the animation command.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(188, 48_922)
            .WithCommand(48_922, ImpactApplyEffect, effectId: 176)
            .WithStatusEffect(176, applyChain: 109_904)
            .WithCommand(109_904, PlayAnimation);

        var scan = NpcWeaponAbilities.Scan(data, 0, 188);

        Assert.True(scan.Animates);
        Assert.False(scan.DeliversDamage);
    }

    [Fact]
    public void AnEffectTheChainApplies_CarriesTheAnimationAndTheHit()
    {
        // NPC Charge Up and Channel Fire's shape: applying the charge effect (its apply chain animates)
        // and then a command that hands the effect its duration.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(39_249, 1_038_403)
            .WithCommand(1_038_403, ImpactApplyEffect, next: 1_038_402, effectId: 10_496)
            .WithCommand(1_038_402, (ushort)CommandType.ReplenishEffectDuration)
            .WithStatusEffect(10_496, applyChain: 1_274_905, removeChain: 1_274_909)
            .WithCommand(1_274_905, AbilityAnimation)
            .WithCommand(1_274_909, FireProjectile);

        var scan = NpcWeaponAbilities.Scan(data, 39_249, 0);

        Assert.True(scan.Animates);
        Assert.True(scan.DeliversDamage);
    }

    [Fact]
    public void DamageInAnAppliedEffectsRemoveChain_CountsAsTheChainsOwnHit()
    {
        // The charge effect fires the weapon's own projectile when it is released, so the chain is the
        // attack - the AI must not fire one of its own on top.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(39_249, 1_038_403)
            .WithCommand(1_038_403, ImpactApplyEffect, effectId: 10_496)
            .WithStatusEffect(10_496, removeChain: 1_274_909)
            .WithCommand(1_274_909, FireProjectile);

        Assert.True(NpcWeaponAbilities.Scan(data, 39_249, 0).DeliversDamage);
    }

    [Fact]
    public void DamageInAnAppliedEffectsUpdateChain_CountsAsTheChainsOwnHit()
    {
        // The flamethrower's shape: the burst applies a burning effect whose update chain does the damage.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(38_264, 1_627_549)
            .WithCommand(1_627_549, ImpactApplyEffect, effectId: 8_785)
            .WithStatusEffect(8_785, updateChain: 1_000)
            .WithCommand(1_000, InflictDamage);

        var scan = NpcWeaponAbilities.Scan(data, 0, 38_264);

        Assert.True(scan.DeliversDamage);
        Assert.False(scan.Animates);
    }

    [Fact]
    public void AbilitiesBehindABranchOrACall_AreWalked()
    {
        var data = new FakeNpcAttackDataSource()
            .WithAbility(1, 100)
            .WithCommand(100, ConditionalBranch, thenChain: 200, elseChain: 300)
            .WithCommand(200, (ushort)CommandType.PlayAnimation)
            .WithCommand(300, Call, calledAbilityId: 2)
            .WithAbility(2, 400)
            .WithCommand(400, InflictDamage);

        var scan = NpcWeaponAbilities.Scan(data, 1, 0);

        Assert.True(scan.Animates);
        Assert.True(scan.DeliversDamage);
    }

    [Fact]
    public void ChainsThatLoopOrShareATail_AreWalkedOnce()
    {
        // A cycle (command 1 -> 2 -> 1) must not spin forever, and a tail two commands share must not be
        // walked twice.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(1, 1)
            .WithCommand(1, ImpactApplyEffect, next: 2, effectId: 176)
            .WithCommand(2, ImpactApplyEffect, next: 1, effectId: 176)
            .WithStatusEffect(176, applyChain: 3)
            .WithCommand(3, (ushort)CommandType.PlayAnimation);

        var scan = NpcWeaponAbilities.Scan(data, 1, 0);

        Assert.True(scan.Animates);
    }

    [Fact]
    public void AnEffectTheDatabaseDoesNotHave_IsSkipped()
    {
        // Weapon chains reference effect ids the loaded database may not carry; a missing row must end
        // that branch of the walk rather than throw or report a false animation.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(39_249, 1_038_403)
            .WithCommand(1_038_403, ImpactApplyEffect, next: 1_038_402, effectId: 10_496)
            .WithCommand(1_038_402, (ushort)CommandType.ReplenishEffectDuration);

        var scan = NpcWeaponAbilities.Scan(data, 39_249, 0);

        Assert.False(scan.Animates);
        Assert.False(scan.DeliversDamage);
    }
}
