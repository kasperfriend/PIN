using System;
using System.Collections.Generic;
using System.Reflection;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.apt;
using GameServer.StaticDB.Records.aptfs;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Activation;
using GameServer.Systems.Aptitude.Commands.Duration;
using GameServer.Systems.Aptitude.Commands.Impact;
using GameServer.Systems.Aptitude.Commands.Logic;
using GameServer.Systems.Aptitude.Commands.Other;
using GameServer.Systems.Aptitude.Commands.Register;
using GameServer.Systems.Aptitude.Commands.Requirement;
using GameServer.Systems.Aptitude.Commands.Target;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The consumable path end to end, in the shapes the shipped data uses: a Health Pack
///     (<c>TimeCooldown, StatRequirement, ConsumeItem, ImpactApplyEffect, InstantActivation</c>, item 30287)
///     and a 1-Use Glider Pad (<c>ConditionalBranch(if airborne; else notify, InstantActivation, Return),
///     ConsumeItem, DeployableSpawn ...</c>, item 32755). The item must leave the inventory exactly when
///     the use goes through - not when the pack is refused at full health, not when a second click lands
///     inside the cooldown, and not when the grounded glider pad branch already answered the player.
/// </summary>
public class ConsumableActivationTests
{
    private const uint HealthPack = 30287;
    private const uint GliderPad = 32755;
    private const uint HealthPackAbility = 30287;
    private const uint GliderPadAbility = 32755;
    private const uint HealthPackChain = 680632;
    private const uint GliderPadChain = 1147321;
    private const uint HealEffect = 526;
    private const uint NotifyEffect = 11819;

    [Fact]
    public void HealthPack_Use_SpendsOneAndHeals()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(HealthPackAbility, HealthPackChain);
        factory.Chains[HealthPackChain] = HealthPackChainFor(factory);
        player.Inventory.AddResource(HealthPack, 2);
        character.SetMaxHealth(1000, resetCurrent: true);
        character.SetCurrentHealth(400);

        Assert.True(Activate(shard, character, HealthPackAbility, HealthPack));

        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));
        Assert.Equal(400 + 200, character.CurrentHealth); // 0.2 x MaxHealth, from LoadRegisterFromStat(MaxHealth).
        Assert.NotNull(shard.Abilities.GetOrAddState(character).GetActiveCooldown(HealthPackAbility, 0, shard.CurrentTime));
    }

    [Fact]
    public void HealthPack_AtFullHealth_IsRefusedAndKept()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(HealthPackAbility, HealthPackChain);
        factory.Chains[HealthPackChain] = HealthPackChainFor(factory);
        player.Inventory.AddResource(HealthPack, 1);
        character.SetMaxHealth(1000, resetCurrent: true);

        Assert.False(Activate(shard, character, HealthPackAbility, HealthPack));

        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));
        Assert.Null(shard.Abilities.GetOrAddState(character).GetActiveCooldown(HealthPackAbility, 0, shard.CurrentTime));
    }

    [Fact]
    public void HealthPack_DuringCooldown_IsRefusedAndKept()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(HealthPackAbility, HealthPackChain);
        factory.Chains[HealthPackChain] = HealthPackChainFor(factory);
        player.Inventory.AddResource(HealthPack, 2);
        character.SetMaxHealth(1000, resetCurrent: true);
        character.SetCurrentHealth(100);

        Assert.True(Activate(shard, character, HealthPackAbility, HealthPack));
        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));

        shard.CurrentTimeLong += 1000;
        Assert.False(Activate(shard, character, HealthPackAbility, HealthPack));
        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));
        Assert.Equal(300, character.CurrentHealth);
    }

    /// <summary>
    ///     224 consumable chains spend the item before their <c>InstantActivation</c> gate, 48 of them
    ///     (Ammo Pack 30192, Arcfold Beacon 34132) with no <c>TimeCooldown</c> check ahead of it. A
    ///     rejection after the item is gone must hand it back, and no cooldown may start.
    /// </summary>
    [Fact]
    public void ConsumeItem_LaterNodeFails_ReturnsTheStackedItem()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(90, 900);
        factory.Chains[900] = Commands(
            new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 }),
            new InstantActivationCommand(new InstantActivationCommandDef { Id = 2, LocalCooldown = 60000 }),
            new ProbeCommand(result: false));
        player.Inventory.AddResource(HealthPack, 1);

        Assert.False(Activate(shard, character, 90, HealthPack));

        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));
        Assert.Null(shard.Abilities.GetOrAddState(character).GetActiveCooldown(90, 0, shard.CurrentTime));
    }

    /// <summary>Guid copies (older inventories) roll back the same way, keeping their guid.</summary>
    [Fact]
    public void ConsumeItem_LaterNodeFails_RestoresTheGuidItem()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(91, 910);
        factory.Chains[910] = Commands(
            new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 }),
            new ProbeCommand(result: false));
        var guid = player.Inventory.CreateItem(HealthPack);

        Assert.False(Activate(shard, character, 91, HealthPack));

        Assert.True(player.Inventory.TryGetItem(guid, out var item));
        Assert.Equal(HealthPack, item.SdbId);
    }

    /// <summary>A rejection inside a called ability is part of the root activation and rolls it back too.</summary>
    [Fact]
    public void ConsumeItem_CalledAbilityFails_ReturnsTheItem()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(92, 920, 93, 930);
        factory.Chains[930] = Commands(new ProbeCommand(result: false));
        factory.Chains[920] = Commands(
            new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1 }),
            new CallCommand(new CallCommandDef { Id = 2, AbilityId = 93 }));
        player.Inventory.AddResource(HealthPack, 1);

        Assert.False(Activate(shard, character, 92, HealthPack));

        Assert.Equal(1u, player.Inventory.GetResourceQuantity(HealthPack));
    }

    [Fact]
    public void GliderPad_OnTheGround_ReturnsBeforeConsumeItem()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(GliderPadAbility, GliderPadChain);
        var spawn = GliderPadChainsFor(factory);
        player.Inventory.AddResource(GliderPad, 1);

        // Grounded: the else branch notifies, queues the cooldown and returns; the root chain
        // reports success (the branch ran) but never reaches ConsumeItem/DeployableSpawn.
        Assert.True(Activate(shard, character, GliderPadAbility, GliderPad));

        Assert.Equal(1u, player.Inventory.GetResourceQuantity(GliderPad));
        Assert.Contains(character.GetActiveEffects(), state => state?.Effect.Id == NotifyEffect);
        Assert.False(spawn.Executed);
    }

    [Fact]
    public void GliderPad_Airborne_SpendsThePad()
    {
        var (shard, factory, character, player) = CreateRuntime();
        using var abilities = RegisterAbility(GliderPadAbility, GliderPadChain);
        var spawn = GliderPadChainsFor(factory);
        player.Inventory.AddResource(GliderPad, 1);
        character.IsAirborne = true;

        Assert.True(Activate(shard, character, GliderPadAbility, GliderPad));

        Assert.Equal(0u, player.Inventory.GetResourceQuantity(GliderPad));
        Assert.True(spawn.Executed);
    }

    [Fact]
    public void Return_InsideNestedLogicChain_EndsTheWholeAbility()
    {
        var (shard, factory, character, _) = CreateRuntime();
        using var abilities = RegisterAbility(77, 770);
        var reached = new ProbeCommand();
        factory.Chains[771] = Commands(new ReturnCommand(new ReturnCommandDef { Id = 1 }));
        factory.Chains[772] = Commands(new LogicAndChainCommand(new LogicAndChainCommandDef { Id = 2, AndChain = 771 }));
        factory.Chains[770] = Commands(
            new ConditionalBranchCommand(new ConditionalBranchCommandDef { Id = 3, IfChain = 772, ThenChain = 0, ElseChain = 0 }),
            reached);

        // The branch's if-chain hit a Return two levels down: the branch, then the root, stop there.
        Assert.True(shard.Abilities.HandleActivateAbility(shard, character, 77, shard.CurrentTime, new AptitudeTargets()));
        Assert.False(reached.Executed);

        // The flag does not leak into the next activation on a fresh context.
        Assert.True(shard.Abilities.HandleActivateAbility(shard, character, 77, shard.CurrentTime, new AptitudeTargets()));
    }

    [Fact]
    public void Return_InCalledAbility_EndsOnlyTheCalledChain()
    {
        var (shard, factory, character, _) = CreateRuntime();
        using var abilities = RegisterAbility(78, 780, 79, 790);
        var afterCall = new ProbeCommand();
        factory.Chains[790] = Commands(new ReturnCommand(new ReturnCommandDef { Id = 1 }), new ProbeCommand());
        factory.Chains[780] = Commands(new CallCommand(new CallCommandDef { Id = 2, AbilityId = 79 }), afterCall);

        Assert.True(shard.Abilities.HandleActivateAbility(shard, character, 78, shard.CurrentTime, new AptitudeTargets()));
        Assert.True(afterCall.Executed);
    }

    [Theory]
    [InlineData(400, 1000, 1, 0, 0, 100f, true)] // 400 < 100% of 1000
    [InlineData(1000, 1000, 1, 0, 0, 100f, false)] // full: not < 100%
    [InlineData(1000, 1000, 1, 0, 1, 100f, true)] // <= passes at full
    [InlineData(250, 1000, 0, 1, 0, 25f, false)] // 250 > 25% of 1000: no (equal)
    [InlineData(250, 1000, 0, 1, 1, 25f, true)] // >= 25%: yes
    public void StatRequirement_ComparesHealthAgainstPercentOfMax(int health, int max, byte lt, byte gt, byte eq, float value, bool expected)
    {
        var (shard, _, character, _) = CreateRuntime();
        character.SetMaxHealth(max, resetCurrent: true);
        character.SetCurrentHealth(health);

        var command = new StatRequirementCommand(new StatRequirementCommandDef
        {
            Id = 1, Stat1 = 6, Stat2 = 7, Lessthan = lt, Greaterthan = gt, Equalto = eq, Value = value,
        });

        Assert.Equal(expected, command.Execute(new Context(shard, character)));
    }

    [Fact]
    public void StatRequirement_AbsoluteValue_WhenNoSecondStat()
    {
        var (shard, _, character, _) = CreateRuntime();
        character.SetMaxHealth(1000, resetCurrent: true);
        character.SetCurrentHealth(30);

        var above = new StatRequirementCommand(new StatRequirementCommandDef { Id = 1, Stat1 = 6, Greaterthan = 1, Value = 25f });
        var below = new StatRequirementCommand(new StatRequirementCommandDef { Id = 2, Stat1 = 6, Lessthan = 1, Value = 25f });

        Assert.True(above.Execute(new Context(shard, character)));
        Assert.False(below.Execute(new Context(shard, character)));
    }

    [Fact]
    public void LoadRegisterFromStat_ReadsTheLiveHealthPool()
    {
        var (shard, _, character, _) = CreateRuntime();
        character.SetMaxHealth(1000, resetCurrent: true);
        character.SetCurrentHealth(640);

        var maxHealth = new LoadRegisterFromStatCommand(new LoadRegisterFromStatCommandDef { Id = 1, Stat = 7, Regop = 0 });
        var health = new LoadRegisterFromStatCommand(new LoadRegisterFromStatCommandDef { Id = 2, Stat = 6, Regop = 0 });

        var context = new Context(shard, character);
        maxHealth.Execute(context);
        Assert.Equal(1000f, context.Register);
        health.Execute(context);
        Assert.Equal(640f, context.Register);
    }

    private static bool Activate(FakeShard shard, CharacterEntity character, uint abilityId, uint itemSdbId) =>
        shard.Abilities.HandleActivateAbility(shard, character, abilityId, shard.CurrentTime, new AptitudeTargets(), abilityModuleId: itemSdbId);

    private static Chain HealthPackChainFor(FakeAptitudeFactory factory)
    {
        factory.Effects[HealEffect] = new Effect
        {
            Data = new StatusEffectData { Id = HealEffect, MaxStackCount = 8, UpdateFrequency = 250 },
            ApplyChain = Commands(
                new TargetClearCommand(new TargetClearCommandDef { Id = 1365386, Current = 1 }),
                new TargetSelfCommand(new TargetSelfCommandDef { Id = 1365385 }),
                new SetRegisterCommand(new SetRegisterCommandDef { Id = 1365380, RegisterVal = 0.2f, Regop = 0 }),
                new LoadRegisterFromStatCommand(new LoadRegisterFromStatCommandDef { Id = 1365379, Stat = 7, Regop = 2 }),
                new HealRegisterCommand()),
            DurationChain = Commands(
                new TimeDurationCommand(new TimeDurationCommandDef { Id = 1365388, DurationMs = 500 }),
                new StatRequirementCommand(new StatRequirementCommandDef { Id = 1365387, Stat1 = 6, Stat2 = 7, Lessthan = 1, Value = 100f })),
        };

        return Commands(
            new TimeCooldownCommand(new TimeCooldownCommandDef { Id = 680631, Category = 9, CheckCategory = 1, CheckGlobal = 1, CheckLocal = 1 }),
            new StatRequirementCommand(new StatRequirementCommandDef { Id = 680630, Stat1 = 6, Stat2 = 7, Lessthan = 1, Value = 100f }),
            new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 680629 }),
            new ImpactApplyEffectCommand(new ImpactApplyEffectCommandDef { Id = 680628, EffectId = HealEffect, ApplyToSelf = 1 }),
            new InstantActivationCommand(new InstantActivationCommandDef { Id = 680627, Category = 9, LocalCooldown = 60000, CategoryCooldown = 60000, GlobalCooldown = 200 }));
    }

    /// <returns>The probe standing in for DeployableSpawn; the pad was spent iff it executed.</returns>
    private static ProbeCommand GliderPadChainsFor(FakeAptitudeFactory factory)
    {
        var spawn = new ProbeCommand();
        factory.Effects[NotifyEffect] = new Effect
        {
            Data = new StatusEffectData { Id = NotifyEffect, MaxStackCount = 1, UpdateFrequency = 100 },
            DurationChain = Commands(new TimeDurationCommand(new TimeDurationCommandDef { Id = 1139580, DurationMs = 3000 })),
        };

        factory.Chains[1147309] = Commands(new AirborneDurationCommand(new AirborneDurationCommandDef { Id = 1147309 }));
        factory.Chains[1147312] = Commands(
            new ImpactApplyEffectCommand(new ImpactApplyEffectCommandDef { Id = 1147312, EffectId = NotifyEffect, ApplyToSelf = 1 }),
            new InstantActivationCommand(new InstantActivationCommandDef { Id = 1147311 }),
            new ReturnCommand(new ReturnCommandDef { Id = 1147310 }));
        factory.Chains[GliderPadChain] = Commands(
            new TimeCooldownCommand(new TimeCooldownCommandDef { Id = 1147320, Category = 13, CheckCategory = 1, CheckGlobal = 1, CheckLocal = 1 }),
            new ConditionalBranchCommand(new ConditionalBranchCommandDef { Id = 1147319, IfChain = 1147309, ThenChain = 0, ElseChain = 1147312 }),
            new ConsumeItemCommand(new ConsumeItemCommandDef { Id = 1147318 }),
            spawn);
        return spawn;
    }

    private static (FakeShard Shard, FakeAptitudeFactory Factory, CharacterEntity Character, FakeNetworkPlayer Player) CreateRuntime()
    {
        var shard = new FakeShard { CurrentTimeLong = 60_000 };
        var factory = new FakeAptitudeFactory(shard);
        shard.Abilities = new AbilitySystem(shard, factory);
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);
        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        character.SetControllingPlayer(player);
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Living, 0);

        // Partial updates stay off, so the mutations never touch a (nonexistent) network channel.
        player.Inventory = new CharacterInventory(shard, null, character);
        return (shard, factory, character, player);
    }

    /// <summary>
    ///     Points <see cref="SDBInterface" />'s ability table at the given ability/chain pairs, the way the
    ///     health tests swap their level-attribute table, and restores it on dispose. The chains themselves
    ///     come from the fake factory, so no client database is needed.
    /// </summary>
    private static IDisposable RegisterAbility(params uint[] abilityAndChainPairs)
    {
        var field = typeof(SDBInterface).GetField("_abilityData", BindingFlags.NonPublic | BindingFlags.Static);
        var original = (Dictionary<uint, AbilityData>)field.GetValue(null);
        var table = original == null ? new Dictionary<uint, AbilityData>() : new Dictionary<uint, AbilityData>(original);
        for (int i = 0; i + 1 < abilityAndChainPairs.Length; i += 2)
        {
            table[abilityAndChainPairs[i]] = new AbilityData { Id = abilityAndChainPairs[i], Chain = abilityAndChainPairs[i + 1] };
        }

        field.SetValue(null, table);
        return new RestoreOnDispose(() => field.SetValue(null, original));
    }

    private static Chain Commands(params ICommand[] commands) => new() { Commands = [.. commands] };

    private sealed class RestoreOnDispose(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// <summary>
    ///     Stands in for the effect's <c>HealDamage(healpoints 1 x register)</c>: heals Self by the register.
    ///     The production command resolves hostility through the faction tables of the static database,
    ///     which a unit test does not have; the register arithmetic it multiplies is what this test checks.
    /// </summary>
    private sealed class HealRegisterCommand : ICommand
    {
        public uint Id { get; set; }

        public bool Execute(Context context)
        {
            context.Shard.Damage.ApplyHeal(context.Self as CharacterEntity, (int)MathF.Round(context.Register));
            return true;
        }
    }

    private sealed class ProbeCommand(Action onExecute = null, bool result = true) : ICommand
    {
        public uint Id { get; set; }

        public bool Executed { get; private set; }

        public bool Execute(Context context)
        {
            Executed = true;
            onExecute?.Invoke();
            return result;
        }
    }
}
