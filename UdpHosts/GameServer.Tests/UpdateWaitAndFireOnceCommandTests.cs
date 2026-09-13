using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Duration;
using GameServer.Systems.Aptitude.Commands.Update;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for <see cref="UpdateWaitAndFireOnceCommand" />, the delayed one-shot an effect's update loop uses
///     to fire a chain once after a wait. Until the command was implemented the wait was a no-op, so every
///     instance was dead data - including NPC attack stages whose projectile, damage and force push live behind
///     the wait (monster 898's ability 35803 fires its projectile 2000 ms after the wind-up effect animates).
/// </summary>
public class UpdateWaitAndFireOnceCommandTests
{
    [Fact]
    public void UpdateLoop_FiresTheCarriedChainOnceAfterTheWait()
    {
        var (shard, factory, character) = CreateRuntime(10_000);
        var marker = new MarkerCommand();
        InstallEffect(factory, 4313, wait: 2000, duration: 8000, updateFrequency: 500, marker: marker);

        Assert.True(shard.Abilities.DoApplyEffect(4313, character, new Context(shard, character) { InitTime = 10_000 }));

        // The effect's first update tick, one second in: the loop runs, but the wait is not over yet.
        Tick(shard, character, 11_100);
        Assert.Equal(0, marker.Executions);

        // Past the wait on the next tick that is due.
        Tick(shard, character, 12_500);
        Assert.Equal(1, marker.Executions);

        // Later ticks of the same application must not fire it again, even though the effect is still alive.
        Tick(shard, character, 13_500);
        Tick(shard, character, 14_500);
        Assert.Equal(1, marker.Executions);
        Assert.NotNull(Active(character, 4313));
    }

    [Fact]
    public void UpdateLoop_MeasuresTheWaitFromTheEffectsOwnStart()
    {
        var (shard, factory, character) = CreateRuntime(10_000);
        var marker = new MarkerCommand();
        InstallEffect(factory, 4561, wait: 2000, duration: 8000, updateFrequency: 500, marker: marker);

        // The activation carries a client-predicted time 1000 ms ahead of the server tick that applies the
        // effect. The effect starts when it is applied, so its wait is over at 12_500; measuring it from the
        // predicted timestamp would leave it waiting (2,500 - 1,000 < 2,000).
        Assert.True(shard.Abilities.DoApplyEffect(4561, character, new Context(shard, character) { InitTime = 11_000 }));

        Tick(shard, character, 12_500);
        Assert.Equal(1, marker.Executions);
    }

    [Fact]
    public void Execute_FiresWhenTheElapsedTimeReachesTheWait()
    {
        // The database authors waits that end exactly on an effect tick (effect 10162 waits 20000 ms inside a
        // 20000 ms effect), so the comparison is inclusive: a wait that is up now fires now instead of waiting
        // for the next millisecond.
        var (shard, marker, command, context) = CreateCommandAt(43, wait: 2000, result: true, startTime: 10_000);

        shard.CurrentTimeLong = 11_999;
        Assert.True(command.Execute(context));
        Assert.Equal(0, marker.Executions);

        shard.CurrentTimeLong = 12_000;
        Assert.True(command.Execute(context));
        Assert.Equal(1, marker.Executions);

        // And only once: the wait does not re-arm.
        shard.CurrentTimeLong = 20_000;
        Assert.True(command.Execute(context));
        Assert.Equal(1, marker.Executions);
    }

    [Fact]
    public void Execute_DoesNotRetryAFailedChain()
    {
        var (shard, marker, command, context) = CreateCommandAt(44, wait: 1000, result: false, startTime: 10_000);

        shard.CurrentTimeLong = 10_500;
        Assert.True(command.Execute(context));
        Assert.Equal(0, marker.Executions);

        // The carried chain reports failure (its own requirement is not met), which is passed on...
        shard.CurrentTimeLong = 11_000;
        Assert.False(command.Execute(context));
        Assert.Equal(1, marker.Executions);

        // ...but the command is a one-shot, not a retry loop: the next tick does not run the chain again.
        shard.CurrentTimeLong = 12_000;
        Assert.True(command.Execute(context));
        Assert.Equal(1, marker.Executions);
    }

    [Fact]
    public void Execute_WithoutAChain_IsANoOp()
    {
        var shard = new FakeShard { CurrentTimeLong = 10_000 };
        var command = new UpdateWaitAndFireOnceCommand(new UpdateWaitAndFireOnceCommandDef { Id = 1, Chain = 0, Duration = 50 });
        var context = new Context(shard, CreateCharacter(shard)) { EffectStartTime = 10_000 };

        shard.CurrentTimeLong = 20_000;
        Assert.True(command.Execute(context));
    }

    [Fact]
    public void UpdateLoop_TracksEachApplicationSeparately()
    {
        var (shard, factory, character) = CreateRuntime(10_000);
        var second = CreateCharacter(shard);
        var marker = new MarkerCommand();
        InstallEffect(factory, 7533, wait: 2000, duration: 8000, updateFrequency: 500, marker: marker);

        Assert.True(shard.Abilities.DoApplyEffect(7533, character, new Context(shard, character) { InitTime = 10_000 }));
        Tick(shard, character, 12_500);
        Assert.Equal(1, marker.Executions);

        // A second application on another target starts its own wait. The shared command instance must not
        // treat it as already fired.
        Assert.True(shard.Abilities.DoApplyEffect(7533, second, new Context(shard, second) { InitTime = 12_500 }));
        Tick(shard, second, 13_500);
        Assert.Equal(1, marker.Executions);

        Tick(shard, second, 14_600);
        Assert.Equal(2, marker.Executions);
    }

    private static (FakeShard Shard, MarkerCommand Marker, UpdateWaitAndFireOnceCommand Command, Context Context) CreateCommandAt(uint chainId, uint wait, bool result, uint startTime)
    {
        var shard = new FakeShard { CurrentTimeLong = startTime };
        var factory = new FakeAptitudeFactory(shard);
        shard.Abilities = new AbilitySystem(shard, factory);
        var marker = new MarkerCommand { Result = result };
        factory.Chains[chainId] = Commands(marker);

        var command = new UpdateWaitAndFireOnceCommand(new UpdateWaitAndFireOnceCommandDef { Id = chainId * 10, Chain = chainId, Duration = wait });
        var context = new Context(shard, CreateCharacter(shard)) { EffectStartTime = startTime };
        return (shard, marker, command, context);
    }

    private static (FakeShard Shard, FakeAptitudeFactory Factory, CharacterEntity Character) CreateRuntime(ulong time)
    {
        var shard = new FakeShard { CurrentTimeLong = time };
        var factory = new FakeAptitudeFactory(shard);
        shard.Abilities = new AbilitySystem(shard, factory);
        var character = CreateCharacter(shard);
        return (shard, factory, character);
    }

    private static CharacterEntity CreateCharacter(FakeShard shard)
    {
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);
        return character;
    }

    private static void Tick(FakeShard shard, CharacterEntity character, ulong time)
    {
        shard.CurrentTimeLong = time;
        shard.Abilities.ProcessTarget(character, time);
    }

    private static EffectState Active(CharacterEntity character, uint id)
    {
        foreach (var state in character.GetActiveEffects())
        {
            if (state?.Effect.Id == id)
            {
                return state;
            }
        }

        return null;
    }

    /// <summary>
    ///     Installs the shape of a real row: an effect whose update loop waits and then fires a chain, with a
    ///     duration chain that outlives the wait, exactly like the rows do (effect 4313 waits 2000 ms of its
    ///     2200 ms, effect 6940 waits 5000 ms of 8000 ms). The lifetime here is longer so that the tests can
    ///     tick past the fire and show that the wait does not re-arm.
    /// </summary>
    private static void InstallEffect(FakeAptitudeFactory factory, uint id, uint wait, uint duration, uint updateFrequency, MarkerCommand marker)
    {
        factory.Chains[id + 100] = Commands(marker);
        factory.Effects[id] = new Effect
        {
            Data = new StatusEffectData { Id = id, MaxStackCount = 1, UpdateFrequency = updateFrequency },
            DurationChain = Commands(new TimeDurationCommand(new TimeDurationCommandDef { DurationMs = duration })),
            UpdateChain = Commands(new UpdateWaitAndFireOnceCommand(new UpdateWaitAndFireOnceCommandDef { Id = id * 10, Chain = id + 100, Duration = wait })),
        };
    }

    private static Chain Commands(params ICommand[] commands) => new() { Commands = [.. commands] };

    private sealed class MarkerCommand : ICommand
    {
        public uint Id { get; set; }

        public bool Result { get; set; } = true;

        public int Executions { get; private set; }

        public bool Execute(Context context)
        {
            Executions++;
            return Result;
        }
    }
}
