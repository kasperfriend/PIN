using System.Collections.Generic;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireWeaponTemplateCommandDef</c>: gates on the weapon the character currently
///     holds being of the row's template (weapon type). The loadout slot the question refers to is
///     selected by the replicated weapon index (1 = primary, 2 = secondary, anything else means no
///     weapon in hand); the item id then maps through <c>dbitems::Weapons.WeaponTypeId</c>, the
///     same link the NPC attack data source uses. Params.Negate mirrors the requirement.
/// </summary>
public class RequireWeaponTemplateCommand : Command, ICommand
{
    private RequireWeaponTemplateCommandDef Params;

    public RequireWeaponTemplateCommand(RequireWeaponTemplateCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        var character = CharacterRequirement.Find(context, false);
        if (character?.CurrentLoadout != null)
        {
            uint weaponItemId = character.WeaponIndex.Index switch
            {
                1 => character.CurrentLoadout.SlottedItems.GetValueOrDefault(Data.LoadoutSlotType.Primary),
                2 => character.CurrentLoadout.SlottedItems.GetValueOrDefault(Data.LoadoutSlotType.Secondary),
                _ => 0,
            };

            if (weaponItemId != 0)
            {
                var weapon = SDBInterface.GetWeapon(weaponItemId);
                result = weapon != null && weapon.WeaponTypeId == Params.WeaponTemplateId;
            }
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
