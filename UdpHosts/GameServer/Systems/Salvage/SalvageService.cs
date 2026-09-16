using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
using Serilog;
using RequestEntry = AeroMessages.GSS.Character.Command.ItemSalvageRequest;
using ResponseEntry = AeroMessages.GSS.Character.Event.ItemSalvageResponse;

namespace GameServer.Systems.Salvage;

/// <summary>
///     The salvage window's <c>SalvageRequest</c>: breaks the named items down into what the
///     client database says they are made of. Equipment travels by guid (one request entry per
///     piece, quantity ignored past one); stackables travel guidless, by sdb id and quantity,
///     which is exactly what the client sends for them - a captured request for twenty health
///     packs is <c>guid 0, sdb 82604, quantity 20</c>.
/// </summary>
/// <remarks>
///     <para>
///     What an item breaks into comes straight out of the database: <c>RootItem.SalvageRewards</c>
///     names a <c>dbitems::SalvageRewards</c> row, the row names the loot table, and
///     <see cref="SalvageRoller" /> rolls it once per salvaged piece. Items the database does not
///     mark (salvage rewards id 0 or a dangling row) are refused whole - the request entry is
///     dropped from the response so the client keeps showing the item, and the server keeps it.
///     </para>
///     <para>
///     The response is the wire shape from AeroMessages: the client removes from the window what
///     the response echoes back, so the echo carries exactly the entries - and, for stackables,
///     the quantities - that were actually broken down.
///     </para>
/// </remarks>
public static class SalvageService
{
    private static readonly ILogger Logger = Log.ForContext(typeof(SalvageService));

    /// <summary>Item lookup; production reads the database, tests swap in a fixed shelf.</summary>
    internal static Func<uint, RootItem> LookupItem { get; set; } = SDBInterface.GetRootItem;

    /// <summary>Salvage-rewards lookup; production reads the database, tests swap in a fixed shelf.</summary>
    internal static Func<uint, SalvageRewards> LookupSalvageRewards { get; set; } = SDBInterface.GetSalvageRewards;

    /// <summary>The loot-table roll; production rolls the real tables, tests swap in fixed awards.</summary>
    internal static Func<uint, IReadOnlyList<SalvageAward>> RollLootTable { get; set; } = SalvageRoller.Roll;

    /// <summary>
    ///     Executes a salvage batch: removes what can be broken down, rolls and grants the
    ///     materials, and answers with the echo of what was salvaged.
    /// </summary>
    /// <param name="player">The salvaging player.</param>
    /// <param name="requests">The entries of the client's request.</param>
    /// <returns>The response to send back.</returns>
    public static AeroMessages.GSS.Character.Event.SalvageResponse Run(IPlayer player, RequestEntry[] requests)
    {
        var character = player.CharacterEntity;
        var inventory = player.Inventory;

        var salvaged = new List<ResponseEntry>();
        var awards = new List<SalvageAward>();

        if (inventory != null && requests != null)
        {
            foreach (var entry in requests)
            {
                if (!TryRemove(inventory, character, entry, out uint removedQuantity, out uint lootTableId))
                {
                    continue;
                }

                salvaged.Add(new ResponseEntry
                {
                    GUID = entry.GUID,
                    SdbId = entry.SdbId,
                    Quantity = removedQuantity,
                });

                for (uint piece = 0; piece < removedQuantity; piece++)
                {
                    awards.AddRange(RollLootTable(lootTableId));
                }
            }
        }

        if (inventory != null && awards.Count > 0)
        {
            Grant(inventory, awards);
        }

        if (salvaged.Count > 0)
        {
            Logger.Information(
                "Salvage: {Player} salvaged {Salvaged} for {Awards}",
                character,
                string.Join(", ", salvaged.GroupBy(s => s.SdbId).Select(g => $"{ItemName(g.Key)} x{g.Aggregate(0u, (sum, s) => sum + s.Quantity)}")),
                awards.Count > 0
                    ? string.Join(", ", awards.GroupBy(a => a.SdbId).Select(g => $"{ItemName(g.Key)} x{g.Aggregate(0u, (sum, a) => sum + a.Quantity)}"))
                    : "nothing");
        }

        return new AeroMessages.GSS.Character.Event.SalvageResponse
        {
            Unk1 = 1,
            SalvageResponses = [.. salvaged],
        };
    }

    /// <summary>
    ///     What an entry breaks into: the loot table of the item's <c>dbitems::SalvageRewards</c>
    ///     row, or 0 when the item cannot be salvaged.
    /// </summary>
    private static uint SalvageLootTable(uint sdbId)
    {
        var rewardsId = LookupItem(sdbId)?.SalvageRewards ?? 0;
        return rewardsId == 0 ? 0 : LookupSalvageRewards(rewardsId)?.LootTable ?? 0;
    }

    /// <summary>
    ///     Removes one request entry from the inventory, in the shape the entry travels in:
    ///     guid-carrying equipment by guid (equipped gear is refused, and the guid must name that
    ///     sdb id), stackables out of their resource pool by quantity (all-or-nothing).
    /// </summary>
    private static bool TryRemove(CharacterInventory inventory, CharacterEntity character, RequestEntry entry, out uint removedQuantity, out uint lootTableId)
    {
        removedQuantity = 0;
        lootTableId = SalvageLootTable(entry.SdbId);

        if (LookupItem(entry.SdbId) == null)
        {
            Logger.Debug("Salvage: {Player} cannot salvage {ItemSdbId}: unknown item", character, entry.SdbId);
            return false;
        }

        if (lootTableId == 0)
        {
            Logger.Debug("Salvage: {Player} cannot salvage {ItemSdbId}: no salvage rewards", character, entry.SdbId);
            return false;
        }

        if (entry.GUID != 0)
        {
            if (!inventory.TryGetItem(entry.GUID, out var held) || held.SdbId != entry.SdbId)
            {
                Logger.Debug("Salvage: {Player} cannot salvage item {ItemGuid}: not carried (or a different item)", character, entry.GUID);
                return false;
            }

            if (!inventory.RemoveItem(entry.GUID))
            {
                Logger.Debug("Salvage: {Player} cannot salvage equipped item {ItemGuid} ({ItemSdbId})", character, entry.GUID, entry.SdbId);
                return false;
            }

            removedQuantity = 1;
            return true;
        }

        var quantity = entry.Quantity == 0 ? 1u : entry.Quantity;
        if (!inventory.ConsumeResource(entry.SdbId, quantity))
        {
            Logger.Debug("Salvage: {Player} cannot salvage {Quantity} of {ItemSdbId}: the pool does not hold them", character, quantity, entry.SdbId);
            return false;
        }

        removedQuantity = quantity;
        return true;
    }

    /// <summary>
    ///     Hands the salvage materials to the player: goods that live in stacks top the matching
    ///     resource pools up, everything else lands as real items, one per copy.
    /// </summary>
    private static void Grant(CharacterInventory inventory, List<SalvageAward> awards)
    {
        foreach (var stack in awards
                     .Where(a => IsStacked(a.SdbId))
                     .GroupBy(a => a.SdbId))
        {
            inventory.AddResource(stack.Key, stack.Aggregate(0u, (sum, a) => sum + a.Quantity));
        }

        foreach (var award in awards.Where(a => !IsStacked(a.SdbId)))
        {
            for (uint copy = 0; copy < award.Quantity; copy++)
            {
                inventory.CreateItem(award.SdbId);
            }
        }
    }

    /// <summary>Whether the awarded good stacks as a resource (consumables, basics, raw materials) or is carried as items.</summary>
    private static bool IsStacked(uint sdbId)
    {
        var item = LookupItem(sdbId);
        return item != null && ItemStacking.IsStackedAsResource(item.Type);
    }

    /// <summary>The localized name of an item, for a log line.</summary>
    private static string ItemName(uint sdbId)
    {
        return SDBInterface.GetLocalizedString(LookupItem(sdbId)?.NameId ?? 0u) ?? $"item {sdbId}";
    }
}
