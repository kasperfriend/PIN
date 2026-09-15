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
///     <para>
///     <b>Every id this catalog mints is small</b>, and that is load-bearing rather than cosmetic. A
///     live 2015 capture (build 1869, the "Copacabana ARES Supplies" window) shows the real server
///     minting plain database ids - products 469931..603221, prices 1229621..1334821, all below
///     2^21 - and the client echoing exactly those ids back in <c>VendorPurchaseRequest</c>. The
///     client carries them through its scripted UI, where every number is an IEEE-754 double with a
///     53-bit mantissa. A guid above 2^53 does not survive that round trip: at 6.2e18 (what the
///     "VEND"-prefixed guids this catalog used to mint weigh in at) the spacing between
///     representable doubles is 1024, so a whole vendor's stock collapses onto one value and the id
///     the UI hands back to the native layer no longer matches the entry it came from. The window
///     still renders and a row can still be highlighted, but Buy never reaches the wire. See
///     <see cref="ProductGuid" /> and <see cref="StoreId" />.
///     </para>
/// </remarks>
public static class VendorCatalog
{
    /// <summary><c>dbitems::RootItem</c> 10 - crystite, the currency every quartermaster price uses.</summary>
    public const uint CrystiteSdbId = 10;

    /// <summary>
    ///     Bit 25, set in every guid the catalog mints so that neither a product nor a price can ever
    ///     be zero (the client reads zero as "nothing selected"). It sits above the fields rather
    ///     than inside them: a base bit inside the vendor's range would decode as vendor + 256.
    /// </summary>
    private const ulong GuidBase = 1ul << 25;

    /// <summary>
    ///     Bit 24 marks a price guid; product guids leave it clear. Vendor ids occupy bits 8..23 and
    ///     the stock index bits 0..7, so neither the flag nor <see cref="GuidBase" /> can collide
    ///     with either field.
    /// </summary>
    private const ulong PriceFlag = 1ul << 24;

    /// <summary>
    ///     Every bit a minted guid may use: the 8-bit stock index, the 16-bit vendor id,
    ///     <see cref="PriceFlag" /> and <see cref="GuidBase" />. Anything else is not one of ours.
    /// </summary>
    private const ulong GuidMask = 0x3FFFFFFul;

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

    /// <summary>
    ///     The deterministic guid the client sees (and sends back) for a stock entry: 8 bits of stock
    ///     index, 16 bits of vendor id and <see cref="GuidBase" />, so it is never zero and never
    ///     above 0x2FFFFFF - well inside the 32 bits (and the 53-bit double) the client's UI holds it
    ///     in, and the same order of magnitude as the ids the live server minted.
    /// </summary>
    /// <remarks>
    ///     The index is masked to a byte because the protocol counts a window's products with a
    ///     single byte anyway - a vendor can never list more than 255 entries.
    /// </remarks>
    public static ulong ProductGuid(uint vendorId, int index)
    {
        return GuidBase | ((ulong)(vendorId & 0xFFFF) << 8) | (uint)(index & 0xFF);
    }

    /// <summary>The deterministic guid of the entry's single price line.</summary>
    public static ulong PriceGuid(uint vendorId, int index)
    {
        return ProductGuid(vendorId, index) | PriceFlag;
    }

    /// <summary>
    ///     The store id a vendor window is opened with (<c>VendorProductsResponse.Id</c>), which the
    ///     client truncates to 32 bits and echoes back as the last field of a purchase request.
    /// </summary>
    /// <remarks>
    ///     On live this was an id out of the server-only store tables (terminal 60 answered with
    ///     store 2321); those tables did not survive, so the terminal's vendor id stands in. It is
    ///     small, stable across a session and unique per vendor, which is all the client needs: it
    ///     never resolves the id itself, it just hands it back. Keeping it under 2^32 matters - a
    ///     64-bit entity id (what this used to send) is both truncated by the client and mangled by
    ///     its UI's double arithmetic.
    /// </remarks>
    /// <param name="vendorId">The NPC's <c>dbcharacter::Monster.vendor_id</c>.</param>
    /// <returns>The store id to open the window with.</returns>
    public static uint StoreId(uint vendorId)
    {
        return vendorId;
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

        // Anything using bits outside the encoding, or missing the base bit, is not ours: an
        // inventory item guid, a store id, or a value the client's UI rounded into nonsense.
        if ((guid & ~GuidMask) != 0 || (guid & GuidBase) == 0)
        {
            return false;
        }

        isPrice = (guid & PriceFlag) != 0;
        vendorId = (uint)((guid >> 8) & 0xFFFF);
        index = (int)(guid & 0xFF);
        return true;
    }

    private static string ItemName(Func<uint, RootItem> rootItemLookup, uint sdbId)
    {
        var item = rootItemLookup(sdbId);
        return SDBInterface.GetLocalizedString(item?.NameId ?? 0) ?? $"item {sdbId}";
    }
}
