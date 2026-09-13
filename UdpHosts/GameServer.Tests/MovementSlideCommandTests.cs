using System.Numerics;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Movement;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     <c>aptfs::MovementSlideCommandDef</c> is the database's displacement: the dodge pair's 5 m sidestep over
///     667 ms (effects 1247/1282, applied by abilities 33812/33833), the Move Then Fire module's 20 m lunge
///     toward its target over 1000 ms (ability 36817), and ability 39066's 20 m rise over 2400 ms. The
///     animation of all three was already replicated; nothing moved the mob, because the command was a
///     placeholder. These tests cover the motion itself: the axes the row's offsets are measured in, the
///     duration (including a row that derives it from <c>fixed_speed</c>), the exact endpoint, and the cases
///     where the server must not move the character at all.
/// </summary>
public class MovementSlideCommandTests
{
    private const float Tolerance = 0.01f;

    [Fact]
    public void TheDodgeSidestep_MovesFiveMetresInSixHundredSixtySevenMilliseconds()
    {
        var (shard, character) = CreateRuntime(60_000);
        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 358560, OffsetX = 5f, MoveDuration = 667 });
        var context = new Context(shard, character);

        Assert.True(command.Execute(context));

        // The command starts the motion; the move itself happens on the ability sweep.
        Assert.True(shard.Abilities.IsMovementSliding(character.EntityId));
        AssertClose(Vector3.Zero, character.Position);

        // Halfway through the row's 667 ms: half the 5 m, and only along the character's own right axis
        // (the character faces +Y, so its right is +X).
        Tick(shard, character, 60_333);
        AssertClose(new Vector3(2.4978f, 0f, 0f), character.Position);

        // The endpoint lands exactly on the row's offset, even though the slide ends between ticks.
        Tick(shard, character, 60_700);
        AssertClose(new Vector3(5f, 0f, 0f), character.Position);
        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));

        // And it stays there.
        Tick(shard, character, 61_000);
        AssertClose(new Vector3(5f, 0f, 0f), character.Position);
    }

    [Fact]
    public void TheOtherDodgeDirection_MovesTheOtherWay()
    {
        var (shard, character) = CreateRuntime(60_000);
        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 794956, OffsetX = -5f, MoveDuration = 667 });

        Assert.True(command.Execute(new Context(shard, character)));
        Tick(shard, character, 60_700);

        AssertClose(new Vector3(-5f, 0f, 0f), character.Position);
    }

    [Fact]
    public void TheOffsetsAreTheCharactersOwnAxes()
    {
        // A character facing +X: its local +X (right) is -Y, exactly as CharacterEntity's own muzzle and aim
        // maths rotate a local offset. A row that says "5 m to the right" sidesteps to -Y for this character.
        var (shard, character) = CreateRuntime(60_000);
        character.SetOrientation(AiVectors.OrientationFacing(new Vector3(1f, 0f, 0f)));
        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 1, OffsetX = 5f, MoveDuration = 500 });

        Assert.True(command.Execute(new Context(shard, character)));
        Tick(shard, character, 60_500);

        AssertClose(new Vector3(0f, -5f, 0f), character.Position);
    }

    [Fact]
    public void TheLunge_HeadsTowardTheCurrentTarget()
    {
        // Move Then Fire (ability 36817, module 86132): the row states offset_y 20 over 1000 ms with
        // offset_target, and the effect's own apply chain targets what it is about to charge into.
        var (shard, character) = CreateRuntime(60_000);
        var target = AddCharacter(shard);
        target.SetPosition(new Vector3(30f, 0f, 0f));

        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 852780, OffsetY = 20f, MoveDuration = 1000, OffsetTarget = 1 });
        var context = new Context(shard, character);
        context.Targets.Push(target);

        Assert.True(command.Execute(context));
        Tick(shard, character, 61_000);

        AssertClose(new Vector3(20f, 0f, 0f), character.Position);
    }

    [Fact]
    public void TheLungeWithoutATarget_FollowsTheAim()
    {
        // The else branch of the same effect: offset_y 20 over 1000 ms with along_velocity and offset_aim.
        var (shard, character) = CreateRuntime(60_000);
        character.AimDirection = new Vector3(0f, 1f, 0f);
        var command = new MovementSlideCommand(new MovementSlideCommandDef
        {
            Id = 852781,
            OffsetY = 20f,
            MoveDuration = 1000,
            AlongVelocity = 1,
            OffsetAim = 1,
        });

        Assert.True(command.Execute(new Context(shard, character)));
        Tick(shard, character, 61_000);

        AssertClose(new Vector3(0f, 20f, 0f), character.Position);
    }

    [Fact]
    public void ARise_UsesTheUpAxis()
    {
        // Ability 39066: offset_z 20 over 2400 ms - the caster lifts off the ground, and the same effect's
        // update loop drifts another 0.1 m upwards every 600 ms.
        var (shard, character) = CreateRuntime(60_000);
        var rise = new MovementSlideCommand(new MovementSlideCommandDef { Id = 1129559, OffsetZ = 20f, MoveDuration = 2400 });
        var drift = new MovementSlideCommand(new MovementSlideCommandDef { Id = 1129554, OffsetZ = 0.1f, MoveDuration = 600 });

        Assert.True(rise.Execute(new Context(shard, character)));
        Tick(shard, character, 61_200);
        AssertClose(new Vector3(0f, 0f, 10f), character.Position);

        Tick(shard, character, 62_400);
        AssertClose(new Vector3(0f, 0f, 20f), character.Position);

        // The drift starts from wherever the caster is now, so it is a fresh 600 ms slide of 0.1 m.
        Assert.True(drift.Execute(new Context(shard, character)));
        Tick(shard, character, 63_000);
        AssertClose(new Vector3(0f, 0f, 20.1f), character.Position);
    }

    [Fact]
    public void ARegisterCanScaleTheOffset()
    {
        // offset_regop drives the whole offset vector, like a ForcePush's strength: row 1396995 states
        // offset_x 2 with offset_regop 2 (MULTIPLY), so a register of 3 pushes the caster 6 m.
        var (shard, character) = CreateRuntime(60_000);
        var command = new MovementSlideCommand(new MovementSlideCommandDef
        {
            Id = 1396995,
            OffsetX = 2f,
            MoveDuration = 950,
            OffsetRegop = 2,
        });

        Assert.True(command.Execute(new Context(shard, character) { Register = 3f }));
        Tick(shard, character, 60_950);

        AssertClose(new Vector3(6f, 0f, 0f), character.Position);
    }

    [Fact]
    public void AFixedSpeedRow_DerivesTheDurationFromTheDistance()
    {
        // Row 187880 states move_duration 2 next to fixed_speed 10, and row 1335509 states 733 ms next to a
        // speed of 4 for the same 20 m: when a row names a speed, the duration is the distance over it.
        var (shard, character) = CreateRuntime(60_000);
        var command = new MovementSlideCommand(new MovementSlideCommandDef
        {
            Id = 1335509,
            OffsetY = 20f,
            MoveDuration = 733,
            FixedSpeed = 4f,
        });

        Assert.True(command.Execute(new Context(shard, character)));
        Tick(shard, character, 62_500);
        AssertClose(new Vector3(0f, 10f, 0f), character.Position); // 4 m/s: 10 m in 2.5 s.

        Tick(shard, character, 65_000);
        AssertClose(new Vector3(0f, 20f, 0f), character.Position); // 20 m in 5 s.
        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));
    }

    [Fact]
    public void AZeroOffsetOrDuration_StartsNothing()
    {
        var (shard, character) = CreateRuntime(60_000);

        Assert.True(new MovementSlideCommand(new MovementSlideCommandDef { Id = 1, OffsetX = 0f, MoveDuration = 500 }).Execute(new Context(shard, character)));
        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));

        Assert.True(new MovementSlideCommand(new MovementSlideCommandDef { Id = 2, OffsetX = 5f, MoveDuration = 0 }).Execute(new Context(shard, character)));
        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));
    }

    [Fact]
    public void APlayerControlledCharacterIsNotMoved()
    {
        // A player's position is the client's to report: the row's motion for the player's own ability is what
        // the client predicted (the flag the database spells allow_prediction), and the server overwriting it
        // would fight the movement relay.
        var (shard, character) = CreateRuntime(60_000);
        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        player.AttachRealChannels();
        character.SetControllingPlayer(player);

        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 1, OffsetX = 5f, MoveDuration = 667 });

        Assert.True(command.Execute(new Context(shard, character)));
        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));
        Tick(shard, character, 60_700);
        AssertClose(Vector3.Zero, character.Position);
    }

    [Fact]
    public void ACorpseDoesNotSlide()
    {
        // The dodge effect's duration chain ends on RequireCState(living=1) while its slide runs 167 ms
        // longer, so a mob killed mid-dodge must stop where it died.
        var (shard, character) = CreateRuntime(60_000);
        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 1, OffsetX = 5f, MoveDuration = 667 });

        Assert.True(command.Execute(new Context(shard, character)));
        Tick(shard, character, 60_200);
        AssertClose(new Vector3(1.4994f, 0f, 0f), character.Position);

        character.SetCharacterState(CharacterStateData.CharacterStatus.Dead, 60_200);
        Tick(shard, character, 60_400);

        AssertClose(new Vector3(1.4994f, 0f, 0f), character.Position);
        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));
    }

    [Fact]
    public void ASlideForAnEntityThatLeftTheShard_IsForgotten()
    {
        var (shard, character) = CreateRuntime(60_000);
        var command = new MovementSlideCommand(new MovementSlideCommandDef { Id = 1, OffsetX = 5f, MoveDuration = 667 });

        Assert.True(command.Execute(new Context(shard, character)));
        shard.EntityMan.Remove(character);
        shard.Abilities.Tick(20, 60_100, System.Threading.CancellationToken.None);

        Assert.False(shard.Abilities.IsMovementSliding(character.EntityId));
    }

    private static (FakeShard Shard, CharacterEntity Character) CreateRuntime(ulong time)
    {
        var shard = new FakeShard { CurrentTimeLong = time };
        shard.Abilities = new AbilitySystem(shard, new FakeAptitudeFactory(shard));
        return (shard, AddCharacter(shard));
    }

    private static CharacterEntity AddCharacter(FakeShard shard)
    {
        var character = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(character.EntityId, character);
        // CurrentTime is a default interface member on IShard; the fake is read through its own clock.
        character.SetCharacterState(CharacterStateData.CharacterStatus.Living, unchecked((uint)shard.CurrentTimeLong));
        return character;
    }

    private static void Tick(FakeShard shard, CharacterEntity character, ulong time)
    {
        shard.CurrentTimeLong = time;
        shard.Abilities.ProcessTarget(character, time);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.True(
            Vector3.Distance(expected, actual) <= Tolerance,
            $"expected {expected} but was {actual}");
    }
}
