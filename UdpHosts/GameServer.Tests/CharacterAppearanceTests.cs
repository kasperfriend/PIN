using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Tests.Fakes;
using GrpcGameServerAPIClient;
using Shared.Common;
using Shared.Common.Characters;
using Xunit;
using BasicCharacterInfo = GrpcGameServerAPIClient.BasicCharacterInfo;

namespace GameServer.Tests;

/// <summary>
///     Tests for the "get what you select" appearance rules: the head accessory
///     ordering both surfaces render from (<see cref="CharacterAppearance"/>) and
///     the chassis warpaint override that makes the in-game battleframe wear the
///     colors the character selection screen preview shows.
/// </summary>
public class CharacterAppearanceTests
{
    [Fact]
    public void HeadAccessoryMeshes_LeadsWithHairAndFacialHair()
    {
        // The original service's data model: hair == head_accessories[0],
        // facial_hair == head_accessories[1] (verified against a captured
        // character). Both surfaces must render from that one ordering.
        var visuals = new CharacterVisualsRecord
        {
            Hair = 10089,
            FacialHair = 10106,
            HeadAccessories = [10089, 10106, 10270]
        };

        Assert.Equal(new uint[] { 10089, 10106, 10270 }, CharacterAppearance.HeadAccessoryMeshes(visuals).ToArray());
    }

    [Fact]
    public void HeadAccessoryMeshes_PutsTheHairMeshBeforeTheExtraAccessories()
    {
        // A record whose Hair is not part of the accessory list (PIN's default
        // template): the hair mesh leads, the extra accessories follow.
        var visuals = new CharacterVisualsRecord
        {
            Hair = 10113,
            FacialHair = 0,
            HeadAccessories = [10117]
        };

        Assert.Equal(new uint[] { 10113, 10117 }, CharacterAppearance.HeadAccessoryMeshes(visuals).ToArray());
    }

    [Fact]
    public void HeadAccessoryMeshes_SkipsZerosAndDuplicates()
    {
        var visuals = new CharacterVisualsRecord
        {
            Hair = 0,
            FacialHair = 10106,
            HeadAccessories = [10106, 0, 10270, 10106]
        };

        Assert.Equal(new uint[] { 10106, 10270 }, CharacterAppearance.HeadAccessoryMeshes(visuals).ToArray());
    }

    [Fact]
    public void HeadAccessoryMeshes_EmptyVisuals_WearNothing()
    {
        var visuals = new CharacterVisualsRecord { Hair = 0, FacialHair = 0, HeadAccessories = [] };
        Assert.Empty(CharacterAppearance.HeadAccessoryMeshes(visuals));
    }

    [Fact]
    public void ApplyLoadout_WithoutOverride_WearsTheLoadoutChassisWarpaint()
    {
        using var sdb = SdbWithoutBattleframes();
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));

        var loadout = new CharacterLoadout
        {
            ChassisWarpaint = new ChassisWarpaintResult
            {
                Gradients = [],
                Colors = [1, 2, 3, 4, 5, 6, 7],
                Palettes = []
            }
        };

        character.ApplyLoadout(loadout);

        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7 }, character.Character_EquipmentView.CurrentEquipmentProp.Chassis.Visuals.Colors);
    }

    [Fact]
    public void ApplyLoadout_WithChosenWarpaint_WearsItOnTheChassis()
    {
        using var sdb = SdbWithoutBattleframes();
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));

        // The 7 packed light-dark colors a characters.json record carries in
        // Visuals.Warpaint — the exact values the selection screen preview
        // renders the armor with.
        var chosenWarpaint = DefaultCharacterTemplate.Warpaint;
        character.SetChassisWarpaint(chosenWarpaint);

        var loadout = new CharacterLoadout
        {
            ChassisWarpaint = new ChassisWarpaintResult
            {
                Gradients = [],
                Colors = [1, 2, 3, 4, 5, 6, 7],
                Palettes = []
            }
        };

        character.ApplyLoadout(loadout);

        var chassisVisuals = character.Character_EquipmentView.CurrentEquipmentProp.Chassis.Visuals;
        Assert.Equal(chosenWarpaint, chassisVisuals.Colors);

        // The override carries colors only (no palettes), mirroring the data the
        // selection screen itself renders from.
        Assert.Empty(chassisVisuals.Palettes);
        Assert.Empty(chassisVisuals.Patterns);
    }

    [Fact]
    public void SetChassisWarpaint_EmptyOrNull_FallsBackToTheChassisDefault()
    {
        using var sdb = SdbWithoutBattleframes();
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));

        var loadout = new CharacterLoadout
        {
            ChassisWarpaint = new ChassisWarpaintResult
            {
                Gradients = [],
                Colors = [1, 2, 3, 4, 5, 6, 7],
                Palettes = []
            }
        };

        character.SetChassisWarpaint([9, 9, 9, 9, 9, 9, 9]);
        character.SetChassisWarpaint([]);
        character.ApplyLoadout(loadout);

        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7 }, character.Character_EquipmentView.CurrentEquipmentProp.Chassis.Visuals.Colors);
    }

    [Fact]
    public void LoadRemote_AppliesTheChosenWarpaintAndTheHairAccessory()
    {
        using var sdb = SdbWithoutBattleframes();
        var shard = new FakeShard();
        var character = new CharacterEntity(shard, shard.GetNextGuid(0));

        var chosenWarpaint = DefaultCharacterTemplate.Warpaint;

        var remote = new CharacterAndBattleframeVisuals
        {
            CharacterInfo = new BasicCharacterInfo
            {
                Name = "Kasper",
                CurrentBattleframeSDBId = 76334
            },
            CharacterVisuals = new CharacterVisuals
            {
                Head = new WebId { Id = 10026 },
                Eyes = new WebId { Id = 10001 },
                VoiceSet = new WebId { Id = 1033 },
                SkinColor = new WebIdValueColor { Id = 118969, Value = new WebColor { Color = 4294930822 } },
                LipColor = new WebIdValueColor { Id = 1, Value = new WebColor { Color = 4294903873 } },
                EyeColor = new WebIdValueColor { Id = 118980, Value = new WebColor { Color = 1633685600 } },
                HairColor = new WebIdValueColor { Id = 77193, Value = new WebColor { Color = 1917780001 } },
                FacialHairColor = new WebIdValueColor { Id = 77193, Value = new WebColor { Color = 1917780001 } },
                Hair = new WebIdValueColorId { Id = 10113 },

                // LoadRemote reads these too; the capture carries id 0 for both.
                Glider = new WebId { Id = 0 },
                Vehicle = new WebId { Id = 0 },
            },
            BattleframeVisuals = new PlayerBattleframeVisuals
            {
                WarpaintId = 143225
            }
        };
        remote.BattleframeVisuals.Warpaint.AddRange(chosenWarpaint);

        // What GameServerApiService sends after the hair-mesh fix: the hair mesh
        // leads the accessory list (CharacterAppearance.HeadAccessoryMeshes).
        remote.CharacterVisuals.HeadAccessories.Add(new WebIdValueColor { Id = 10113 });
        remote.CharacterVisuals.HeadAccessories.Add(new WebIdValueColor { Id = 10117 });

        character.LoadRemote(remote);

        // The hair mesh the mapper sent leads the in-game accessory list...
        Assert.Equal(new uint[] { 10113, 10117 }, character.StaticInfo.HeadAccessories.ToArray());

        // ...and the body colors flowed through unchanged.
        Assert.Equal(
            new uint[] { 4294930822, 4294903873, 1633685600, 1917780001, 1917780001 },
            character.StaticInfo.Visuals.Colors.ToArray());

        // The chosen warpaint sticks and is worn on the chassis of any loadout.
        var loadout = new CharacterLoadout
        {
            ChassisWarpaint = new ChassisWarpaintResult
            {
                Gradients = [],
                Colors = [1, 2, 3, 4, 5, 6, 7],
                Palettes = []
            }
        };
        character.ApplyLoadout(loadout);

        Assert.Equal(chosenWarpaint, character.Character_EquipmentView.CurrentEquipmentProp.Chassis.Visuals.Colors);
    }

    /// <summary>
    ///     Points the SDB interface's battleframe table at an empty dictionary, the
    ///     same reflection pattern the health tests use, so ApplyLoadout can run
    ///     without a loaded static database (the energy lookup for chassis 0 finds
    ///     no row and returns early). Disposing restores the original table.
    /// </summary>
    private static IDisposable SdbWithoutBattleframes()
    {
        var field = typeof(StaticDB.SDBInterface).GetField("_battleframe", BindingFlags.NonPublic | BindingFlags.Static);
        var original = (Dictionary<uint, StaticDB.Records.dbitems.Battleframe>)field.GetValue(null);
        field.SetValue(null, new Dictionary<uint, StaticDB.Records.dbitems.Battleframe>());
        return new RestoreOnDispose(() => field.SetValue(null, original));
    }

    private sealed class RestoreOnDispose : IDisposable
    {
        private readonly Action _restore;

        public RestoreOnDispose(Action restore)
        {
            _restore = restore;
        }

        public void Dispose()
        {
            _restore();
        }
    }
}
