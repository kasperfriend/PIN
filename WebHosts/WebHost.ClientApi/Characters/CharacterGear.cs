using System.Collections.Generic;
using System.Linq;
using Shared.Common.Characters;
using WebHost.ClientApi.Characters.Models;

namespace WebHost.ClientApi.Characters;

/// <summary>
/// What a character owns, as the web API describes it: the battleframe its
/// record carries and the modules that frame ships with for a player
/// (<see cref="ChassisStockLoadouts"/>).
///
/// PIN has no persisted inventory — the GameServer builds one from the static
/// database when a character zones in — so the web hosts describe a character
/// from its record plus the precomputed stock loadout of its frame. That is
/// enough for everything the client asks the web API for (the gear on a
/// character card, the equipped items, the garage) and it is the same gear the
/// GameServer equips, so the selection screen and the in-game character do not
/// drift apart.
///
/// It replaces the hardcoded answers these endpoints used to give: one fixed
/// Recon loadout on every character card, one operator bag for everybody and a
/// Firecat in every garage — constants captured from a single session, which
/// read as "a new account owns the admin's gear".
/// </summary>
public static class CharacterGear
{
    /// <summary>Type code the original service reports for a piece of worn gear.</summary>
    private const uint GearTypeCode = 244;

    /// <summary>
    /// Prefix of the derived item guids: it keeps them in the positive range of
    /// the <c>long</c> the models serialize, away from the values a real service
    /// would have stored.
    /// </summary>
    private const ulong ItemGuidPrefix = 0x5000000000000000UL;

    /// <summary>
    /// The equipped modules of the character's battleframe: the stock loadout of
    /// the chassis its record carries, filtered to the slots a character wears
    /// (<see cref="StockLoadoutSlots"/>) and ordered by slot. Empty for a chassis
    /// the stock loadout table does not know, which is a character that owns
    /// nothing but its frame.
    /// </summary>
    /// <param name="character">The character, or null.</param>
    public static IReadOnlyList<StockLoadoutSlot> Slots(CharacterRecord character)
    {
        return StockLoadoutSlots.GearFor(character?.CurrentBattleframeSDBId ?? 0);
    }

    /// <summary>
    /// The character's worn gear as the character list reports it
    /// (<c>api/v2/characters/list</c>): one entry per equipped module, with the
    /// slot type and item id the client resolves the preview from.
    /// </summary>
    /// <param name="character">The character, or null.</param>
    public static List<Gear> Gear(CharacterRecord character)
    {
        var characterGuid = character?.CharacterGuid ?? 0;

        return Slots(character)
               .Where(slot => slot.PveModule != 0)
               .Select(slot => new Gear
                               {
                                   SlotTypeId = slot.SlotType,
                                   SdbId = (int)slot.PveModule,
                                   ItemGuid = (long)ItemGuid(characterGuid, slot.SlotType)
                               })
               .ToList();
    }

    /// <summary>
    /// The character's worn gear as the gear inventory reports it
    /// (<c>api/v3/characters/{id}/inventories/gear/items</c>): the same modules,
    /// in the item shape that endpoint answers.
    /// </summary>
    /// <param name="character">The character, or null.</param>
    public static object[] InventoryItems(CharacterRecord character)
    {
        var characterGuid = character?.CharacterGuid ?? 0;

        return Slots(character)
               .Where(slot => slot.PveModule != 0)
               .Select(slot => new Items
                               {
                                   ItemId = ItemGuid(characterGuid, slot.SlotType),
                                   ItemSdbId = slot.PveModule,
                                   OwnerGuid = characterGuid,
                                   TypeCode = GearTypeCode,
                                   CharacterGuid = characterGuid,
                                   Durability = new Durability { Current = 1000, Pool = 0 }
                               })
               .Cast<object>()
               .ToArray();
    }

    /// <summary>
    /// The item guid a character reports for one slot of its gear.
    ///
    /// There is no persisted inventory, so an item has no server side instance
    /// guid — the original service answered the guid of the row it stored. This
    /// derives a stable stand-in from the character guid and the slot instead, so
    /// the same character always reports the same ids (the client carries them
    /// through its UI and hands them back).
    /// </summary>
    /// <param name="characterGuid">The character the item belongs to.</param>
    /// <param name="slotType">The slot the item sits in.</param>
    public static ulong ItemGuid(ulong characterGuid, byte slotType)
    {
        return DeriveItemGuid(characterGuid, slotType);
    }

    /// <summary>
    /// The item guid a character reports for the battleframe in its garage: a
    /// frame is gear in its own right, keyed by its chassis id instead of a
    /// slot.
    /// </summary>
    /// <param name="characterGuid">The character the frame belongs to.</param>
    /// <param name="chassisId">The battleframe (chassis) SDB id.</param>
    public static ulong ChassisItemGuid(ulong characterGuid, uint chassisId)
    {
        return DeriveItemGuid(characterGuid, (byte)(chassisId & 0xff));
    }

    /// <summary>
    /// A guid that is stable per character and per discriminator byte, so two
    /// slots of one character never collide and a character's ids never change.
    /// </summary>
    private static ulong DeriveItemGuid(ulong characterGuid, byte discriminator)
    {
        return ItemGuidPrefix | ((characterGuid & 0x0000ffffffffffffUL) ^ ((ulong)discriminator << 48));
    }
}
