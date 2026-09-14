using GameServer.Controllers.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Register;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class LoadRegisterFromLevelCommandTests
{
    [Fact]
    public void Execute_LoadsTheCharactersLevelWhenTheControllerIsAvailable()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid())
        {
            Character_BaseController = new BaseController { LevelProp = 25 },
        };
        var context = new Context(shard, character);
        var command = new LoadRegisterFromLevelCommand(new LoadRegisterFromLevelCommandDef
        {
            Id = 37408,
            Regop = 0,
        });

        Assert.True(command.Execute(context));
        Assert.Equal(25f, context.Register);
    }

    [Fact]
    public void Execute_FailsSafelyWhenTheCharacterControllerHasNotBeenCreated()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid());
        var context = new Context(shard, character) { Register = 12f };
        var command = new LoadRegisterFromLevelCommand(new LoadRegisterFromLevelCommandDef
        {
            Id = 37408,
            Regop = 0,
        });

        Assert.False(command.Execute(context));
        Assert.Equal(12f, context.Register);
    }

    [Fact]
    public void Execute_FailsSafelyWhenTheInitiatorIsUnavailable()
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid());
        var context = new Context(shard, character)
        {
            Initiator = null,
            Register = 7f,
        };
        var command = new LoadRegisterFromLevelCommand(new LoadRegisterFromLevelCommandDef
        {
            Id = 37408,
            FromInitiator = 1,
            Regop = 0,
        });

        Assert.False(command.Execute(context));
        Assert.Equal(7f, context.Register);
    }
}
