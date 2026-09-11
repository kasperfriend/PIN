using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shared.Common;
using Shared.Common.Characters;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the New You appearance payload (<see cref="PlayerVisualLoadout"/>):
///     the exact shape the client's customization screen reads from
///     <c>GET api/v2/characters/{guid}/visual_loadouts</c> (verified against the
///     live service's captured response) and the mapping back that the save
///     request (<c>.../purchase_and_update</c>) applies to the character record.
/// </summary>
public class PlayerVisualLoadoutTests
{
    private static readonly JsonSerializerOptions WebJson = new()
                                                             {
                                                                 PropertyNamingPolicy = new SnakeCasePropertyNamingPolicy()
                                                             };

    /// <summary>The character of the live capture this shape came from (2015-05-02, build 1869).</summary>
    private static CharacterRecord CapturedCharacter()
    {
        return new CharacterRecord
               {
                   CharacterGuid = 9168405683928077054,
                   Gender = 0,
                   Race = 0,
                   Visuals = new CharacterVisualsRecord
                             {
                                 SkinColorId = 77181,
                                 VoiceSet = 1000,
                                 Head = 10002,
                                 Eyes = 10001,
                                 EyeColorId = 77183,
                                 LipColorId = 1,
                                 HairColorId = 77194,
                                 FacialHairColorId = 77194,
                                 Hair = 10089,
                                 FacialHair = 10106,
                                 HeadAccessories = [],
                                 Ornaments = [10270, 10224, 10061]
                             }
               };
    }

    [Fact]
    public void From_SerializesToTheLiveServicesResponse()
    {
        // The exact response the live service gave the client for its own
        // character: the New You screen reads these ids and not a single extra
        // field, so the shape is pinned here.
        const string captured = """
                                {"id":0,"character_guid":9168405683928077054,"gender":0,"race":0,
                                 "skin_color_id":77181,"voice_set_id":1000,"head_id":10002,"eye_id":10001,
                                 "eye_color_id":77183,"lip_color_id":1,"hair_color_id":77194,
                                 "facial_hair_color_id":77194,
                                 "head_accessories":[{"remote_id":10089},{"remote_id":10106}],
                                 "ornaments":[{"remote_id":10270},{"remote_id":10224},{"remote_id":10061}]}
                                """;

        var json = JsonSerializer.Serialize(PlayerVisualLoadout.From(CapturedCharacter()), WebJson);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(captured), JsonNode.Parse(json)),
                    $"Serialized loadout does not match the captured response: {json}");
    }

    [Fact]
    public void From_LeadsTheHeadAccessoriesWithHairAndFacialHair()
    {
        var record = CapturedCharacter();
        record.Visuals.HeadAccessories = [10270];

        var loadout = PlayerVisualLoadout.From(record);

        Assert.Equal(new uint[] { 10089, 10106, 10270 }, Ids(loadout.HeadAccessories));
    }

    [Fact]
    public void From_CarriesTheGenderAndRaceOfTheRecord()
    {
        var record = CapturedCharacter();
        record.Gender = 1;
        record.Race = 0;

        var loadout = PlayerVisualLoadout.From(record);

        Assert.Equal(1, loadout.Gender);
        Assert.Equal(0, loadout.Race);
    }

    [Fact]
    public void ApplyTo_StoresThePostedIds()
    {
        var record = CapturedCharacter();

        new PlayerVisualLoadout
        {
            Gender = 1,
            Race = 0,
            SkinColorId = 77183,
            VoiceSetId = 1001,
            HeadId = 10003,
            EyeId = 10004,
            EyeColorId = 77182,
            LipColorId = 2,
            HairColorId = 77195,
            FacialHairColorId = 77195,
            HeadAccessories = [Remote(10090), Remote(10107)],
            Ornaments = [Remote(10271)]
        }.ApplyTo(record);

        Assert.Equal(1u, record.Gender);
        Assert.Equal(0u, record.Race);
        Assert.Equal(77183u, record.Visuals.SkinColorId);
        Assert.Equal(1001u, record.Visuals.VoiceSet);
        Assert.Equal(10003u, record.Visuals.Head);
        Assert.Equal(10004u, record.Visuals.Eyes);
        Assert.Equal(77182u, record.Visuals.EyeColorId);
        Assert.Equal(2u, record.Visuals.LipColorId);
        Assert.Equal(77195u, record.Visuals.HairColorId);
        Assert.Equal(77195u, record.Visuals.FacialHairColorId);
        Assert.Equal(10090u, record.Visuals.Hair);
        Assert.Equal(10107u, record.Visuals.FacialHair);
        Assert.Empty(record.Visuals.HeadAccessories);
        Assert.Equal(new uint[] { 10271 }, record.Visuals.Ornaments.ToArray());
    }

    [Fact]
    public void ApplyTo_SplitsHairFacialHairAndExtraAccessories()
    {
        var record = CapturedCharacter();
        record.Visuals.Hair = 0;
        record.Visuals.FacialHair = 0;
        record.Visuals.HeadAccessories = [9999];

        new PlayerVisualLoadout
        {
            HeadAccessories = [Remote(10089), Remote(10106), Remote(10270)]
        }.ApplyTo(record);

        Assert.Equal(10089u, record.Visuals.Hair);
        Assert.Equal(10106u, record.Visuals.FacialHair);
        Assert.Equal(new uint[] { 10270 }, record.Visuals.HeadAccessories.ToArray());
    }

    [Fact]
    public void ApplyTo_ResolvesThePostedPaletteIdsToColors()
    {
        // Palettes 76714/76715 are two of the precomputed creation palettes
        // (white and red): the ARGB the avatar renders with must follow the id
        // the client posted.
        var record = CapturedCharacter();

        new PlayerVisualLoadout
        {
            SkinColorId = 76714,
            EyeColorId = 76715,
            HairColorId = 76714,
            FacialHairColorId = 76715
        }.ApplyTo(record);

        Assert.Equal(0xffffffffu, record.Visuals.SkinColor);
        Assert.Equal(0xffff0000u, record.Visuals.EyeColor);
        Assert.Equal(0xffffffffu, record.Visuals.HairColor);
        Assert.Equal(0xffff0000u, record.Visuals.FacialHairColor);
    }

    [Fact]
    public void ApplyTo_KeepsTheStoredColorWhenThePaletteCannotBeResolved()
    {
        // A zero or unknown palette id means "no color information"; the record
        // keeps what it had instead of losing its face colors.
        var record = CapturedCharacter();
        record.Visuals.SkinColor = 0x12345678;
        record.Visuals.EyeColor = 0x9abcdef0;

        new PlayerVisualLoadout
        {
            SkinColorId = 0,
            EyeColorId = 999999
        }.ApplyTo(record);

        Assert.Equal(0x12345678u, record.Visuals.SkinColor);
        Assert.Equal(0x9abcdef0u, record.Visuals.EyeColor);
    }

    [Fact]
    public void ApplyTo_RoundTripsWhatFromRead()
    {
        // Reading the loadout and applying it back must not change the record:
        // this is what happens when the player opens the New You screen and
        // saves without changing anything.
        var record = CapturedCharacter();
        var original = PlayerVisualLoadout.From(record);

        original.ApplyTo(record);

        Assert.Equal(original.Gender, (int)record.Gender);
        Assert.Equal(original.Race, (int)record.Race);
        Assert.Equal(original.SkinColorId, record.Visuals.SkinColorId);
        Assert.Equal(original.VoiceSetId, record.Visuals.VoiceSet);
        Assert.Equal(original.HeadId, record.Visuals.Head);
        Assert.Equal(original.EyeId, record.Visuals.Eyes);
        Assert.Equal(original.EyeColorId, record.Visuals.EyeColorId);
        Assert.Equal(original.LipColorId, record.Visuals.LipColorId);
        Assert.Equal(original.HairColorId, record.Visuals.HairColorId);
        Assert.Equal(original.FacialHairColorId, record.Visuals.FacialHairColorId);
        Assert.Equal(Ids(original.HeadAccessories), Ids(PlayerVisualLoadout.From(record).HeadAccessories));
        Assert.Equal(Ids(original.Ornaments), Ids(PlayerVisualLoadout.From(record).Ornaments));
    }

    private static uint[] Ids(List<RemoteItem> items)
    {
        return items.Select(item => item.RemoteId).ToArray();
    }

    private static RemoteItem Remote(uint id)
    {
        return new RemoteItem { RemoteId = id };
    }
}
