using System;
using System.Collections.Generic;
using WebHost.ClientApi.Characters.Models;
using Shared.Common;
using WebHost.ClientApi.Models.Base;

namespace WebHost.ClientApi.Characters;

public class CharactersRepository : ICharactersRepository
{
    public CharactersList GetCharacters()
    {
        return new CharactersList
               {
                   Characters = new List<Character>
                                {
                                    GetCharacter("M22 Homecoming", 0x99aabbccddee0000 + 1181),
                                    GetCharacter("M20 Razor Edge", 0x99aabbccddee0000 + 833),
                                    GetCharacter("M19 Gatecrasher", 0x99aabbccddee0000 + 1171),
                                    GetCharacter("M18 Vagrant Dawn", 0x99aabbccddee0000 + 1007),
                                    GetCharacter("M17 SOS", 0x99aabbccddee0000 + 1151),
                                    GetCharacter("M16 Unearthed", 0x99aabbccddee0000 + 864),
                                    GetCharacter("M15 Agrievan", 0x99aabbccddee0000 + 803),
                                    GetCharacter("M14 Icebreaker", 0x99aabbccddee0000 + 1008),
                                    GetCharacter("M13 Accelerate", 0x99aabbccddee0000 + 1154),
                                    GetCharacter("M12 Prison Break", 0x99aabbccddee0000 + 1155),
                                    GetCharacter("M11 Consequence", 0x99aabbccddee0000 + 1114),
                                    GetCharacter("M10 Off the Grid", 0x99aabbccddee0000 + 1106),
                                    GetCharacter("M09 Taken", 0x99aabbccddee0000 + 1099),
                                    GetCharacter("M08 Catch", 0x99aabbccddee0000 + 1134),
                                    GetCharacter("M07 Trespass", 0x99aabbccddee0000 + 1101),
                                    GetCharacter("M06 Safehouse", 0x99aabbccddee0000 + 1113),
                                    GetCharacter("M05 No Exit", 0x99aabbccddee0000 + 1117),
                                    GetCharacter("M04 Razorwind", 0x99aabbccddee0000 + 1102),
                                    GetCharacter("M03 Crash Down", 0x99aabbccddee0000 + 1003),
                                    GetCharacter("M02 Bathsheba", 0x99aabbccddee0000 + 1104),
                                    GetCharacter("M01 Shadow", 0x99aabbccddee0000 + 1100),
                                    GetCharacter("OP3 ARES Team", 0x99aabbccddee0000 + 1089),
                                    GetCharacter("OP2 High Tide", 0x99aabbccddee0000 + 1093),
                                    GetCharacter("OP1 Miru", 0x99aabbccddee0000 + 1069),
                                    GetCharacter("TDM Refinery", 0x99aabbccddee0000 + 1147),
                                    GetCharacter("Omnidyne-M Stadium", 0x99aabbccddee0000 + 844),
                                    GetCharacter("Holdout Jericho", 0x99aabbccddee0000 + 1163),
                                    GetCharacter("R1 Defense of Dredge", 0x99aabbccddee0000 + 1173),
                                    GetCharacter("Epicenter Melding Tornado", 0x99aabbccddee0000 + 805),
                                    GetCharacter("Abyss Melding Tornado", 0x99aabbccddee0000 + 865),
                                    GetCharacter("Cinerarium", 0x99aabbccddee0000 + 868),
                                    GetCharacter("Danger Room", 0x99aabbccddee0000 + 1162),
                                    GetCharacter("Baneclaw Lair", 0x99aabbccddee0000 + 1051),
                                    GetCharacter("Battlelab", 0x99aabbccddee0000 + 1125),
                                    GetCharacter("Nothing", 0x99aabbccddee0000 + 12),
                                    GetCharacter("Diamond Head", 0x99aabbccddee0000 + 162),
                                    GetCharacter("Sertao", 0x99aabbccddee0000 + 1030),
                                    GetCharacter("New Eden", 0x99aabbccddee0000 + 448)
                                },
                   IsDev = false,
                   RbBalance = 0,
                   NameChangeCost = 100
               };
    }

    private static Character GetCharacter(string name, ulong guid)
    {
        return new Character
               {
                   CharacterGuid = guid,
                   Name = name,
                   UniqueName = "Ascendant",
                   IsDev = false,
                   IsActive = true,
                   CreatedAt = new DateTime(2017, 1, 3, 23, 41, 26),
                   TitleId = DefaultCharacterTemplate.TitleId,
                   TimePlayedSecs = 500,
                   NeedsNameChange = false,
                   MaxFrameLevel = DefaultCharacterTemplate.MaxFrameLevel,
                   FrameSdbId = (int)DefaultCharacterTemplate.FrameSdbId,
                   CurrentLevel = DefaultCharacterTemplate.CurrentLevel,
                   Gender = (int)DefaultCharacterTemplate.Gender,
                   CurrentGender = DefaultCharacterTemplate.Gender == 1 ? "female" : "male",
                   EliteRank = 95487,
                   LastSeenAt = DateTime.Now - TimeSpan.FromDays(365),
                   Visuals = new Visuals
                             {
                                 Id = 0,
                                 Race = (int)DefaultCharacterTemplate.Race,
                                 Gender = (int)DefaultCharacterTemplate.Gender,
                                 SkinColor = new ColoredItem { Id = (int)DefaultCharacterTemplate.SkinColorItemId, Value = new ColorValue { Color = (long)DefaultCharacterTemplate.SkinColor } },
                                 VoiceSet = new Item { Id = (int)DefaultCharacterTemplate.VoiceSetId },
                                 Head = new Item { Id = (int)DefaultCharacterTemplate.HeadId },
                                 EyeColor = new ColoredItem { Id = (int)DefaultCharacterTemplate.EyeColorItemId, Value = new ColorValue { Color = (long)DefaultCharacterTemplate.EyeColor } },
                                 LipColor = new ColoredItem { Id = (int)DefaultCharacterTemplate.LipColorItemId, Value = new ColorValue { Color = (long)DefaultCharacterTemplate.LipColor } },
                                 HairColor = new ColoredItem { Id = (int)DefaultCharacterTemplate.HairColorItemId, Value = new ColorValue { Color = (long)DefaultCharacterTemplate.HairColor } },
                                 FacialHairColor = new ColoredItem { Id = (int)DefaultCharacterTemplate.FacialHairColorItemId, Value = new ColorValue { Color = (long)DefaultCharacterTemplate.FacialHairColor } },
                                 HeadAccessories = new List<ColoredItem> { new() { Id = (int)DefaultCharacterTemplate.HeadAccessoryId, Value = new ColorValue { Color = (long)DefaultCharacterTemplate.HeadAccessoryColor } } },
                                 Ornaments = new List<ColoredItem>(),
                                 Eyes = new Item { Id = (int)DefaultCharacterTemplate.EyesId },
                                 Hair = new HairItem { Id = (int)DefaultCharacterTemplate.HairId, Color = new ColorItem { Id = (int)DefaultCharacterTemplate.HairColorItemId, Value = (long)DefaultCharacterTemplate.HairColor } },
                                 FacialHair = new HairItem { Id = (int)DefaultCharacterTemplate.FacialHairId, Color = new ColorItem { Id = 77187, Value = 1518862368 } },
                                 Glider = new Item { Id = (int)DefaultCharacterTemplate.GliderId },
                                 Vehicle = new Item { Id = (int)DefaultCharacterTemplate.VehicleId },
                                 Decals = new List<ColoredTransformableSdbItem>(),
                                 WarpaintId = DefaultCharacterTemplate.WarpaintId,
                                 Warpaint = new List<long>
                                            {
                                                4216738474,
                                                0,
                                                4216717312,
                                                418250752,
                                                1525350400,
                                                4162844703,
                                                4162844703
                                            },
                                 Decalgradients = new List<long>(),
                                 WarpaintPatterns = new List<WarpaintPattern>(),
                                 VisualOverrides = new List<long>()
                             },
                   Gear = new List<Gear>
                          {
                              new() { SlotTypeId = 1, SdbId = 86969, ItemGuid = 5068916056568384765 },
                              new() { SlotTypeId = 2, SdbId = 87918, ItemGuid = 5068916056568385021 },
                              new() { SlotTypeId = 6, SdbId = 91770, ItemGuid = 5068923373180718589 },
                              new() { SlotTypeId = 116, SdbId = 126000, ItemGuid = 5068916056568385277 },
                              new() { SlotTypeId = 122, SdbId = 129359, ItemGuid = 5068916056568385533 },
                              new() { SlotTypeId = 126, SdbId = 127501, ItemGuid = 5068916056568385789 },
                              new() { SlotTypeId = 127, SdbId = 128271, ItemGuid = 5068916056568386045 },
                              new() { SlotTypeId = 128, SdbId = 126731, ItemGuid = 5068916056568386301 },
                              new() { SlotTypeId = 129, SdbId = 129067, ItemGuid = 5068916056568386557 }
                          },
                   ExpiresIn = 0,
                   Race = "chosen",
                   Migrations = new List<int>()
               };
    }
}