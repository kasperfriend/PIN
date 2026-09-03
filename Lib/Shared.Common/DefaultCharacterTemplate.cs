namespace Shared.Common;

/// <summary>
/// Single source of truth for the hardcoded default character.
///
/// This is used both by the web character list (the character selection screen)
/// and by the GameServer fallback that is applied when no character data can be
/// fetched over GRPC. Keeping them in one place makes sure that the character
/// you pick in the selection screen is the character you actually play as.
/// </summary>
public static class DefaultCharacterTemplate
{
    /// <summary>Gender of the default character. 0 = Male, 1 = Female.</summary>
    public const uint Gender = 1;

    /// <summary>Race of the default character. 0 = Human.</summary>
    public const uint Race = 0;

    /// <summary>Displayed title.</summary>
    public const ushort TitleId = 0;

    /// <summary>Battleframe (chassis) SDB id. 76334 = Raptor.</summary>
    public const uint FrameSdbId = 76334;

    /// <summary>Character level shown in the selection screen.</summary>
    public const int CurrentLevel = 10;

    /// <summary>Max battleframe level shown in the selection screen.</summary>
    public const int MaxFrameLevel = 10;

    // Visuals
    public const uint HeadId = 10026;
    public const uint EyesId = 10001;
    public const uint VoiceSetId = 1033;
    public const uint GliderId = 0;
    public const uint VehicleId = 0;

    public const uint HairId = 10113;
    public const uint FacialHairId = 0;

    public const uint SkinColorItemId = 118969;
    public const uint EyeColorItemId = 118980;
    public const uint LipColorItemId = 1;
    public const uint HairColorItemId = 77193;
    public const uint FacialHairColorItemId = 77193;

    public const uint SkinColor = 4294930822;
    public const uint EyeColor = 1633685600;
    public const uint LipColor = 4294903873;
    public const uint HairColor = 1917780001;
    public const uint FacialHairColor = 1917780001;

    public const uint HeadAccessoryId = 10117;
    public const uint HeadAccessoryColor = 1211031763;

    public const int WarpaintId = 143225;

    /// <summary>Head accessories worn by the default character.</summary>
    public static uint[] HeadAccessories => [HeadAccessoryId];

    /// <summary>Ornaments worn by the default character.</summary>
    public static uint[] Ornaments => [];
}
