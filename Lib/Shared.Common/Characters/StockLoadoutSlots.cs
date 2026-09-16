using System.Collections.Generic;
using System.Linq;

namespace Shared.Common.Characters;

/// <summary>
/// The part of a <see cref="ChassisStockLoadout"/> a character actually wears.
///
/// A char-create loadout in the static database fills more slots than the
/// GameServer equips: next to the weapons, the abilities, the backpack and the
/// chassis gear it also carries legacy slot types (3, 4, 5, 10) that no longer
/// mean anything to the client. The web hosts answer the character selection
/// screen and the gear/bag inventories out of the precomputed table (they
/// cannot read the SDB at runtime), and they have to describe the same
/// character the GameServer builds, so both filter the loadout the same way.
/// </summary>
public static class StockLoadoutSlots
{
    /// <summary>
    /// The slot types the GameServer equips from a char-create loadout: the
    /// weapon slots, the ability slots, the chassis gear slots (mirroring
    /// <c>CharacterLoadout.LoadoutWeaponSlots</c>, <c>LoadoutAbilitySlots</c> and
    /// <c>LoadoutChassisSlots</c>) plus the backpack — which the GameServer
    /// resolves separately, from the chassis' own char-create loadout
    /// (<c>SDBUtils.GetChassisDefaultBackpack</c>), but which the character
    /// wears just the same. The numbering is the SDB's <c>slot_type</c> and the
    /// GameServer's <c>LoadoutSlotType</c>: 1 primary, 2 secondary, 6 HKM,
    /// 7..9 abilities, 11 backpack, 116/122..137 chassis gear.
    /// </summary>
    private static readonly HashSet<byte> EquippedSlotTypes =
    [
        1, 2,                       // weapons
        6, 7, 8, 9,                 // HKM and the three abilities
        11,                         // backpack
        116, 122, 123, 124,         // torso, aux weapon, medical system, head
        126, 127, 128, 129, 130, 137 // arms, legs, reactor, OS, gadgets
    ];

    /// <summary>Whether the GameServer equips this slot type from a char-create loadout.</summary>
    /// <param name="slotType">The SDB's <c>slot_type</c> / the GameServer's <c>LoadoutSlotType</c>.</param>
    public static bool IsEquipped(byte slotType)
    {
        return EquippedSlotTypes.Contains(slotType);
    }

    /// <summary>
    /// The modules of a stock loadout the character wears, in slot order. An
    /// empty list when the loadout is null (a chassis the table does not know).
    /// </summary>
    /// <param name="loadout">The chassis' stock loadout, or null.</param>
    public static IReadOnlyList<StockLoadoutSlot> EquippedSlots(ChassisStockLoadout loadout)
    {
        if (loadout == null)
        {
            return [];
        }

        return loadout.Slots.Where(slot => IsEquipped(slot.SlotType)).ToList();
    }

    /// <summary>
    /// The gear a character of <paramref name="chassisId"/> wears: the equipped
    /// modules of that chassis' stock loadout, or an empty list for a chassis
    /// the table does not know (such a character owns its bare frame only).
    /// </summary>
    /// <param name="chassisId">The battleframe (chassis) SDB id.</param>
    public static IReadOnlyList<StockLoadoutSlot> GearFor(uint chassisId)
    {
        return ChassisStockLoadouts.TryGet(chassisId, out var loadout) ? EquippedSlots(loadout) : [];
    }
}
