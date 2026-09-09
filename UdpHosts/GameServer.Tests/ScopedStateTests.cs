using System;
using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Controller;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.Packets;
using GameServer.StaticDB.Records.apt;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Custom;
using GameServer.Systems.Aptitude.Commands.Modifier;
using GameServer.Systems.Aptitude.Commands.Requirement;
using GameServer.Systems.Aptitude.Commands.SetFlags;
using GameServer.Tests.Fakes;
using Xunit;
using ScopeController = GameServer.Controllers.Character.CombatController;

namespace GameServer.Tests;

public class ScopedStateTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void UseScope_HoldsOneEffectAndKeepsTheClientEventTime(int inScope)
    {
        var (shard, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, unchecked((byte)inScope)));
        var state = Assert.Single(character.GetActiveEffects().Where(effect => effect != null));
        Assert.Equal(1313u, state.Effect.Id);
        Assert.Equal(60_001u, state.Time);
        Assert.Equal(60_000u, state.Context.EffectStartTime);
        Assert.Equal((byte)1, character.FireMode_1.Mode);
        Assert.Equal((byte)0, character.GetActiveFireModeIndex()); // ADS must not select the underbarrel.
        Assert.True(character.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.restrict_sprint));
        Assert.Equal(0.5f, character.GetCurrentStatModifierValue(StatModifierIdentifier.RunSpeedMult));

        shard.CurrentTimeLong = 62_000;
        shard.Abilities.ProcessTarget(character, 62_000);
        controller.UseScope(player, player, character.EntityId, Packet(62_000, 1));
        Assert.Same(state, Assert.Single(character.GetActiveEffects().Where(effect => effect != null)));
        Assert.Equal(60_001u, state.Time);
        Assert.Equal(60_001u, character.Character_CombatController.StatusEffects_0Prop.Value.Time);
        // Self-applied effects are not mirrored into the owner-private local effects slots (live never
        // does, and the duplicate would fight the client's own prediction of the sights).
        Assert.Null(character.Character_LocalEffectsController.LocalStatusEffects_0Prop);

        controller.UseScope(player, player, character.EntityId, Packet(62_001, 0));
        AssertUnscoped(character);
        Assert.True(state.Removed);
    }

    /// <summary>
    ///     The sights are the weapon's secondary fire mode on the wire: the live servers answer
    ///     <c>UseScope InScope=1</c> with <c>FireMode_0 = { Mode = 1, Time = &lt;the client's event time&gt; }</c>
    ///     and clear it again on scope out (2014-09-19 gameplay capture, GSS protocol version 883 — in that
    ///     build the field sits behind the fifteen status-effect slot change times; it is the same field the
    ///     2015/2016 captures show <c>SelectFireMode</c> writing). That is what the client predicts when it
    ///     raises the sights, so a scope that is only replicated through a field the client does not predict
    ///     leaves its own fire mode contradicted by the server and it drops the sights again a moment later.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void UseScope_WritesTheScopedFireModeWithTheClientEventTime(int inScope)
    {
        var (_, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, unchecked((byte)inScope)));

        Assert.Equal((byte)1, character.FireMode_0.Mode);
        Assert.Equal(60_001u, character.FireMode_0.Time);
        Assert.Equal(character.FireMode_0, character.Character_CombatController.FireMode_0Prop);
        Assert.Equal(character.FireMode_0, character.Character_CombatView.FireMode_0Prop);

        // The second fire mode field carries the scope as well, and raising the sights must not switch the
        // weapon the server simulates (the underbarrel of the main weapon has different sights).
        Assert.Equal((byte)1, character.FireMode_1.Mode);
        Assert.Equal((byte)0, character.GetActiveFireModeIndex());

        controller.UseScope(player, player, character.EntityId, Packet(60_002, 0));
        Assert.Equal((byte)0, character.FireMode_0.Mode);
        Assert.Equal((byte)0, character.FireMode_1.Mode);
    }

    /// <summary>
    ///     Live confirms a scope-in with two more halves: the effect slot replicates a zero-based stack
    ///     count (a fresh apply carries Stack 0, never 1) and <c>WeaponFireBaseTime</c> arrives as
    ///     <c>{T &amp; 0xFFFF, 0x81}</c> alongside <c>FireMode_0 = {1, T}</c>, while scope-out replicates
    ///     FM0 alone (2014-09-19 capture). The client drops rifle IronSights when either half is missing.
    /// </summary>
    [Fact]
    public void UseScope_ReplicatesLiveScopeAnswer_StackZeroAndScopedWeaponState()
    {
        var (_, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));

        var slot = character.Character_CombatController.StatusEffects_0Prop.Value;
        Assert.Equal(1313u, slot.Id);
        Assert.Equal((byte)0, slot.Stack);
        Assert.Equal(60_001u, slot.Time);

        var fireBaseTime = character.Character_CombatController.WeaponFireBaseTimeProp;
        Assert.Equal(unchecked((ushort)60_001), fireBaseTime.ChangeTime);
        Assert.Equal((byte)0x81, fireBaseTime.Unk);

        // Scope-out replicates FM0 alone and leaves the weapon base time untouched, like live.
        controller.UseScope(player, player, character.EntityId, Packet(60_002, 0));
        Assert.Equal(fireBaseTime, character.Character_CombatController.WeaponFireBaseTimeProp);
    }

    /// <summary>
    ///     Scoping out hands the replicated fire mode back to the mode the player selected: a character that
    ///     switched to the underbarrel keeps firing the underbarrel once it stops aiming.
    /// </summary>
    [Fact]
    public void ScopeOut_KeepsTheFireModeThePlayerSelected()
    {
        var (_, character, player, controller) = CreateRuntime();
        controller.SelectFireMode(player, player, character.EntityId, Packet(60_002, 1));
        Assert.Equal((byte)1, character.GetActiveFireModeIndex());

        controller.UseScope(player, player, character.EntityId, Packet(60_003, 1));
        Assert.Equal((byte)1, character.FireMode_0.Mode);
        Assert.Equal(102u, character.ScopeStatusEffectId); // The underbarrel's sights, not the main weapon's.

        controller.UseScope(player, player, character.EntityId, Packet(60_004, 0));
        Assert.Equal((byte)0, character.FireMode_1.Mode); // No longer scoped.
        Assert.Equal((byte)1, character.FireMode_0.Mode); // Still the underbarrel.
        Assert.Equal((byte)1, character.GetActiveFireModeIndex());
        Assert.Equal(0u, character.ScopeStatusEffectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WeaponOrFireModeSwitch_ClearsTheScopedModeAsWellAsTheEffect(bool fireMode)
    {
        var (_, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));
        if (fireMode)
        {
            controller.SelectFireMode(player, player, character.EntityId, Packet(60_002, 1));
            Assert.Equal((byte)1, character.GetActiveFireModeIndex());
        }
        else
        {
            controller.SelectWeapon(player, player, character.EntityId, Packet(60_002, 2, 0));
            Assert.Equal((byte)2, character.WeaponIndex.Index);
        }

        AssertUnscoped(character);
        Assert.Equal(60_002u, character.FireMode_1.Time);
        Assert.Equal(character.FireMode_1, character.Character_CombatController.FireMode_1Prop);
        Assert.Equal(character.FireMode_1, character.Character_CombatView.FireMode_1Prop);

        controller.UseScope(player, player, character.EntityId, Packet(60_003, 1));
        Assert.Equal(102u, Assert.Single(character.GetActiveEffects().Where(effect => effect != null)).Effect.Id);
    }

    [Fact]
    public void ExternalEffectRemoval_ClearsTheModeAndAllowsTheNextScopeRequest()
    {
        var (shard, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));
        shard.CurrentTimeLong = 61_000;
        Assert.True(shard.Abilities.DoRemoveEffect(character, 1313));
        AssertUnscoped(character);

        controller.UseScope(player, player, character.EntityId, Packet(61_001, 1));
        Assert.Equal(1313u, character.ScopeStatusEffectId);
        Assert.Single(character.GetActiveEffects().Where(effect => effect != null));
    }

    [Fact]
    public void Death_ClearsBothHalvesImmediatelyAndRejectsScopingWhileDead()
    {
        var (_, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));
        character.SetCharacterState(CharacterStateData.CharacterStatus.Dead, 60_002);
        AssertUnscoped(character);
        controller.UseScope(player, player, character.EntityId, Packet(60_003, 1));
        AssertUnscoped(character);
    }

    [Fact]
    public void StaleScopeOut_DoesNotCancelANewerScopeIn()
    {
        var (_, character, player, controller) = CreateRuntime();
        controller.UseScope(player, player, character.EntityId, Packet(60_200, 1));
        controller.UseScope(player, player, character.EntityId, Packet(60_100, 0));
        Assert.Equal((byte)1, character.FireMode_1.Mode);
        Assert.Equal(60_200u, character.FireMode_1.Time);
        Assert.Equal(1313u, character.ScopeStatusEffectId);
    }

    [Fact]
    public void ClockWrap_ZeroRemainsAValidScopePredictionTimestamp()
    {
        var (_, character, player, controller) = CreateRuntime(uint.MaxValue - 1ul);
        controller.UseScope(player, player, character.EntityId, Packet(0, 1));
        var state = Assert.Single(character.GetActiveEffects().Where(effect => effect != null));
        Assert.Equal(0u, state.Time);
        Assert.Equal(0u, character.FireMode_1.Time);
        controller.UseScope(player, player, character.EntityId, Packet(uint.MaxValue, 0));
        Assert.Equal((byte)1, character.FireMode_1.Mode); // Before the wrap: stale.
        controller.UseScope(player, player, character.EntityId, Packet(1, 0));
        AssertUnscoped(character);
    }

    [Fact]
    public void UnknownScopeEffect_DoesNotLeaveTheScopedModeSet()
    {
        var (shard, character, player, controller) = CreateRuntime();
        ((FakeAptitudeFactory)shard.Abilities.Factory).Effects.Clear();
        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));
        AssertUnscoped(character);
    }

    private static void AssertUnscoped(CharacterEntity character)
    {
        Assert.Equal((byte)0, character.FireMode_1.Mode);
        Assert.Equal(0u, character.ScopeStatusEffectId);
        Assert.All(character.GetActiveEffects(), Assert.Null);
        Assert.Null(character.Character_CombatController.StatusEffects_0Prop);
        Assert.Null(character.Character_LocalEffectsController.LocalStatusEffects_0Prop);
        Assert.False(character.HasCombatFlag(CombatFlagsData.CharacterCombatFlags.restrict_sprint));
        Assert.Equal(1f, character.GetCurrentStatModifierValue(StatModifierIdentifier.RunSpeedMult));
    }

    [Fact]
    public void UseScope_SendsTheCombatLogConfirmationRowTheCaptureDocuments()
    {
        var (_, character, player, controller) = CreateRuntime(withOwnerChannel: true);

        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));
        player.FlushAttachedChannels();

        // The row the 2014 capture documents: attributed to the weapon, carrying the scope statusfx and the
        // client's UseScope time. Public and private logs each carry it to the owner, so find it twice.
        Assert.True(CountPacketsContaining(player, StatusFxRowBytes(source: 0x01, logType: 0x0B, effectId: 1313, time: 60_001u)) >= 2);
    }

    [Fact]
    public void UseScopeOut_EchoesTheRemoveRowStampedWithServerTime()
    {
        var (_, character, player, controller) = CreateRuntime(withOwnerChannel: true);

        controller.UseScope(player, player, character.EntityId, Packet(60_001, 1));
        controller.UseScope(player, player, character.EntityId, Packet(60_100, 0));
        player.FlushAttachedChannels();

        // The remove row carries the server clear time the slot clear itself was stamped with.
        Assert.True(CountPacketsContaining(player, StatusFxRowBytes(source: 0x01, logType: 0x0C, effectId: 1313, time: 60_000u)) >= 2);
    }

    [Fact]
    public void ChainAppliedEffect_IsAttributedToTheStatusFxSystem()
    {
        var (shard, character, player, _) = CreateRuntime(withOwnerChannel: true);
        ((FakeAptitudeFactory)shard.Abilities.Factory).Effects[9001] = ScopeEffect(9001);

        Assert.True(shard.Abilities.DoApplyEffect(9001, character, new Context(shard, character) { InitTime = 60_042 }));
        player.FlushAttachedChannels();

        // Not the weapon's scope effect, so the row is attributed to the status effect system.
        Assert.True(CountPacketsContaining(player, StatusFxRowBytes(source: 0x06, logType: 0x0B, effectId: 9001, time: 60_042u)) >= 2);
    }

    /// <summary>
    ///     The owner-private local effects slots exist for effects that come from somewhere else (that is
    ///     all the 2016 capture ever writes there). A self-applied effect - the scope effect above all -
    ///     is predicted by the client itself, and a server-owned duplicate makes it discard its own
    ///     prediction and drop the sights mid-hold. Foreign-initiated effects must still be mirrored.
    /// </summary>
    [Fact]
    public void ForeignInitiatedEffect_IsMirroredIntoTheLocalEffectsSlots()
    {
        var (shard, character, _, _) = CreateRuntime();
        ((FakeAptitudeFactory)shard.Abilities.Factory).Effects[9002] = ScopeEffect(9002);
        var initiator = FakeCharacterFactory.Create(shard);
        shard.EntityMan.Add(initiator.EntityId, initiator);

        Assert.True(shard.Abilities.DoApplyEffect(9002, character, new Context(shard, initiator) { InitTime = 60_042 }));

        var slot = character.Character_LocalEffectsController.LocalStatusEffects_0Prop;
        Assert.NotNull(slot);
        Assert.Equal(9002u, slot.Value.Effect);
        Assert.Equal(initiator.AeroEntityId.Backing, slot.Value.Entity.Backing);

        shard.Abilities.DoRemoveEffect(character, 9002);
        Assert.Null(character.Character_LocalEffectsController.LocalStatusEffects_0Prop);
    }

    private static byte[] StatusFxRowBytes(byte source, byte logType, uint effectId, uint time)
    {
        // HaveData = 1 (int32), Bytes = 10 (ushort), then the row: 1 source, 1 log type, 4 id, 4 time.
        var bytes = new byte[16];
        bytes[0] = 1;
        bytes[4] = 10;
        bytes[6] = source;
        bytes[7] = logType;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), effectId);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), time);
        return bytes;
    }

    private static int CountPacketsContaining(FakeNetworkPlayer player, byte[] needle)
    {
        return player.SentPackets.Count(packet => Contains(packet.Span, needle));
    }

    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (var start = 0; start + needle.Length <= haystack.Length; start++)
        {
            if (haystack.Slice(start, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    private static (FakeShard Shard, CharacterEntity Character, FakeNetworkPlayer Player, ScopeController Controller) CreateRuntime(ulong time = 60_000, bool withOwnerChannel = false)
    {
        var shard = new FakeShard { CurrentTimeLong = time };
        var factory = new FakeAptitudeFactory(shard);
        shard.Abilities = new AbilitySystem(shard, factory);
        factory.Effects[1313] = ScopeEffect(1313);
        factory.Effects[102] = ScopeEffect(102);
        var character = FakeCharacterFactory.Create(shard);
        // Keep the replicated controller fields observable without opening any network channels.
        character.Character_CombatController = new AeroMessages.GSS.Character.Controller.CombatController();
        character.Character_LocalEffectsController = new LocalEffectsController();
        shard.EntityMan.Add(character.EntityId, character);
        character.SetWeaponIndex(new WeaponIndexData { Index = 1, Time = unchecked((uint)time) });

        // Only bypass the SDB-backed weapon-cache builder; requests, effect execution, expiry, cleanup and
        // replicated field setters all run through production code.
        var weapons = new CharacterEntity.ActiveWeaponDetails[3, 2];
        weapons[1, 0] = new CharacterEntity.ActiveWeaponDetails { ScopeStatusFx = 1313 };
        weapons[1, 1] = new CharacterEntity.ActiveWeaponDetails { ScopeStatusFx = 102 };
        weapons[2, 0] = new CharacterEntity.ActiveWeaponDetails { ScopeStatusFx = 102 };
        typeof(CharacterEntity).GetField("_weaponDetailsCache", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character, weapons);

        var player = new FakeNetworkPlayer(shard) { CharacterEntity = character };
        if (withOwnerChannel)
        {
            // Owner-bound traffic (controller updates, combat log rows) is only sent to a linked,
            // player-controlled character, and sending needs real channels: FlushViewChangesToPlayer
            // indexes NetChannels directly, so the link and the channels always come together.
            player.AttachRealChannels();
            character.Player = player;
        }

        var controller = new ScopeController();
        controller.Init(player, player, shard, shard.Logger);
        return (shard, character, player, controller);
    }

    private static Effect ScopeEffect(uint id) => new()
    {
        Data = new StatusEffectData { Id = id, MaxStackCount = 1, UpdateFrequency = 250 },
        ApplyChain = new Chain
        {
            Commands =
            [
                new StatModifierCommand(new StatModifierCommandDef { Id = 1605142, Stat = (ushort)StatModifierIdentifier.RunSpeedMult, Value = 50, Op = 2 }),
                new CombatFlagsCommand(new CombatFlagsCommandDef { Id = 1605139, RestrictSprint = 1 }),
                new RequirementServerCommand(new RequirementServerCommandDef { Id = 1605137, Local = 1 }),
            ],
        },
        DurationChain = new Chain
        {
            Commands =
            [
                new RequireCStateCommand(new RequireCStateCommandDef { Id = 1605145, Living = 1 }),
                new CustomNOOPCommand("RequireServerConfirmed", 1605144),
            ],
        },
    };

    private static GamePacket Packet(uint time, params byte[] fields)
    {
        var data = new byte[sizeof(uint) + fields.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(data, time);
        fields.CopyTo(data, sizeof(uint));
        return new GamePacket(default, data);
    }
}
