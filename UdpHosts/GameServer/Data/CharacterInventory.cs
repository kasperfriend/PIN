using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;
using LoadoutVisualType = AeroMessages.GSS.Character.LoadoutConfig_Visual.LoadoutVisualType;

namespace GameServer.Data;

public class CharacterInventory
{
    private static readonly ILogger _logger = Log.ForContext<CharacterEntity>();

    private readonly Dictionary<ulong, Item> _items; // By guid
    private readonly Dictionary<uint, Resource> _resources; // By typeid
    private readonly Dictionary<int, Loadout> _loadouts; // By loadoutid

    private readonly IShard _shard;
    private readonly INetworkClient _player;
    private readonly CharacterEntity _character;

    public CharacterInventory(IShard shard, INetworkClient player, CharacterEntity character)
    {
        _shard = shard;
        _player = player;
        _character = character;
        _items = [];
        _resources = [];
        _loadouts = [];
    }

    public bool EnablePartialUpdates { get; set; }

    /// <summary>
    /// Load the dev sandbox inventory: every battleframe plus the captured item
    /// and resource dump. Admin accounts get this — it is the playground the
    /// zone-picker entries, the frame and level cheats and the operator's own
    /// testing are for — and so does a login whose character record could not be
    /// fetched over GRPC, because that is the only state PIN can prove nothing
    /// about. Every other character starts fresh (see
    /// <see cref="LoadStartingInventory"/>).
    /// </summary>
    public void LoadHardcodedInventory()
    {
        HardcodedCharacterData.LoadSandboxInventory(this);
    }

    /// <summary>
    /// Load the inventory a freshly created character starts its life with: the
    /// one battleframe the player picked on the character creation screen
    /// (<paramref name="chassisId"/>), wearing the modules that frame ships with
    /// for a player in the static database, and nothing else — no second frame,
    /// no item dump, no resource pools.
    /// </summary>
    /// <remarks>
    /// Before this existed every character was handed the dev sandbox on login,
    /// because the inventory was one hardcoded set shared by everyone: a brand
    /// new account opened the game owning all 17 frames (each in its endgame
    /// "Elite ... Reward" loadout) and a bag and wallet copied from the
    /// operator's. Character progression is not persisted yet, so this is also
    /// what a returning character of a non-admin account is equipped with —
    /// which is at least the frame its record says it wears.
    /// </remarks>
    /// <param name="chassisId">
    /// The character's battleframe (chassis) SDB id, or 0 for the fallback frame.
    /// </param>
    public void LoadStartingInventory(uint chassisId)
    {
        HardcodedCharacterData.GenerateStartingLoadout(this, chassisId);
    }

    public int GetLoadoutIdForChassis(uint chassisId)
    {
        foreach (var (loadoutId, loadout) in _loadouts)
        {
            if (loadout.ChassisID == chassisId)
            {
                return loadoutId;
            }
        }

        return 0;
    }

    /// <summary>
    /// Get a loadout from the inventory by loadoutId
    /// </summary>
    /// <param name="loadoutId">The id of the loadout to get</param>
    /// <returns>The loadout data as LoadoutReferenceData or null if the loadoutId was invalid</returns>
    public LoadoutReferenceData GetLoadoutReferenceData(int loadoutId)
    {
        if (!_loadouts.TryGetValue(loadoutId, out var loadout))
        {
            return null;
        }

        var refData = new LoadoutReferenceData()
                      {
                          LoadoutId = loadoutId,
                          ChassisId = loadout.ChassisID
                      };

        var pveConfig = loadout.LoadoutConfigs[0];
        var pvpConfig = loadout.LoadoutConfigs[1];
        foreach (var itemRef in pveConfig.Items)
        {
            var item = _items[itemRef.ItemGUID];
            refData.SlottedItemsPvE.Add((LoadoutSlotType)itemRef.SlotIndex, item.SdbId);
        }

        foreach (var itemRef in pvpConfig.Items)
        {
            var item = _items[itemRef.ItemGUID];
            refData.SlottedItemsPvP.Add((LoadoutSlotType)itemRef.SlotIndex, item.SdbId);
        }

        return refData;
    }

    /// <summary>
    /// Counts how many inventory entries carry the given item sdb id. Items are not stackable
    /// (stackable goods live in the resource store instead), so every matching entry counts once;
    /// this is what aptitude's RequireHasItem quantity refers to.
    /// </summary>
    public int CountItemsBySdbId(uint sdbId) => _items.Values.Count(item => item.SdbId == sdbId);

    /// <summary>Looks up a carried item by guid.</summary>
    public bool TryGetItem(ulong guid, out Item item) => _items.TryGetValue(guid, out item);

    /// <summary>Whether the item is currently equipped in a loadout; equipped gear cannot leave the inventory.</summary>
    public bool IsItemEquipped(ulong guid) =>
        _items.TryGetValue(guid, out var item) && (item.DynamicFlags & (byte)ItemDynamicFlags.IsEquipped) != 0;

    /// <summary>
    ///     Removes an item from the inventory. Equipped gear refuses to go (it is slotted in a
    ///     loadout, which would be left pointing at a missing item).
    /// </summary>
    /// <param name="guid">The item to remove.</param>
    /// <returns>False when no such item is carried or it is equipped.</returns>
    public bool RemoveItem(ulong guid)
    {
        if (IsItemEquipped(guid))
        {
            return false;
        }

        if (!_items.Remove(guid))
        {
            return false;
        }

        SendItemsRemoved();
        return true;
    }

    /// <summary>
    ///     Puts an item that <see cref="RemoveItem" /> or <see cref="ConsumeItemBySdbId" /> took out back, with
    ///     its old guid, so a rolled-back activation leaves the inventory as it found it (see
    ///     <c>ConsumeItemCommand</c>).
    /// </summary>
    public void RestoreItem(Item item)
    {
        _items[item.GUID] = item;
        SendItemUpdate(item.GUID);
        SendBagUpdate();
    }

    /// <summary>Whether the given good is at hand: a resource pool of that sdb id with enough in it, or at least that many free (unequipped) copies as items.</summary>
    public bool HasItemOrResource(uint sdbId, uint quantity = 1)
    {
        if (quantity == 0)
        {
            return true;
        }

        if (_resources.TryGetValue(sdbId, out var res) && res.Quantity >= quantity)
        {
            return true;
        }

        return CountUnequippedItems(sdbId) >= quantity;
    }

    /// <summary>
    ///     Takes one or more copies of a good out of the inventory: out of the resource pool when
    ///     it holds enough, otherwise that many unequipped items of the same sdb id. Pools are not
    ///     mixed - the removal is all-or-nothing from whichever pool covers the whole amount.
    /// </summary>
    /// <returns>False when the inventory does not hold the full amount; nothing is removed then.</returns>
    public bool ConsumeItemBySdbId(uint sdbId, uint quantity = 1) => ConsumeItemBySdbId(sdbId, quantity, out _);

    /// <summary>
    ///     <see cref="ConsumeItemBySdbId(uint, uint)" />, also reporting which guid items went - empty when the
    ///     amount came off the resource pool - so the caller can hand exactly those back on a rollback.
    /// </summary>
    public bool ConsumeItemBySdbId(uint sdbId, uint quantity, out List<Item> removedItems)
    {
        removedItems = [];
        if (quantity == 0)
        {
            return true;
        }

        if (_resources.TryGetValue(sdbId, out var res) && res.Quantity >= quantity)
        {
            return ConsumeResource(sdbId, quantity);
        }

        if (CountUnequippedItems(sdbId) < quantity)
        {
            return false;
        }

        removedItems = _items.Values
            .Where(item => item.SdbId == sdbId && (item.DynamicFlags & (byte)ItemDynamicFlags.IsEquipped) == 0)
            .Take((int)quantity)
            .ToList();
        foreach (var item in removedItems)
        {
            _items.Remove(item.GUID);
        }

        SendItemsRemoved();
        return true;
    }

    /// <summary>
    ///     How many slots the bags would show as occupied: one per carried item and one per
    ///     stackable pool, mirroring a captured <c>BagInventoryUpdate</c>. The client enforces
    ///     this against its bag model, so purchases into a full bag must be declined server-side
    ///     with the same math (see <c>NpcVendorService</c>).
    /// </summary>
    public int BagSlotCount => _items.Count + _resources.Count;

    /// <summary>The occupied bag slots, items first (with their guids), stackable pools after (guidless, with the stack size) - the order a capture shows.</summary>
    public List<BagInventoryLayout.BagSlot> GetBagSlots()
    {
        var slots = new List<BagInventoryLayout.BagSlot>(BagSlotCount);
        foreach (var item in _items.Values)
        {
            slots.Add(new BagInventoryLayout.BagSlot(item.GUID, item.SdbId, 1));
        }

        foreach (var resource in _resources.Values)
        {
            slots.Add(new BagInventoryLayout.BagSlot(0, resource.SdbId, resource.Quantity));
        }

        return slots;
    }

    private int CountUnequippedItems(uint sdbId) =>
        _items.Values.Count(item => item.SdbId == sdbId && (item.DynamicFlags & (byte)ItemDynamicFlags.IsEquipped) == 0);

    public ulong CreateItem(uint sdbId)
    {
        ulong guid = _shard.GetNextGuid((byte)GuidService.AdditionalTypes.Item);
        Item item = new Item()
        {
            SdbId = sdbId,
            GUID = guid,
            SubInventory = GetInventoryTypeByItemTypeId(sdbId),
            Durability = 1000,
            DynamicFlags = 0,
            TimestampEpoch = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Modules = [],
            Unk1 = 0,
            Unk3 = 0,
            Unk4 = 0,
            Unk5 = 0,
            Unk6 = [],
            Unk7 = 0,
        };

        _items.Add(guid, item);
        SendItemUpdate(guid);
        return guid;
    }

    public void AddResource(uint sdbId, uint quantity)
    {
        if (!_resources.ContainsKey(sdbId))
        {
            Resource resource = new Resource()
            {
                Quantity = 0,
                SdbId = sdbId,
                SubInventory = GetInventoryTypeByItemTypeId(sdbId),
                TextKey = string.Empty,
                Unk2 = 0,
            };

            _resources.Add(sdbId, resource);
        }

        var res = _resources[sdbId];
        bool newSlot = res.Quantity == 0;
        res.Quantity += quantity;
        _resources[sdbId] = res;
        SendResourceUpdate(sdbId);
        if (newSlot)
        {
            SendBagUpdate();
        }
    }

    public bool ConsumeResource(uint sdbId, uint cost)
    {
        if (!_resources.TryGetValue(sdbId, out var res))
        {
            return false;
        }

        if (res.Quantity < cost)
        {
            return false;
        }
        else
        {
            res.Quantity -= cost;

            if (res.Quantity > 0)
            {
                _resources[sdbId] = res;
                SendResourceUpdate(sdbId);
            }
            else
            {
                _resources.Remove(sdbId);
                SendResourceUpdate(sdbId);

                // The stack's bag slot is gone; the client's bag model only learns that from a new layout.
                SendBagUpdate();
            }

            return true;
        }
    }

    public uint GetResourceQuantity(uint sdbId)
    {
        return _resources.TryGetValue(sdbId, out var value) ? value.Quantity : 0;
    }

    public void AddLoadout(Loadout loadout)
    {
        _loadouts.Add(loadout.FrameLoadoutId, loadout);
    }

    public void SendFullInventory()
    {
        if (_items.Count > (255 * 3) - 1)
        {
            throw new NotImplementedException("Too many items in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        if (_resources.Count > 254)
        {
            throw new NotImplementedException("Too many resources in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        if (_loadouts.Count > 254)
        {
            throw new NotImplementedException("Too many loadouts in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        var update = new InventoryUpdate()
        {
            ClearExistingData = 1,
            ItemsPart1Length = 0,
            ItemsPart1 = [],
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources = [.. _resources.Values],
            Loadouts = [.. _loadouts.Values],
            Unk = 1,
            SecondItems = [],
            SecondResources = []
        };

        if (_items.Count >= 255)
        {
            var tmp = _items.Values.ToArray();
            update.ItemsPart1Length = 255;
            update.ItemsPart1Full = tmp[..255];
            if (_items.Count >= 510)
            {
                update.ItemsPart2Length = 255;
                update.ItemsPart2Full = tmp[255..510];

                update.ItemsPart3Length = (byte)tmp[510..^0].Length;
                update.ItemsPart3 = tmp[510..^0];
            }
            else
            {
                update.ItemsPart2Length = (byte)tmp[255..^0].Length;
                update.ItemsPart2 = tmp[255..^0];
            }
        }
        else
        {
            update.ItemsPart1Length = (byte)_items.Count;
            update.ItemsPart1 = [.. _items.Values];
        }

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    /// <summary>
    ///     Tells the client an item left the inventory. <c>InventoryUpdate</c> has no per-item removal
    ///     form we know of (a partial update only ever adds or replaces entries), so a removal is
    ///     replicated as a full inventory (<c>ClearExistingData = 1</c>) plus a fresh bag layout. Without
    ///     this a consumed or salvaged guid item stayed in the client's bag until relog, and clicking it
    ///     produced activations the server then refused.
    /// </summary>
    public void SendItemsRemoved()
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        SendFullInventory();
        SendBagUpdate();
    }

    /// <summary>Sends the client the bag layout for the slots actually carried (see <see cref="BagInventoryLayout" />).</summary>
    public void SendBagUpdate()
    {
        if (!EnablePartialUpdates || _player == null)
        {
            return;
        }

        var bagUpdate = new BagInventoryUpdate { Data = BagInventoryLayout.BuildUpdateJson(GetBagSlots()) };
        _player.NetChannels[ChannelType.ReliableGss].SendMessage(bagUpdate, _character.EntityId);
    }

    public void SendItemUpdate(ulong guid)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var item = _items[guid];
        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = 1,
            ItemsPart1 =
            [
                item
            ],
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources = [],
            Loadouts = [],
            Unk = 1,
            SecondItems = [],
            SecondResources = []
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendResourceUpdate(uint sdbId)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        // ConsumeResource drops a pool the moment it hits zero, but the client still has to hear
        // that it is empty: indexing the dictionary here used to throw a KeyNotFoundException on
        // exactly that case (spending the last of a currency, e.g. a vendor purchase), which left
        // the caller without a response and the client's numbers stale.
        var resource = _resources.TryGetValue(sdbId, out var existing)
            ? existing
            : new Resource
            {
                SdbId = sdbId,
                TextKey = string.Empty,
                Quantity = 0,
                SubInventory = GetInventoryTypeByItemTypeId(sdbId),
                Unk2 = 0,
            };

        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = 0,
            ItemsPart1 = [],
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources =
            [
                resource
            ],
            Loadouts = [],
            Unk = 1,
            SecondItems = [],
            SecondResources = []
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendEquipmentChanges(ulong oldItemGuid, ulong newItemGuid)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var itemChanges = new Item[]
        {
        };

        if (oldItemGuid != 0)
        {
            var oldItem = _items[oldItemGuid];
            itemChanges = [.. itemChanges, oldItem];
        }

        if (newItemGuid != 0)
        {
            var newItem = _items[newItemGuid];
            itemChanges = [.. itemChanges, newItem];
        }

        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = (byte)itemChanges.Length,
            ItemsPart1 = itemChanges,
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources = [],
            Loadouts = [.. _loadouts.Values],
            Unk = 1,
            SecondItems = [],
            SecondResources = []
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void EquipItemByGUID(int loadoutId, LoadoutSlotType slot, ulong guid)
    {
        ulong changedOldItemGUID = 0;
        ulong changedNewItemGUID = guid;

        // Unequip old Item (if any)
        if (_loadouts[loadoutId].LoadoutConfigs[0].Items.Any((e) => e.SlotIndex == (byte)slot))
        {
            // Set Item to unequipped
            var oldItemGUID = _loadouts[loadoutId].LoadoutConfigs[0].Items.First((e) => e.SlotIndex == (byte)slot).ItemGUID;
            changedOldItemGUID = oldItemGUID;
            var oldItem = _items[oldItemGUID];
            oldItem.DynamicFlags = (byte)(oldItem.DynamicFlags ^ (byte)ItemDynamicFlags.IsEquipped);
            _items[oldItemGUID] = oldItem;

            // Update CurrentLoadout
            _character.CurrentLoadout.SlottedItems[slot] = 0;

            // Update LoadoutConfigs
            _loadouts[loadoutId].LoadoutConfigs[0].Items = [.. _loadouts[loadoutId].LoadoutConfigs[0].Items.Where(e => e.SlotIndex != (byte)slot)];
        }

        // Equip new item (if any)
        if (guid != 0)
        {
            // Update Item to Equipped
            var item = _items[guid];
            item.DynamicFlags = (byte)(item.DynamicFlags | (byte)ItemDynamicFlags.IsEquipped);
            _items[guid] = item;

            // Update CurrentLoadout
            _character.CurrentLoadout.SlottedItems[slot] = item.SdbId;

            // Update LoadoutConfig
            _loadouts[loadoutId].LoadoutConfigs[0].Items = [.. _loadouts[loadoutId].LoadoutConfigs[0].Items, new LoadoutConfig_Item() { ItemGUID = guid, SlotIndex = (byte)slot }];
        }

        // Update StaticInfo when visuals are changed
        var equippedSdbId = (guid != 0) ? _items[guid].SdbId : 0;
        switch (slot)
        {
            case LoadoutSlotType.Glider:
                _character.SetStaticInfo(_character.StaticInfo with { LoadoutGlider = equippedSdbId });
                break;
            case LoadoutSlotType.Vehicle:
                _character.SetStaticInfo(_character.StaticInfo with { LoadoutVehicle = equippedSdbId });
                break;
        }

        SendEquipmentChanges(changedOldItemGUID, changedNewItemGUID);
    }

    public void EquipVisualBySdbId(int loadoutId, LoadoutVisualType visual, LoadoutSlotType slot, uint sdb_id)
    {
        // Unequip old item (if any)
        if (_loadouts[loadoutId].LoadoutConfigs[0].Visuals.Any(i => i.VisualType == visual))
        {
            // Update Visuals
            _loadouts[loadoutId].LoadoutConfigs[0].Visuals = [.. _loadouts[loadoutId].LoadoutConfigs[0].Visuals.Where(e => e.VisualType != visual)];
        }

        // Equip new item (if any)
        if (sdb_id != 0)
        {
            // Update Visuals
            _loadouts[loadoutId].LoadoutConfigs[0].Visuals =
            [
                .. _loadouts[loadoutId].LoadoutConfigs[0].Visuals,
                new LoadoutConfig_Visual() { ItemSdbId = sdb_id, VisualType = visual, Data1 = 0, Data2 = 0, Transform = [] },
            ];
            _ = _items.First(e => e.Value.SdbId == sdb_id).Value;
        }

        var equippedGUID = (sdb_id != 0) ? _items.First(e => e.Value.SdbId == sdb_id).Value.GUID : 0;
        EquipItemByGUID(loadoutId, slot, equippedGUID);
    }

    private byte GetInventoryTypeByItemTypeId(uint sdbId)
    {
        var itemInfo = SDBInterface.GetRootItem(sdbId);
        if (itemInfo != null)
        {
            return GetInventoryTypeByItemType(itemInfo.Type);
        }
        else
        {
            return (byte)InventoryType.Bag;
        }
    }

    private byte GetInventoryTypeByItemType(byte itemType)
    {
        var result = InventoryType.Bag;
        switch ((ItemType)itemType)
        {
            case ItemType.TinkerTools:
                result = InventoryType.Bag;
                break;
            case ItemType.ItemModule:
                result = InventoryType.Bag;
                break;
            case ItemType.PaletteModule:
                result = InventoryType.Bag;
                break;
            case ItemType.CraftingStation:
                result = InventoryType.Bag;
                break;
            case ItemType.ResourceItem:
                result = InventoryType.Bag;
                break;
            case ItemType.LockBoxKey:
                result = InventoryType.Bag;
                break;
            case ItemType.Basic: // NOTE: There are some basic type items and resources that go into cache
                result = InventoryType.Bag;
                break;
            case ItemType.Consumable:
                result = InventoryType.Cache;
                break;
            case ItemType.AbilityModule:
                result = InventoryType.Gear;
                break;
            case ItemType.FrameModule:
                result = InventoryType.Gear;
                break;
            case ItemType.Weapon:
                result = InventoryType.Gear;
                break;
            case ItemType.Chassis:
                result = InventoryType.Gear;
                break;
            default:
                _logger.Warning("Unknown InventoryType for ItemType {ItemType}, defaulting to {Result}", (ItemType)itemType, result);
                break;
        }

        return (byte)result;
    }
}