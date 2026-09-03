using System.Collections.Generic;
using System.Linq;
using Shared.Common.Characters;
using WebHost.ClientApi.Characters.Models;
using WebHost.ClientApi.Models.Base;

namespace WebHost.ClientApi.Characters;

/// <summary>
/// Serves the character selection screen from the shared <see cref="CharacterStore"/>,
/// the same store the GameServer reads over GRPC. Keeping both on one source of
/// truth is what stops the selected character and the in-game character diverging.
/// </summary>
public class CharactersRepository : ICharactersRepository
{
    public CharactersList GetCharacters()
    {
        CharacterStore.Init();

        return new CharactersList
               {
                   Characters = CharacterStore.GetAll().Select(ToApiCharacter).ToList(),
                   IsDev = false,
                   RbBalance = 0,
                   NameChangeCost = 100
               };
    }

    private static Character ToApiCharacter(CharacterRecord record)
    {
        var visuals = record.Visuals;

        return new Character
               {
                   CharacterGuid = record.CharacterGuid,
                   Name = record.Name,
                   UniqueName = "Ascendant",
                   IsDev = false,
                   IsActive = true,
                   CreatedAt = record.CreatedAt,
                   TitleId = record.TitleId,
                   TimePlayedSecs = (int)record.TimePlayed,
                   NeedsNameChange = false,
                   MaxFrameLevel = record.MaxFrameLevel,
                   FrameSdbId = (int)record.CurrentBattleframeSDBId,
                   CurrentLevel = record.CurrentLevel,
                   Gender = (int)record.Gender,
                   CurrentGender = record.Gender == 1 ? "female" : "male",
                   EliteRank = 95487,
                   LastSeenAt = record.LastSeenAt,
                   Visuals = new Visuals
                             {
                                 Id = 0,
                                 Race = (int)record.Race,
                                 Gender = (int)record.Gender,
                                 SkinColor = Colored(visuals.SkinColorId, visuals.SkinColor),
                                 VoiceSet = new Item { Id = (int)visuals.VoiceSet },
                                 Head = new Item { Id = (int)visuals.Head },
                                 EyeColor = Colored(visuals.EyeColorId, visuals.EyeColor),
                                 LipColor = Colored(visuals.LipColorId, visuals.LipColor),
                                 HairColor = Colored(visuals.HairColorId, visuals.HairColor),
                                 FacialHairColor = Colored(visuals.FacialHairColorId, visuals.FacialHairColor),
                                 HeadAccessories = visuals.HeadAccessories
                                                          .Select(id => Colored(id, visuals.HeadAccessoryColor))
                                                          .ToList(),
                                 Ornaments = visuals.Ornaments
                                                    .Select(id => Colored(id, 0))
                                                    .ToList(),
                                 Eyes = new Item { Id = (int)visuals.Eyes },
                                 Hair = new HairItem
                                        {
                                            Id = (int)visuals.Hair,
                                            Color = new ColorItem { Id = (int)visuals.HairColorId, Value = visuals.HairColor }
                                        },
                                 FacialHair = new HairItem
                                              {
                                                  Id = (int)visuals.FacialHair,
                                                  Color = new ColorItem { Id = (int)visuals.FacialHairColorId, Value = visuals.FacialHairColor }
                                              },
                                 Glider = new Item { Id = (int)visuals.Glider },
                                 Vehicle = new Item { Id = (int)visuals.Vehicle },
                                 Decals = new List<ColoredTransformableSdbItem>(),
                                 WarpaintId = visuals.WarpaintId,
                                 Warpaint = visuals.Warpaint.Select(v => (long)v).ToList(),
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

    private static ColoredItem Colored(uint id, uint color)
    {
        return new ColoredItem { Id = (int)id, Value = new ColorValue { Color = color } };
    }
}
