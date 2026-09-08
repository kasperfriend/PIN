using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Duration;
using GameServer.Systems.Aptitude.Commands.Requirement;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     When the server commands a launch (a glider pad's <c>ForcePush</c>), the aptitude gates that keep the
///     launch effects alive run on the character's *reported* pose, which cannot exist until the client has
///     played the forced movement it was just told to play. ForcePush therefore opens a provisional window
///     (<see cref="CharacterEntity.MarkServerLaunchPending" />) during which the character counts as airborne
///     and gliding. The window is closed by the first <c>MovementInput</c> that can answer it (airborne pose,
///     or any pose after the forced movement ended), which makes the client's reported pose the truth again.
///     These tests pin down the window's effect on the two gates that tore the pad launch down in the field
///     (<c>AirborneDuration</c>, <c>RequireMovestate</c>).
/// </summary>
public class LaunchWindowTests
{
    [Fact]
    public void AirborneDuration_PassesWhileTheServerLaunchWindowIsOpen()
    {
        var (shard, character, command) = CreateRuntime(10_000);
        var context = new Context(shard, character);

        character.MarkServerLaunchPending(10_000); // forced window ends 10_550, deadline 12_050
        Assert.True(character.IsServerLaunchPending);
        Assert.True(character.IsServerLaunchForcedWindowActive);
        Assert.True(command.Execute(context));

        // After the forced window the window is still open until its deadline...
        shard.CurrentTimeLong = 11_551;
        Assert.True(character.IsServerLaunchPending);
        Assert.False(character.IsServerLaunchForcedWindowActive);
        Assert.True(command.Execute(context));

        // ... and the deadline passing ends it on its own.
        shard.CurrentTimeLong = 12_051;
        Assert.False(character.IsServerLaunchPending);
        Assert.False(command.Execute(context));

        // Closing the window with the client's handoff pose ends it too.
        character.MarkServerLaunchPending(11_000); // clock is at 12_051, deadline 13_050 still ahead
        Assert.True(command.Execute(context));
        character.ClearServerLaunchPending();
        Assert.False(command.Execute(context));
    }

    [Fact]
    public void AirborneDuration_StillUsesTheReportedAirbornePoseOutsideTheWindow()
    {
        var (shard, character, command) = CreateRuntime(10_000);
        var context = new Context(shard, character);

        // No window, but the client already reports being airborne: duration must pass.
        character.IsAirborne = true;
        Assert.True(command.Execute(context));

        // Landing pose: gone.
        character.IsAirborne = false;
        Assert.False(command.Execute(context));
    }

    [Fact]
    public void AirborneDuration_NegateFlipsInsideAndOutsideTheWindow()
    {
        var (shard, character, _) = CreateRuntime(10_000);
        var negated = new AirborneDurationCommand(new AirborneDurationCommandDef { Negate = 1 });
        var context = new Context(shard, character);

        character.IsAirborne = false;
        character.MarkServerLaunchPending(10_000);
        Assert.False(negated.Execute(context)); // launch in flight: "not airborne" must not pass

        character.ClearServerLaunchPending();
        Assert.True(negated.Execute(context)); // grounded and no window: "not airborne" passes
    }

    [Fact]
    public void RequireMovestate_LaunchWindowCountsAsGlidingUntilTheClientPoseArrives()
    {
        var (shard, character, _) = CreateRuntime(10_000);
        var gliding = new RequireMovestateCommand(new RequireMovestateCommandDef { Gliding = 1 });
        var standing = new RequireMovestateCommand(new RequireMovestateCommandDef { Standing = 1 });
        var notStanding = new RequireMovestateCommand(new RequireMovestateCommandDef { Standing = 1, Negate = 1 });
        var context = new Context(shard, character);

        character.MovementStateContainer.MovementStateValue = 0x1000; // last reported pose: standing
        character.MarkServerLaunchPending(10_000);

        // While the window is open the unconfirmed launch satisfies the gliding arm of the pad's duration
        // chain, but it must not satisfy other states.
        Assert.True(gliding.Execute(context));
        Assert.False(standing.Execute(context));
        Assert.True(notStanding.Execute(context));

        // First pose after the push: standing is the truth again.
        character.ClearServerLaunchPending();
        Assert.False(gliding.Execute(context));
        Assert.True(standing.Execute(context));
        Assert.False(notStanding.Execute(context));

        // And an airborne pose outside the window satisfies the falling/gliding arms normally.
        character.MovementStateContainer.MovementStateValue = 0x3000;
        var falling = new RequireMovestateCommand(new RequireMovestateCommandDef { Falling = 1 });
        Assert.True(falling.Execute(context));
    }

    [Fact]
    public void LaunchWindow_ExpiryAndForcedWindowAreWrapSafe()
    {
        var shard = new FakeShard { CurrentTimeLong = uint.MaxValue - 100ul };
        var character = FakeCharacterFactory.Create(shard);

        // Push just before the uint wrap: the forced window and the deadline both live across the wrap.
        character.MarkServerLaunchPending(uint.MaxValue - 100u); // forced end wraps to 449, deadline to 1949
        Assert.True(character.IsServerLaunchForcedWindowActive);
        Assert.True(character.IsServerLaunchPending);

        shard.CurrentTimeLong = 1_000; // wrapped: forced window over (deadline 1949 still ahead)
        Assert.False(character.IsServerLaunchForcedWindowActive);
        Assert.True(character.IsServerLaunchPending);

        shard.CurrentTimeLong = 2_000; // wrapped: deadline passed too
        Assert.False(character.IsServerLaunchPending);
    }

    private static (FakeShard Shard, CharacterEntity Character, AirborneDurationCommand Command) CreateRuntime(ulong time)
    {
        var shard = new FakeShard { CurrentTimeLong = time };
        var character = FakeCharacterFactory.Create(shard);
        return (shard, character, new AirborneDurationCommand(new AirborneDurationCommandDef()));
    }
}
