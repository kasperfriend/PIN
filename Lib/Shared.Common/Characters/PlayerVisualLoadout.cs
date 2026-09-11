using System.Collections.Generic;
using System.Linq;

namespace Shared.Common.Characters;

/// <summary>
/// A character's equipped appearance as the client's New You / garage
/// customization screen reads it (<c>GET api/v2/characters/{guid}/visual_loadouts</c>
/// on the ClientApi host).
///
/// Only item ids are sent — the client resolves the colors from its static
/// database by palette id — and, like the character list, the hair and facial
/// hair meshes are the leading head accessories
/// (<see cref="CharacterAppearance.HeadAccessoryMeshes"/>): a real captured
/// character has <c>head_accessories[0] == hair</c> and
/// <c>head_accessories[1] == facial_hair</c>. The property set matches the live
/// service's response (captured 2015-05-02, build 1869) and serializes to the
/// same snake_case keys through the web hosts' naming policy.
/// </summary>
public class PlayerVisualLoadout
{
    public int Id { get; set; }

    public ulong CharacterGuid { get; set; }

    public int Gender { get; set; }

    public int Race { get; set; }

    public uint SkinColorId { get; set; }

    public uint VoiceSetId { get; set; }

    public uint HeadId { get; set; }

    public uint EyeId { get; set; }

    public uint EyeColorId { get; set; }

    public uint LipColorId { get; set; }

    public uint HairColorId { get; set; }

    public uint FacialHairColorId { get; set; }

    public List<RemoteItem> HeadAccessories { get; set; } = [];

    public List<RemoteItem> Ornaments { get; set; } = [];

    /// <summary>
    /// Build the loadout the New You screen shows for <paramref name="record"/>.
    /// </summary>
    public static PlayerVisualLoadout From(CharacterRecord record)
    {
        var visuals = record.Visuals;

        return new PlayerVisualLoadout
               {
                   Id = 0,
                   CharacterGuid = record.CharacterGuid,
                   Gender = (int)record.Gender,
                   Race = (int)record.Race,
                   SkinColorId = visuals.SkinColorId,
                   VoiceSetId = visuals.VoiceSet,
                   HeadId = visuals.Head,
                   EyeId = visuals.Eyes,
                   EyeColorId = visuals.EyeColorId,
                   LipColorId = visuals.LipColorId,
                   HairColorId = visuals.HairColorId,
                   FacialHairColorId = visuals.FacialHairColorId,
                   HeadAccessories = CharacterAppearance.HeadAccessoryMeshes(visuals)
                                                      .Select(Remote)
                                                      .ToList(),
                   Ornaments = visuals.Ornaments.Select(Remote).ToList()
               };
    }

    /// <summary>
    /// Apply the loadout the client posts back after editing the character at a
    /// New You terminal (the save request) onto <paramref name="record"/>.
    ///
    /// The client posts the same ids it read; the color values are resolved
    /// through <see cref="CharacterColorPalettes"/> — the precomputed palette
    /// table character creation also uses — and a palette that cannot be
    /// resolved (zero or unknown id) leaves the color the record already had
    /// instead of blanking it. The first two head accessories are the hair and
    /// facial hair meshes, the inverse of
    /// <see cref="CharacterAppearance.HeadAccessoryMeshes"/>.
    /// </summary>
    public void ApplyTo(CharacterRecord record)
    {
        var visuals = record.Visuals;

        record.Gender = Gender < 0 ? 0u : (uint)Gender;
        record.Race = Race < 0 ? 0u : (uint)Race;

        visuals.VoiceSet = VoiceSetId;
        visuals.Head = HeadId;
        visuals.Eyes = EyeId;

        visuals.SkinColorId = SkinColorId;
        visuals.EyeColorId = EyeColorId;
        visuals.LipColorId = LipColorId;
        visuals.HairColorId = HairColorId;
        visuals.FacialHairColorId = FacialHairColorId;

        visuals.SkinColor = ResolveColor(SkinColorId, visuals.SkinColor);
        visuals.EyeColor = ResolveColor(EyeColorId, visuals.EyeColor);
        visuals.LipColor = ResolveColor(LipColorId, visuals.LipColor);
        visuals.HairColor = ResolveColor(HairColorId, visuals.HairColor);
        visuals.FacialHairColor = ResolveColor(FacialHairColorId, visuals.FacialHairColor);

        var meshes = HeadAccessories == null
                         ? new List<uint>()
                         : HeadAccessories.Where(item => item.RemoteId != 0).Select(item => item.RemoteId).ToList();

        visuals.Hair = meshes.Count > 0 ? meshes[0] : 0;
        visuals.FacialHair = meshes.Count > 1 ? meshes[1] : 0;
        visuals.HeadAccessories = meshes.Skip(2).ToList();

        visuals.Ornaments = Ornaments == null
                                ? new List<uint>()
                                : Ornaments.Where(item => item.RemoteId != 0).Select(item => item.RemoteId).ToList();
    }

    private static RemoteItem Remote(uint id)
    {
        return new RemoteItem { RemoteId = id };
    }

    private static uint ResolveColor(uint paletteId, uint fallback)
    {
        return paletteId != 0 && CharacterColorPalettes.TryGet(paletteId, out var color) ? color : fallback;
    }
}

/// <summary>
/// A reference to an SDB item, which the client sends and receives as
/// <c>{"remote_id": id}</c>.
/// </summary>
public class RemoteItem
{
    public uint RemoteId { get; set; }
}
