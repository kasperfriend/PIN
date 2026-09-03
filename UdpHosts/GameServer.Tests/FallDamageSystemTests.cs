using System.Numerics;
using System.Threading;
using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.Systems.CharacterLifecycle;
using GameServer.Systems.Combat;
using GameServer.Tests.Fakes;
using Xunit;

namespace GameServer.Tests;

public class FallDamageSystemTests
{
    private const short MovementStateStanding = 0x1000;
    private const short MovementStateFalling = 0x3000;
    private const short MovementStateJetpack = 0x6000;
    private const short MovementStateGlider = 0x7000;
    private const short MovementStateKnockdownFalling = unchecked((short)0xb000);
    private const short MovementStateOccupant = unchecked((short)0xd000);

    private static readonly StandardFallDamageRules Rules = new()
    {
        Enabled = true,
        SafeImpactSpeed = 12f,
        LethalImpactSpeed = 48f,
        DamagePerSpeed = 130,
        MinAirTimeMs = 250f,
    };

    private static MovementPoseData Pose(short movementState, Vector3 velocity, short groundTimePositiveAirTimeNegative, byte waterLevelAndDesc = 0)
    {
        return new MovementPoseData
        {
            ShortTime = 1,
            MovementType = MovementDataType.PosRotState,
            WaterLevelAndDesc = waterLevelAndDesc,
            PosRotState = new MovementPosRotState
            {
                Pos = Vector3.Zero,
                Rot = Quaternion.Identity,
                MovementState = movementState
            },
            Velocity = velocity,
            JetpackEnergy = 0,
            GroundTimePositiveAirTimeNegative = groundTimePositiveAirTimeNegative,
            TimeSinceLastJump = 0,
            HaveDebugData = 0
        };
    }

    private static (FakeShard Shard, CharacterEntity Character, FallDamageSystem System) CreateSut(int maxHealth = 5000)
    {
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));
        character.SetControllingPlayer(new FakeNetworkPlayer(shard) { CharacterEntity = character });
        character.Alive = true;
        character.CanBleedout = true;
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Living, 0);
        character.SetMaxHealth(maxHealth, resetCurrent: true);
        shard.CharacterLifecycle.OnCharacterCreated(character);

        var system = new FallDamageSystem(shard, shard.Damage, Rules);
        return (shard, character, system);
    }

    private static void GoAirborne(FakeShard shard, CharacterEntity character, FallDamageSystem system, float fallSpeed, short movementState = MovementStateFalling)
    {
        system.OnMovementInput(character, Pose(movementState, new Vector3(0, 0, -fallSpeed), -5000));
    }

    private static void Land(FakeShard shard, CharacterEntity character, FallDamageSystem system, byte waterLevelAndDesc = 0)
    {
        system.OnMovementInput(character, Pose(MovementStateStanding, Vector3.Zero, 100, waterLevelAndDesc));
    }

    [Fact]
    public void OnMovementInput_LandingAboveSafeSpeed_AppliesFallDamage()
    {
        var (shard, character, system) = CreateSut(maxHealth: 5000);

        GoAirborne(shard, character, system, fallSpeed: 30f);
        Land(shard, character, system);

        // (30 - 12) * 130 = 2340
        Assert.Equal(5000 - 2340, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_LandingBelowSafeSpeed_DealsNoDamage()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 5f);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_TracksFastestPointOfTheFall()
    {
        var (shard, character, system) = CreateSut(maxHealth: 5000);

        GoAirborne(shard, character, system, fallSpeed: 10f);
        GoAirborne(shard, character, system, fallSpeed: 30f);
        GoAirborne(shard, character, system, fallSpeed: 5f);
        Land(shard, character, system);

        // Impact speed is the fastest point (30), not the last sample (5)
        Assert.Equal(5000 - 2340, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_DamageIsAppliedOnlyOncePerLanding()
    {
        var (shard, character, system) = CreateSut(maxHealth: 5000);

        GoAirborne(shard, character, system, fallSpeed: 30f);
        Land(shard, character, system);
        Land(shard, character, system);

        Assert.Equal(5000 - 2340, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_ThrusterDuringFall_NegatesFallDamage()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f);
        GoAirborne(shard, character, system, fallSpeed: 40f, movementState: MovementStateJetpack);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_GliderDuringFall_NegatesFallDamage()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f, movementState: MovementStateGlider);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_WaterLanding_DealsNoDamage()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f);
        Land(shard, character, system, waterLevelAndDesc: 0x02);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_KnockdownFalling_DealsNoDamage()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f, movementState: MovementStateKnockdownFalling);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_ImmuneCombatFlag_DealsNoDamage()
    {
        var (shard, character, system) = CreateSut();

        character.SetCombatFlags(new CombatFlagsData
        {
            Value = CombatFlagsData.CharacterCombatFlags.immune_falldamage,
            Time = 0
        });

        GoAirborne(shard, character, system, fallSpeed: 40f);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_StaleLandingAfterTeleportGap_DealsNoDamage()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f);
        shard.CurrentTimeLong += 10_000; // Teleport or packet gap: the landing is not fresh
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_ResetFor_ClearsInProgressFall()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f);
        system.ResetFor(character);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_OccupantSamples_AreIgnored()
    {
        var (shard, character, system) = CreateSut();

        system.OnMovementInput(character, Pose(MovementStateOccupant, new Vector3(0, 0, -40), 0));
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void OnMovementInput_DeadCharacter_IsIgnored()
    {
        var (shard, character, system) = CreateSut();

        character.Alive = false;
        character.SetCharacterState(AeroMessages.GSS.Character.CharacterStateData.CharacterStatus.Dead, 0);

        GoAirborne(shard, character, system, fallSpeed: 40f);
        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }

    [Fact]
    public void SimulateLanding_AppliesSameMathAsRealLandings()
    {
        var (shard, character, system) = CreateSut(maxHealth: 5000);

        system.SimulateLanding(character, 30f);

        Assert.Equal(5000 - 2340, character.CurrentHealth);
    }

    [Fact]
    public void SimulateLanding_AtLethalSpeed_KillsTheCharacter()
    {
        var (shard, character, system) = CreateSut(maxHealth: 5000);

        system.SimulateLanding(character, 60f);

        Assert.Equal(0, character.CurrentHealth);
        Assert.Equal(CharacterLifecycleState.Bleedout, shard.CharacterLifecycle.GetState(character));
    }

    [Fact]
    public void Tick_RemovesTrackersForGoneEntities()
    {
        var (shard, character, system) = CreateSut();

        GoAirborne(shard, character, system, fallSpeed: 40f);

        shard.Entities.Remove(character.EntityId);
        system.Tick(0.1, shard.CurrentTimeLong, CancellationToken.None);

        Land(shard, character, system);

        Assert.Equal(5000, character.CurrentHealth);
    }
}
