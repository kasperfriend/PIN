using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The walk over a weapon's ability chains: the database puts what an attack draws - its animation, its
///     muzzle flash, its sound - inside the status effects the ability applies, and the client only runs
///     those commands when the effect is replicated. Finding them means following apply/remove/update/
///     duration chains, branches and called abilities, and telling the client's commands from the server's.
///     The chains used here are the shapes the build's monster weapons actually have - see
///     <c>Docs/NPC_AI.md</c>.
/// </summary>
public class NpcWeaponAbilitiesTests
{
    private const ushort PlayAnimation = (ushort)CommandType.PlayAnimation;
    private const ushort AbilityAnimation = (ushort)CommandType.AbilityAnimation;
    private const ushort PerformEmote = (ushort)CommandType.PerformEmote;
    private const ushort ParticleEffectAsset = (ushort)CommandType.ParticleEffectAsset;
    private const ushort AudioFeedback = (ushort)CommandType.AudioFeedback;
    private const ushort StatModifier = (ushort)CommandType.StatModifier;
    private const ushort RequireCState = (ushort)CommandType.RequireCState;
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

        Assert.False(scan.ClientFeedback);
        Assert.False(scan.DeliversDamage);
    }

    [Fact]
    public void AnAnimationInTheChains_IsClientFeedback()
    {
        // Melee - Shadowstrike's shape: the burst ability applies the swing effect, whose apply chain is
        // the animation command.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(188, 48_922)
            .WithCommand(48_922, ImpactApplyEffect, effectId: 176)
            .WithStatusEffect(176, applyChain: 109_904)
            .WithCommand(109_904, PlayAnimation);

        var scan = NpcWeaponAbilities.Scan(data, 0, 188);

        Assert.True(scan.ClientFeedback);
        Assert.False(scan.DeliversDamage);
    }

    [Fact]
    public void AnEmoteInTheChains_IsClientFeedback()
    {
        // 359 status effects in the build hold a tfPerformEmote command: the emote animation reaches the
        // client the same way an attack animation does, so the chain has to run for it to play.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(1, 100)
            .WithCommand(100, ImpactApplyEffect, effectId: 13_551)
            .WithStatusEffect(13_551, applyChain: 200)
            .WithCommand(200, PerformEmote);

        var scan = NpcWeaponAbilities.Scan(data, 1, 0);

        Assert.True(scan.ClientFeedback);
        Assert.False(scan.DeliversDamage);
    }

    [Fact]
    public void ParticleAndAudioCommandsInTheChains_AreClientFeedback()
    {
        // The shape 9 of the build's ability-bearing monster weapons have (the charge sniper 51, the plasma
        // cannon 12129, the fusion cannon 60, the flamethrower 12144, ...): the chain's effect applies a
        // muzzle flash and a weapon sound but no skeleton animation.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(34_894, 1_000)
            .WithCommand(1_000, ImpactApplyEffect, effectId: 2_759)
            .WithStatusEffect(2_759, applyChain: 1_100)
            .WithCommand(1_100, ParticleEffectAsset, next: 1_101)
            .WithCommand(1_101, AudioFeedback);

        var scan = NpcWeaponAbilities.Scan(data, 34_894, 0);

        Assert.True(scan.ClientFeedback);
        Assert.False(scan.DeliversDamage);
    }

    [Fact]
    public void ServerCommandsAlone_AreNotClientFeedback()
    {
        // The Vorrax beam's shape (template 29): the chain applies a stat modifier and holds it with a
        // state requirement. Nothing there is drawn by a client, so running the chain would change the
        // fight without changing what anyone sees.
        var data = new FakeNpcAttackDataSource()
            .WithAbility(30_010, 1_000)
            .WithCommand(1_000, ImpactApplyEffect, effectId: 356)
            .WithStatusEffect(356, applyChain: 1_100, durationChain: 1_200)
            .WithCommand(1_100, StatModifier)
            .WithCommand(1_200, RequireCState);

        var scan = NpcWeaponAbilities.Scan(data, 0, 30_010);

        Assert.False(scan.ClientFeedback);
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

        Assert.True(scan.ClientFeedback);
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
        Assert.False(scan.ClientFeedback);
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

        Assert.True(scan.ClientFeedback);
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

        Assert.True(scan.ClientFeedback);
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

        Assert.False(scan.ClientFeedback);
        Assert.False(scan.DeliversDamage);
    }
}
