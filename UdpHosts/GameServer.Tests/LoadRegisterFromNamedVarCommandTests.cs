using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Register;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     <c>apt::LoadRegisterFromNamedVarCommandDef</c> (type 240) loads the value of a named variable into the
///     aptitude register. The variables live in a per-entity store on the ability system, written by
///     <c>NamedVariableAssign</c> (type 239); a row whose variable nothing has assigned takes the row's
///     <c>undecl_value</c> fallback. The shared glider pad launch ability (chain 1001671, row 1001663) depends
///     on that fallback of 1.0: the register comparisons in the launch effects that follow select the pad's
///     effect level through it, and no chain in the pad's activation assigns "WingFX".
/// </summary>
public class LoadRegisterFromNamedVarCommandTests
{
    [Fact]
    public void LoadsTheUndeclaredVariableFallbackIntoTheRegister()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        var context = new Context(shard, character);

        var command = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 1001663,
            UndeclValue = 1.0f,
            Regop = 0, // ASSIGN: the register takes the fallback value.
        });

        Assert.True(command.Execute(context));
        Assert.Equal(1.0f, context.Register);
    }

    [Fact]
    public void CombinesTheFallbackWithTheCurrentRegisterThroughTheRegop()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        var context = new Context(shard, character);
        context.Register = 3.0f;

        var command = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 1001663,
            UndeclValue = 2.0f,
            Regop = 1, // ADD.
        });

        Assert.True(command.Execute(context));
        Assert.Equal(5.0f, context.Register);
    }

    [Fact]
    public void DoesNotFailAChainWhenTheRegisterWasNeverSet()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        // The register defaults to NaN; loading into it must not poison the rest of the chain.
        var context = new Context(shard, character);

        var command = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 1001663,
            UndeclValue = 1.0f,
            Regop = 1, // ADD onto NaN behaves like a plain assignment.
        });

        Assert.True(command.Execute(context));
        Assert.Equal(1.0f, context.Register);
    }

    [Fact]
    public void ReadsBackWhatNamedVariableAssignStoredOnTheSameEntity()
    {
        var shard = new FakeShard();
        shard.Abilities = new AbilitySystem(shard);
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        // The pair that was broken: nothing ever declared a variable, so this read could only ever
        // take its fallback and a chain that set a variable and branched on it always took the unset
        // branch.
        var assign = new NamedVariableAssignCommand(new NamedVariableAssignCommandDef
        {
            Id = 1,
            NameId = 19,
            MemberName = "WingFX",
            Value = 3.0f,
            Regop = 0, // ASSIGN.
        });
        Assert.True(assign.Execute(new Context(shard, character)));

        var context = new Context(shard, character);
        var read = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 2,
            NameId = 19,
            MemberName = "WingFX",
            UndeclValue = 1.0f,
            Regop = 0,
        });

        Assert.True(read.Execute(context));
        Assert.Equal(3.0f, context.Register); // the stored value, not the 1.0 fallback
    }

    [Fact]
    public void AccumulatesThroughTheAddRegopOnTheStoredValue()
    {
        var shard = new FakeShard();
        shard.Abilities = new AbilitySystem(shard);
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        foreach (var value in new[] { 3.0f, 2.0f })
        {
            var assign = new NamedVariableAssignCommand(new NamedVariableAssignCommandDef
            {
                Id = 1,
                NameId = 19,
                Value = value,
                Regop = value == 3.0f ? (byte)0 : (byte)1, // first write ASSIGNs, second ADDs.
            });
            Assert.True(assign.Execute(new Context(shard, character)));
        }

        var context = new Context(shard, character);
        var read = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 2,
            NameId = 19,
            UndeclValue = 1.0f,
            Regop = 0,
        });

        Assert.True(read.Execute(context));
        Assert.Equal(5.0f, context.Register);
    }

    [Fact]
    public void FirstWriteStoresTheRowsValueWhateverTheRegop()
    {
        var shard = new FakeShard();
        shard.Abilities = new AbilitySystem(shard);
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        // ADD against a variable that was never declared must not become an accumulation: the result
        // would otherwise depend on how many times the chain had run.
        var assign = new NamedVariableAssignCommand(new NamedVariableAssignCommandDef
        {
            Id = 1,
            NameId = 21,
            Value = 4.0f,
            Regop = 1, // ADD.
        });

        Assert.True(assign.Execute(new Context(shard, character)));
        Assert.True(assign.Execute(new Context(shard, character)));

        var context = new Context(shard, character);
        var read = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 2,
            NameId = 21,
            UndeclValue = 1.0f,
            Regop = 0,
        });

        Assert.True(read.Execute(context));
        Assert.Equal(8.0f, context.Register); // 4.0 then +4.0
    }

    [Fact]
    public void TheStoreIsPerEntitySoAnotherEntityStillTakesTheFallback()
    {
        var shard = new FakeShard();
        shard.Abilities = new AbilitySystem(shard);
        var caster = new CharacterEntity(shard, shard.GetNextGuid(0));
        var bystander = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(caster.EntityId, caster);
        shard.Entities.Add(bystander.EntityId, bystander);

        var assign = new NamedVariableAssignCommand(new NamedVariableAssignCommandDef
        {
            Id = 1,
            NameId = 19,
            Value = 3.0f,
            Regop = 0,
        });
        Assert.True(assign.Execute(new Context(shard, caster)));

        var context = new Context(shard, bystander);
        var read = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 2,
            NameId = 19,
            UndeclValue = 1.0f,
            Regop = 0,
        });

        Assert.True(read.Execute(context));
        Assert.Equal(1.0f, context.Register);
    }

    [Fact]
    public void StillWorksWhenTheShardHasNoAbilitySystem()
    {
        var shard = new FakeShard(); // Abilities stays null.
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        shard.Entities.Add(character.EntityId, character);

        var context = new Context(shard, character);
        var read = new LoadRegisterFromNamedVarCommand(new LoadRegisterFromNamedVarCommandDef
        {
            Id = 1001663,
            NameId = 19,
            MemberName = "WingFX",
            UndeclValue = 1.0f,
            Regop = 0,
        });

        Assert.True(read.Execute(context));
        Assert.Equal(1.0f, context.Register);
    }
}
