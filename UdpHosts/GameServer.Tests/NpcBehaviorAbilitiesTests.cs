using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The ability modules a monster behaviour set configures (<c>am1Id</c>/<c>am2Id</c>) resolve the way the
///     database chains them: a module id is looked up in <c>dbitems::AbilityModule</c>, whose
///     <c>ability_chain_id</c> is the <c>apt::AbilityData</c> to run. Of the 26 module ids the build's
///     monsters name, 25 resolve through a module row and 33812 - the dodge's second direction - is written
///     as an ability id already. The scan follows every chain the engine runs, so all 26 reach a client
///     command, 25 of them an animation, and 20 deliver their own damage.
/// </summary>
public class NpcBehaviorAbilitiesTests
{
    private const ushort AbilityAnimation = (ushort)CommandType.AbilityAnimation;
    private const ushort InflictDamage = (ushort)CommandType.InflictDamage;
    private const ushort ImpactApplyEffect = (ushort)CommandType.ImpactApplyEffect;
    private const ushort UpdateWaitAndFireOnce = (ushort)CommandType.UpdateWaitAndFireOnce;

    [Fact]
    public void Resolve_FollowsTheModuleTableIntoTheAbility()
    {
        // 86132 is the Move Then Fire module (the row for monster 548): the string names the module,
        // dbitems::AbilityModule 86132 names ability 36817, and the animation and the roar emote live in
        // that ability's chains.
        var data = new FakeNpcAttackDataSource()
            .WithAbilityModule(86_132, 36_817)
            .WithAbility(36_817, 852_784)
            .WithCommand(852_784, AbilityAnimation);

        var modules = NpcBehaviorAbilities.Resolve(
            NpcBehaviorParams.Parse("Arch_MoveThenFire_Base(am1Id=86132,am1Cooldown=8000,combatDist=30)"),
            data);

        var module = Assert.Single(modules);
        Assert.Equal(86_132u, module.Module.ModuleId);
        Assert.Equal(36_817u, module.AbilityId);
        Assert.True(module.ClientFeedback);
        Assert.True(module.Runnable);
    }

    [Fact]
    public void Resolve_ReadsTheValueWithNoModuleRowAsAnAbilityId()
    {
        // The dodge pair: 33833 is its own module and its own ability, while 33812 has no module row at all
        // (module 77388 names it) and the string writes the ability id.
        var data = new FakeNpcAttackDataSource()
            .WithAbilityModule(33_833, 33_833)
            .WithAbility(33_833, 452_122)
            .WithCommand(452_122, AbilityAnimation)
            .WithAbility(33_812, 358_561)
            .WithCommand(358_561, AbilityAnimation);

        var modules = NpcBehaviorAbilities.Resolve(BehaviorOfTheDodgePair(), data);

        Assert.Equal(2, modules.Count);
        Assert.Equal(33_833u, modules[0].Module.ModuleId);
        Assert.Equal(33_833u, modules[0].AbilityId);
        Assert.True(modules[0].Runnable);
        Assert.Equal(33_812u, modules[1].Module.ModuleId);
        Assert.Equal(33_812u, modules[1].AbilityId);
        Assert.True(modules[1].Runnable);
    }

    [Fact]
    public void Resolve_KeepsTheOrderTheDatabaseSpellsTheModulesIn()
    {
        var data = new FakeNpcAttackDataSource()
            .WithAbilityModule(33_833, 33_833)
            .WithAbility(33_833, 452_122)
            .WithCommand(452_122, AbilityAnimation);

        var modules = NpcBehaviorAbilities.Resolve(BehaviorOfTheDodgePair(), data);

        Assert.Equal(2, modules.Count);
        Assert.Equal(33_833u, modules[0].Module.ModuleId);
        Assert.Equal(33_812u, modules[1].Module.ModuleId);
    }

    [Fact]
    public void Resolve_ReportsTheHitAModuleDeliversItself()
    {
        // 88159 (26 references, the most-named module after the dodge pair) applies effect chains that draw
        // animation 26 and inflict their own damage: the module is the attack, so the AI must not add its own
        // hit on the same target.
        var data = new FakeNpcAttackDataSource()
            .WithAbilityModule(88_159, 37_359)
            .WithAbility(37_359, 600)
            .WithCommand(600, AbilityAnimation, next: 601)
            .WithCommand(601, InflictDamage);

        var module = Assert.Single(NpcBehaviorAbilities.Resolve(NpcBehaviorParams.Parse("Arch_Base(am1Id=88159)"), data));

        Assert.True(module.Runnable);
        Assert.True(module.DeliversDamage);
    }

    [Fact]
    public void Resolve_AModuleThatDrawsNothing_IsReportedButNotRunnable()
    {
        // No behaviour string in the build names such a module - once the walk follows every chain the engine
        // runs, all 26 reach a client command - but the gate has to hold for the shape: a module whose ability
        // only damages is not run, and the weapon is the visible attack of that window, exactly as it is for
        // a server-only weapon chain.
        var data = new FakeNpcAttackDataSource()
            .WithAbilityModule(12_0937, 38_700)
            .WithAbility(38_700, 700)
            .WithCommand(700, InflictDamage);

        var module = Assert.Single(NpcBehaviorAbilities.Resolve(NpcBehaviorParams.Parse("Arch_Base(am1Id=120937)"), data));

        Assert.Equal(38_700u, module.AbilityId);
        Assert.True(module.DeliversDamage);
        Assert.False(module.ClientFeedback);
        Assert.False(module.Runnable);
    }

    [Fact]
    public void Resolve_FindsTheHitAndTheAnimationBehindAWait()
    {
        // Ability 38700 (module 120937, monster 2241's staged attack) keeps its animations 22 and 26 and its
        // damage behind the update loop of the effects its logic branches apply: an update loop that waits,
        // then fires the chain that holds them. Reporting only what a shallow walk sees would call the module
        // harmless and let the AI fire its own attack on top of the stage.
        var data = new FakeNpcAttackDataSource()
            .WithAbilityModule(12_0937, 38_700)
            .WithAbility(38_700, 957_984)
            .WithCommand(957_984, ImpactApplyEffect, effectId: 9_920)
            .WithStatusEffect(9_920, updateChain: 1_110_491)
            .WithCommand(1_110_491, UpdateWaitAndFireOnce, waitChain: 1_000_536)
            .WithCommand(1_000_536, AbilityAnimation, next: 1_000_530)
            .WithCommand(1_000_530, InflictDamage);

        var module = Assert.Single(NpcBehaviorAbilities.Resolve(NpcBehaviorParams.Parse("Arch_Base(am1Id=120937)"), data));

        Assert.True(module.ClientFeedback);
        Assert.True(module.DeliversDamage);
        Assert.True(module.Runnable);
    }

    [Fact]
    public void Resolve_NothingResolves_IsReportedAsNotRunnable()
    {
        // A module the database has no row for and no ability of that id either: nothing to run, and the AI
        // keeps the weapon attack it would have made anyway.
        var data = new FakeNpcAttackDataSource();

        var module = Assert.Single(NpcBehaviorAbilities.Resolve(NpcBehaviorParams.Parse("Arch_Base(am1Id=999999)"), data));

        Assert.Equal(0u, module.AbilityId);
        Assert.False(module.Runnable);
    }

    [Fact]
    public void Resolve_BehaviorWithoutModules_IsEmpty()
    {
        var data = new FakeNpcAttackDataSource();

        Assert.Empty(NpcBehaviorAbilities.Resolve(NpcBehaviorParams.Parse("AggressiveWanderer"), data));
        Assert.Empty(NpcBehaviorAbilities.Resolve(NpcBehaviorParams.Parse("Arch_MedRangedHumanoid_Attack(triggerPullTime=1500)"), data));
        Assert.Empty(NpcBehaviorAbilities.Resolve(null, data));
    }

    private static NpcBehaviorParams BehaviorOfTheDodgePair()
    {
        return NpcBehaviorParams.Parse(
            "Arch_MedRangedHumanoid_Base(triggerPullTime=5000,am1Id = 33833, am1Cooldown = 1700, am1Chance = 0.65, am2Id = 33812, am2Cooldown = 1700, am2Chance = 0.65)");
    }
}
