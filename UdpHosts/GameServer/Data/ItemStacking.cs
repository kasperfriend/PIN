using GameServer.Enums;

namespace GameServer.Data;

/// <summary>
///     Which item types are held as stackable resource pools rather than as guid items.
///     A live bag model (captured <c>BagInventoryUpdate</c> payloads) carries consumables,
///     currency-style basics and raw materials as guidless stacks of a quantity, while equipment
///     (weapons, chassis, modules) keeps one guid per copy - so the server mirrors that split
///     whenever it grants goods, or the client's stacks stop stacking.
/// </summary>
public static class ItemStacking
{
    /// <summary>Whether an item of this <see cref="ItemType" /> lives in a resource pool.</summary>
    public static bool IsStackedAsResource(ItemType type) =>
        type is ItemType.Basic or ItemType.Consumable or ItemType.ResourceItem;

    /// <summary>Whether an item of this <c>dbitems::RootItem.type</c> lives in a resource pool.</summary>
    public static bool IsStackedAsResource(byte itemType) => IsStackedAsResource((ItemType)itemType);
}
