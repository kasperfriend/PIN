using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Duration;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The duration hand-off of a charge-up weapon's ability chain: the activation carries the weapon's
///     charge time, and the effects the chain applies have to live for it (their own duration chain is a
///     replenishable duration reading the register). The command's definition table is server-only, so
///     these tests are what the build knows about the command's behaviour.
/// </summary>
public class ReplenishEffectDurationCommandTests
{
    [Fact]
    public void Execute_RecordsTheDurationForTheEffectsStillToCome()
    {
        // NPC Charge Up and Channel Fire: apply the charge effect, then replenish its duration - and the
        // charge effect is applied after the command one other weapon (Phason Thrower) has it the other
        // way round, so the value has to be recorded on the activation itself.
        var shard = new FakeShard { CurrentTimeLong = 0 };
        var command = new ReplenishEffectDurationCommand(new ReplenishEffectDurationCommandDef { Id = 1_198_933 });
        var context = new Context(shard, CreateCharacter(shard)) { Register = 2f };

        Assert.True(command.Execute(context));
        Assert.Equal(2f, context.AppliedEffectDuration);

        // The value the effects' own duration chains read is the same one: CopyContext carries it into
        // the contexts DoApplyEffect builds.
        Assert.Equal(2f, Context.CopyContext(context).AppliedEffectDuration);
    }

    [Fact]
    public void Execute_HandsItToTheEffectsTheActivationAlreadyApplied()
    {
        var shard = new FakeShard { CurrentTimeLong = 0 };
        var character = CreateCharacter(shard);
        var command = new ReplenishEffectDurationCommand(new ReplenishEffectDurationCommandDef { Id = 1_038_402 });

        var effectContext = new Context(shard, character);
        var context = new Context(shard, character) { Register = 2f };
        context.AppliedEffects = [new AppliedEffectRecord(character, new EffectState { Context = effectContext })];

        Assert.True(command.Execute(context));

        // 2,000 ms of charge: the duration command reads the small value as seconds.
        Assert.Equal(2f, effectContext.Register);
    }

    [Fact]
    public void Execute_WithoutARegister_ChangesNothing()
    {
        // A player activation has no register (the client sends no value), and neither has a weapon with
        // no charge time: the effects keep their own chains' default.
        var shard = new FakeShard { CurrentTimeLong = 0 };
        var character = CreateCharacter(shard);
        var command = new ReplenishEffectDurationCommand(new ReplenishEffectDurationCommandDef { Id = 1_038_402 });

        var effectContext = new Context(shard, character);
        var context = new Context(shard, character);
        context.AppliedEffects = [new AppliedEffectRecord(character, new EffectState { Context = effectContext })];

        Assert.True(command.Execute(context));

        Assert.True(float.IsNaN(context.AppliedEffectDuration));
        Assert.True(float.IsNaN(effectContext.Register));
    }

    [Fact]
    public void Execute_WithoutAppliedEffects_IsANoOp()
    {
        // The command sits in a chain that applies nothing before it (the Phason Thrower's order) and in
        // activations that carry no effect list at all.
        var shard = new FakeShard { CurrentTimeLong = 0 };
        var command = new ReplenishEffectDurationCommand(new ReplenishEffectDurationCommandDef { Id = 1_218_017 });
        var context = new Context(shard, CreateCharacter(shard)) { Register = 4f, AppliedEffects = null };

        Assert.True(command.Execute(context));
        Assert.Equal(4f, context.AppliedEffectDuration);
    }

    private static CharacterEntity CreateCharacter(FakeShard shard)
    {
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities[character.EntityId] = character;
        return character;
    }
}
