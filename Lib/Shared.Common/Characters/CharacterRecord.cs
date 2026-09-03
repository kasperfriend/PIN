using System;
using System.Collections.Generic;

namespace Shared.Common.Characters;

/// <summary>
/// A persisted player character.
///
/// This is the single record that both the web character list (selection screen)
/// and the GameServer (via GRPC) read from, so the character you pick is the
/// character you spawn as.
/// </summary>
public class CharacterRecord
{
    public ulong CharacterGuid { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Position in the character selection list. Lower sorts first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Zone this entry loads into. Derived from the low 16 bits of the guid.</summary>
    public uint ZoneId => (uint)(CharacterGuid & 0xffff);

    public uint Gender { get; set; } = DefaultCharacterTemplate.Gender;

    public uint Race { get; set; } = DefaultCharacterTemplate.Race;

    public ushort TitleId { get; set; } = DefaultCharacterTemplate.TitleId;

    /// <summary>Battleframe (chassis) SDB id the player last selected.</summary>
    public uint CurrentBattleframeSDBId { get; set; } = DefaultCharacterTemplate.FrameSdbId;

    public int CurrentLevel { get; set; } = DefaultCharacterTemplate.CurrentLevel;

    public int MaxFrameLevel { get; set; } = DefaultCharacterTemplate.MaxFrameLevel;

    public string ArmyTag { get; set; } = "ARMY";

    public ulong ArmyGuid { get; set; } = 1u;

    public bool ArmyIsOfficer { get; set; } = true;

    public uint LastZoneId { get; set; }

    public uint LastOutpostId { get; set; }

    public uint TimePlayed { get; set; }

    public DateTime CreatedAt { get; set; } = new DateTime(2017, 1, 3, 23, 41, 26, DateTimeKind.Utc);

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public CharacterVisualsRecord Visuals { get; set; } = new();
}

/// <summary>
/// Appearance of a persisted character. Defaults mirror <see cref="DefaultCharacterTemplate"/>.
/// </summary>
public class CharacterVisualsRecord
{
    public uint Head { get; set; } = DefaultCharacterTemplate.HeadId;

    public uint Eyes { get; set; } = DefaultCharacterTemplate.EyesId;

    public uint VoiceSet { get; set; } = DefaultCharacterTemplate.VoiceSetId;

    public uint Glider { get; set; } = DefaultCharacterTemplate.GliderId;

    public uint Vehicle { get; set; } = DefaultCharacterTemplate.VehicleId;

    public uint Hair { get; set; } = DefaultCharacterTemplate.HairId;

    public uint FacialHair { get; set; } = DefaultCharacterTemplate.FacialHairId;

    public uint SkinColorId { get; set; } = DefaultCharacterTemplate.SkinColorItemId;

    public uint EyeColorId { get; set; } = DefaultCharacterTemplate.EyeColorItemId;

    public uint LipColorId { get; set; } = DefaultCharacterTemplate.LipColorItemId;

    public uint HairColorId { get; set; } = DefaultCharacterTemplate.HairColorItemId;

    public uint FacialHairColorId { get; set; } = DefaultCharacterTemplate.FacialHairColorItemId;

    public uint SkinColor { get; set; } = DefaultCharacterTemplate.SkinColor;

    public uint EyeColor { get; set; } = DefaultCharacterTemplate.EyeColor;

    public uint LipColor { get; set; } = DefaultCharacterTemplate.LipColor;

    public uint HairColor { get; set; } = DefaultCharacterTemplate.HairColor;

    public uint FacialHairColor { get; set; } = DefaultCharacterTemplate.FacialHairColor;

    public List<uint> HeadAccessories { get; set; } = [DefaultCharacterTemplate.HeadAccessoryId];

    public uint HeadAccessoryColor { get; set; } = DefaultCharacterTemplate.HeadAccessoryColor;

    public List<uint> Ornaments { get; set; } = [];

    public int WarpaintId { get; set; } = DefaultCharacterTemplate.WarpaintId;

    public List<uint> Warpaint { get; set; } =
    [
        4216738474,
        0,
        4216717312,
        418250752,
        1525350400,
        4162844703,
        4162844703
    ];
}
