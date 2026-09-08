using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Requirement;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     <c>aptfs::RequireAimModeCommandDef</c> (command type 130) gates chains on the character of the
///     activation aiming down sights. On PIN the scoped state is <c>FireMode_1</c> (mode 1 while scoped,
///     0 in hip fire, driven by <c>UseScope</c>); <c>FireMode_0</c> is the selected main/underbarrel fire
///     mode and must not satisfy it.
/// </summary>
public class RequireAimModeCommandTests
{
    [Fact]
    public void RequireAimMode_ScopedCharacterPassesAndHipFireFails()
    {
        var (shard, character) = CreateRuntime();
        var command = new RequireAimModeCommand(new RequireAimModeCommandDef());

        character.SetFireMode(1, new FireModeData { Mode = 1, Time = 100 });
        Assert.True(command.Execute(new Context(shard, character)));

        character.SetFireMode(1, new FireModeData { Mode = 0, Time = 200 });
        Assert.False(command.Execute(new Context(shard, character)));
    }

    [Fact]
    public void RequireAimMode_IgnoresTheSelectedFireMode()
    {
        var (shard, character) = CreateRuntime();
        var command = new RequireAimModeCommand(new RequireAimModeCommandDef());

        // Scoped on the underbarrel selection: still scoped, still passes.
        character.SetFireMode(0, new FireModeData { Mode = 1, Time = 100 });
        character.SetFireMode(1, new FireModeData { Mode = 1, Time = 100 });
        Assert.True(command.Execute(new Context(shard, character)));

        // Hip fire while the underbarrel is selected must not pass.
        character.SetFireMode(1, new FireModeData { Mode = 0, Time = 200 });
        Assert.False(command.Execute(new Context(shard, character)));
    }

    [Fact]
    public void RequireAimMode_NegateFlipsTheResult()
    {
        var (shard, character) = CreateRuntime();
        var negated = new RequireAimModeCommand(new RequireAimModeCommandDef { Negate = 1 });

        character.SetFireMode(1, new FireModeData { Mode = 1, Time = 100 });
        Assert.False(negated.Execute(new Context(shard, character)));

        character.SetFireMode(1, new FireModeData { Mode = 0, Time = 200 });
        Assert.True(negated.Execute(new Context(shard, character)));
    }

    [Fact]
    public void RequireAimMode_ChainWithoutAnyCharacterIsNotApplicableAndPasses()
    {
        var shard = new FakeShard();
        var command = new RequireAimModeCommand(new RequireAimModeCommandDef());

        // Deployable owned chains (no owner, no character among the targets) cannot answer an aim-mode
        // question; like the other character requirements, "not applicable" is a pass, not a failure.
        var context = new Context(shard, new DeployableEntity(shard, shard.GetNextGuid(), type: 395, abilitySrcId: 0));
        Assert.True(command.Execute(context));
    }

    private static (FakeShard Shard, CharacterEntity Character) CreateRuntime()
    {
        var shard = new FakeShard();
        return (shard, FakeCharacterFactory.Create(shard));
    }
}
