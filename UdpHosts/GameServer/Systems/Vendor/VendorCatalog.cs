using System;
using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Vendor;

/// <summary>
///     One stock entry of an NPC vendor window: what the player buys, and what it costs.
/// </summary>
public sealed class VendorCatalogEntry
{
    /// <summary>Position in the window; also encoded into the deterministic product guid.</summary>
    public int Index { get; init; }

    /// <summary>The <c>dbitems::RootItem</c> the player receives.</summary>
    public uint SdbId { get; init; }

    /// <summary>Copies granted per purchase.</summary>
    public uint Quantity { get; init; } = 1;

    /// <summary>The currency, as a <c>dbitems::RootItem</c> resource id (see <see cref="VendorCatalog.CrystiteSdbId" />).</summary>
    public uint CurrencySdbId { get; init; }

    /// <summary>Cost per purchase, in the currency.</summary>
    public uint Cost { get; init; }

    /// <summary>Localized display name, for logging.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
///     Builds the stock lists NPC vendors sell, entirely from the client database. Two shapes exist:
/// </summary>
/// <remarks>
///     <para>
///     <b>Token-machine vendors</b> - a <c>vendor_id</c> that is a <c>dbitems::VendorTokenMachine</c>
///     id (monster rows 5 and 105 carry machines 5 and 105). Their window lists the machine's
///     <c>VendorTokenDisplayItems</c> prizes, priced at one of the machine's
///     <c>VendorTokenKeyItems</c> tokens each, exactly as the tables author them.
///     </para>
///     <para>
///     <b>Quartermaster vendors</b> - every other <c>vendor_id</c>. The live store catalogs behind
///     these ids were server-only and did not survive, so their windows stock a curated selection of
///     real <c>dbitems::RootItem</c> consumables (health packs, ammo, grenades, calldowns, boosts),
///     priced in crystite (item 10). The prices are emulated - nothing in the client database names
///     a cost - and deliberately modest.
///     </para>
///     <para>
///     Product and price guids are deterministic encodings of (vendor id, index), so a purchase can
///     be validated and decoded without keeping any per-player shop state around.
///     </para>
/// </remarks>
public static class VendorCatalog
{
    /// <summary><c>dbitems::RootItem</c> 10 - crystite, the currency every quartermaster price uses.</summary>
    public const uint CrystiteSdbId = 10;

    /// <summary>High dword of every vendor guid the server mints ("VEND").</summary>
    private const ulong GuidBase = 0x56454E44_00000000ul;

    /// <summary>Bit 31 marks a price guid; product guids leave it clear (vendor ids never reach it).</summary>
    private const uint PriceFlag = 1u << 31;

    /// <summary>
    ///     The quartermaster stock: real consumable <c>dbitems::RootItem</c> rows with emulated
    ///     crystite prices (sdb id, cost). The selection mirrors what Accord quartermasters sold on
    ///     live - field supplies first, boosts last.
    /// </summary>
    private static readonly (uint SdbId, uint Cost)[] QuartermasterStock =
    [
        (30287, 100),   // Health Pack, Small
        (75096, 250),   // Health Pack, Medium
        (85193, 500),   // Health Pack, Large
        (30298, 100),   // Ammo Pack
        (30745, 400),   // Recharging Ammo Pack
        (77655, 150),   // Stim Pack, Small
        (85535, 300),   // Energy Pack
        (82577, 300),   // Grenade, Flash Freeze
        (82595, 300),   // Grenade, Incendiary
        (82596, 300),   // Grenade, Toxic
        (116563, 350),  // Grenade, Concussion
        (54003, 750),   // Sonic Detonator
        (56, 1000),     // 1-Use Jump Pad Calldown
        (32755, 1000),  // 1-Use Glider Pad Calldown
        (75425, 50),    // Flare (Red)
        (77066, 2500),  // XP Boost - 20% for 1 hour
        (81362, 2500),  // Crystite Boost - 20% for 1 hour
    ];

    /// <summary>Builds the stock a vendor window displays, from the client database.</summary>
    /// <param name="vendorId">The NPC's <c>dbcharacter::Monster.vendor_id</c>.</param>
    /// <returns>The stock entries, in window order; empty when nothing resolvable exists.</returns>
    public static IReadOnlyList<VendorCatalogEntry> Build(uint vendorId)
    {
        return Build(vendorId, SDBInterface.GetRootItem, SDBInterface.GetVendorTokenMachine, SDBInterface.GetVendorTokenDisplayItems(), SDBInterface.GetVendorTokenKeyItems());
    }

    /// <summary>
    ///     Test seam: the same build with the database lookups injected, so unit tests run without a
    ///     loaded StaticDB.
    /// </summary>
    internal static IReadOnlyList<VendorCatalogEntry> Build(
        uint vendorId,
        Func<uint, RootItem> rootItemLookup,
        Func<uint, VendorTokenMachine> machineLookup,
        IReadOnlyList<VendorTokenDisplayItems> displayItems,
        IReadOnlyList<VendorTokenKeyItems> keyItems)
    {
        var entries = new List<VendorCatalogEntry>();

        var machine = machineLookup(vendorId);
        if (machine != null)
        {
            // Token machines: the prizes the tables list for that machine, one key token apiece.
            uint tokenId = 0;
            foreach (var key in keyItems)
            {
                if (key.MachineId == vendorId)
                {
                    tokenId = key.KeyItemId;
                    break;
                }
            }

            foreach (var display in displayItems)
            {
                if (display.MachineId != vendorId)
                {
                    continue;
                }

                var item = rootItemLookup(display.ItemId);
                if (item == null)
                {
                    continue;
                }

                entries.Add(new VendorCatalogEntry
                {
                    Index = entries.Count,
                    SdbId = display.ItemId,
                    Quantity = 1,
                    CurrencySdbId = tokenId != 0 ? tokenId : display.KeyItemId,
                    Cost = 1,
                    Name = ItemName(rootItemLookup, display.ItemId),
                });
            }

            return entries;
        }

        // Quartermasters: the curated consumable shelf, minus anything the loaded database does not
        // know (keeps the window clean if a build ever loses a row).
        foreach ((uint sdbId, uint cost) in QuartermasterStock)
        {
            var item = rootItemLookup(sdbId);
            if (item == null)
            {
                continue;
            }

            entries.Add(new VendorCatalogEntry
            {
                Index = entries.Count,
                SdbId = sdbId,
                Quantity = 1,
                CurrencySdbId = CrystiteSdbId,
                Cost = cost,
                Name = ItemName(rootItemLookup, sdbId),
            });
        }

        return entries;
    }

    /// <summary>The deterministic guid the client sees (and sends back) for a stock entry.</summary>
    public static ulong ProductGuid(uint vendorId, int index)
    {
        return GuidBase | ((ulong)(vendorId & 0x3FFF) << 16) | (uint)(index & 0xFFFF);
    }

    /// <summary>The deterministic guid of the entry's single price line.</summary>
    public static ulong PriceGuid(uint vendorId, int index)
    {
        return ProductGuid(vendorId, index) | PriceFlag;
    }

    /// <summary>
    ///     Decodes a guid minted by this catalog back into vendor id and stock index.
    /// </summary>
    /// <param name="guid">The product or price guid from the client.</param>
    /// <param name="vendorId">The vendor the guid belongs to.</param>
    /// <param name="index">The stock index inside that vendor's window.</param>
    /// <param name="isPrice">Whether the guid is a price line rather than a product.</param>
    /// <returns>Whether the guid is one of this catalog's.</returns>
    public static bool TryDecodeGuid(ulong guid, out uint vendorId, out int index, out bool isPrice)
    {
        vendorId = 0;
        index = 0;
        isPrice = false;

        if ((guid & 0xFFFFFFFF_00000000ul) != GuidBase)
        {
            return false;
        }

        uint low = (uint)guid;
        isPrice = (low & PriceFlag) != 0;
        low &= ~PriceFlag;

        vendorId = (low >> 16) & 0x3FFF;
        index = (int)(low & 0xFFFF);
        return true;
    }

    private static string ItemName(Func<uint, RootItem> rootItemLookup, uint sdbId)
    {
        var item = rootItemLookup(sdbId);
        return SDBInterface.GetLocalizedString(item?.NameId ?? 0) ?? $"item {sdbId}";
    }
}
