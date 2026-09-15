using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
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

    /// <summary>Copies granted per purchase. Every row live served granted one.</summary>
    public uint Quantity { get; init; } = 1;

    /// <summary>
    ///     How long the goods last, in the unit the window's <c>Duration</c> field carries. Live sent
    ///     zero on every row, including the boosts and memberships a machine can roll.
    /// </summary>
    public uint Duration { get; init; }

    /// <summary>The currency, as a <c>dbitems::RootItem</c> resource id (see <see cref="VendorCatalog.CrystiteSdbId" />).</summary>
    public uint CurrencySdbId { get; init; }

    /// <summary>Cost per purchase, in the currency.</summary>
    public uint Cost { get; init; }

    /// <summary>
    ///     The reputation with the store's faction this row is gated behind, or
    ///     <see cref="LiveVendorData.NoReputationGate" /> when anyone can buy it. Live carried the gate
    ///     as a <c>MinReputationRestriction</c> naming the store's faction and the threshold.
    /// </summary>
    public uint MinReputation { get; init; }

    /// <summary>
    ///     Buying this row plays the machine's real loot-table roll instead of handing over the row's
    ///     item: the token vending machines list the prizes they advertise, but dispense what
    ///     <see cref="VendorTokenRoll" /> rolls.
    /// </summary>
    public bool TokenMachineRoll { get; init; }

    /// <summary>Where this row's numbers came from.</summary>
    public VendorDataProvenance Provenance { get; init; } = VendorDataProvenance.Emulated;

    /// <summary>Localized display name, for logging.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
///     The window a vendor is opened with, apart from its stock: which store it is, what it is called,
///     whose reputation discounts it, and the ladder of those discounts.
/// </summary>
public sealed class VendorStoreDecor
{
    /// <summary>The store id (<c>VendorProductsResponse.Id</c>) the client echoes back when it buys.</summary>
    public uint StoreId { get; init; }

    /// <summary>The window title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The faction the window's reputation ladder belongs to.</summary>
    public uint FactionId { get; init; }

    /// <summary>The discount ladder, empty when the faction has no positive reputation rungs.</summary>
    public IReadOnlyList<VendorDiscountTier> Discounts { get; init; } = [];
}

/// <summary>
///     Builds the stock NPC vendors sell. Three kinds of data feed it, in this order of trust:
/// </summary>
/// <remarks>
///     <para>
///     <b>Captured stores</b> - a vendor a live server was recorded serving answers with exactly what
///     the recording shows: the stock, the prices, the reputation gates, the window title, the store
///     id and the discount ladder (see <see cref="LiveVendorData" />). One vendor is captured today,
///     terminal 60 - Supply Officer Cross in Copacabana, "Copacabana ARES Supplies", store 2321.
///     </para>
///     <para>
///     <b>Token-machine vendors</b> - a <c>vendor_id</c> that is a <c>dbitems::VendorTokenMachine</c>
///     id (monster rows carry machines 5 and 105). Their window lists the machine's
///     <c>VendorTokenDisplayItems</c> prizes, priced at one of the machine's
///     <c>VendorTokenKeyItems</c> tokens each, exactly as the tables author them; buying spends the
///     token and rolls the machine's real loot tables (<see cref="VendorTokenRoll" />).
///     </para>
///     <para>
///     <b>Quartermaster vendors</b> - every other <c>vendor_id</c>, some seventy of them in build
///     prod-1962 (supply officers, the frame experts, the merit quartermaster, the corporation reps).
///     Their store catalogs were server-only and did not survive, and nothing in the client database
///     names a price: <c>dbitems::RootItem</c> has 27 columns and no cost among them, and the only
///     cost-like columns in the whole file belong to crafting, prestige and perk points. So the shelf
///     is the captured store's field supplies - every one of those prices is a real one - plus the
///     consumables PIN stocked before the capture was decoded, whose prices are chosen to sit with the
///     captured evidence and are marked <see cref="VendorDataProvenance.Emulated" />. The captured
///     store's 21 reputation-gated tier-8 gear rows stay with their own vendor: they are Copacabana's
///     local issue, and their names are placeholders outside it.
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
    ///     The field supplies of the captured store, in the order its window listed them. These are
    ///     the rows every quartermaster PIN serves shares with a real one, and their prices come
    ///     straight out of the capture (see <see cref="LiveVendorData" />).
    /// </summary>
    private static readonly uint[] CapturedConsumableShelf =
    [
        56811,   // Scan Hammer
        30287,   // Health Pack, Small
        30298,   // Ammo Pack
        75096,   // Health Pack, Medium
        85193,   // Health Pack, Large
        116563,  // Grenade, Concussion
        82577,   // Grenade, Flash Freeze
        82595,   // Grenade, Incendiary
        82596,   // Grenade, Toxic
        82597,   // Adrenaline Injector
        32755,   // 1-Use Glider Pad Calldown
    ];

    /// <summary>
    ///     Consumables PIN stocked before the capture was decoded, which no capture and no database
    ///     row prices: each is set beside the captured row it is nearest to, and says which.
    /// </summary>
    /// <remarks>
    ///     The boosts this shelf used to carry (XP and crystite, 20% for an hour) are gone from it:
    ///     on live a quartermaster never sold them. They were vending-machine prizes - loot table
    ///     5855, "Accord Reward Quartermaster - Slot 2 BOOSTS (rare)", rolls them at 3/48 and 21/48 -
    ///     and cash-shop goods, and PIN's machines now roll them for real.
    /// </remarks>
    private static readonly (uint SdbId, uint Cost)[] EmulatedShelf =
    [
        (77655, 100),   // Stim Pack, Small - a quality-1 field consumable, priced with the health and ammo packs
        (85535, 100),   // Energy Pack - same family
        (54003, 150),   // Sonic Detonator - a throwable, priced with the grenades
        (75425, 50),    // Flare (Red) - cheap utility, priced with the scan hammer
        (56, 150),      // 1-Use Jump Pad Calldown - the captured glider pad calldown is 150
        (30745, 500),   // Recharging Ammo Pack - the reusable variant of the 100 cy ammo pack, priced with the medium health pack
    ];

    /// <summary>Builds the stock a vendor window displays, from the captures and the client database.</summary>
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
        // A captured store outranks everything: it is what a real server answered for this vendor.
        var captured = LiveVendorData.FindStore(vendorId);
        if (captured != null)
        {
            return BuildCaptured(vendorId, captured, rootItemLookup);
        }

        var machine = machineLookup(vendorId);
        if (machine != null)
        {
            return BuildTokenMachine(vendorId, rootItemLookup, displayItems, keyItems);
        }

        return BuildQuartermaster(vendorId, rootItemLookup);
    }

    /// <summary>
    ///     The window a vendor opens with: its store id, title, faction and discount ladder. A
    ///     captured store answers with what the capture shows; anything else is described from the
    ///     NPC and the database.
    /// </summary>
    /// <param name="vendorId">The NPC's <c>dbcharacter::Monster.vendor_id</c>.</param>
    /// <param name="npcTitle">The NPC's localized name, the title a store of its own has no need of.</param>
    /// <param name="npcFactionId">The NPC's <c>dbcharacter::Monster.faction_id</c>.</param>
    /// <returns>The window's identity and discounts.</returns>
    public static VendorStoreDecor Describe(uint vendorId, string npcTitle, uint npcFactionId)
    {
        return Describe(vendorId, npcTitle, npcFactionId, SDBInterface.GetVendorTokenMachine, SDBInterface.GetFactionReputations);
    }

    /// <summary>Test seam: the same description with the database lookups injected.</summary>
    internal static VendorStoreDecor Describe(
        uint vendorId,
        string npcTitle,
        uint npcFactionId,
        Func<uint, VendorTokenMachine> machineLookup,
        Func<uint, List<FactionReputations>> reputationLookup)
    {
        var captured = LiveVendorData.FindStore(vendorId);
        if (captured != null)
        {
            // The capture names the store, not the NPC: live titled terminal 60's window after its
            // POI and discounted it against the POI faction, while the NPC himself is Accord.
            return new VendorStoreDecor
            {
                StoreId = captured.StoreId,
                Title = string.IsNullOrWhiteSpace(captured.Title) ? npcTitle : captured.Title,
                FactionId = captured.FactionId,
                Discounts = captured.Discounts,
            };
        }

        return new VendorStoreDecor
        {
            StoreId = StoreId(vendorId, machineLookup),
            Title = npcTitle,
            FactionId = npcFactionId,
            Discounts = DiscountTiersFor(npcFactionId, reputationLookup),
        };
    }

    /// <summary>
    ///     The discount ladder of a faction's vendor windows: one rung per positive reputation tier
    ///     the database authors for that faction, carrying the fractions the capture shows live
    ///     pairing with them.
    /// </summary>
    /// <remarks>
    ///     <c>dbcharacter::FactionReputations</c> gives faction 20 (Copacabana) rungs at -16000,
    ///     -8000, 0, 6000, 12000 and 24000, and the captured window discounted at exactly the three
    ///     positive ones: 6000 for 10%, 12000 for 20%, 24000 for 30%. Thirty-seven of the thirty-eight
    ///     factions with positive rungs have those same three, so the ladder is the database's own and
    ///     the fractions are the capture's. Rungs are matched to fractions by position, so a faction
    ///     with one rung gets 10% and a faction with more than three keeps 30% for the rest.
    /// </remarks>
    /// <param name="factionId">The faction whose windows are being described.</param>
    /// <param name="reputationLookup"><c>dbcharacter::FactionReputations</c> rows by faction id.</param>
    /// <returns>The ladder, lowest rung first; empty when the faction has no positive rungs.</returns>
    public static IReadOnlyList<VendorDiscountTier> DiscountTiersFor(uint factionId, Func<uint, List<FactionReputations>> reputationLookup)
    {
        var tiers = new List<VendorDiscountTier>();
        if (factionId == 0 || reputationLookup == null)
        {
            return tiers;
        }

        var rungs = reputationLookup(factionId);
        if (rungs == null)
        {
            return tiers;
        }

        var fractions = LiveVendorData.CapturedDiscountFractions;
        var positive = rungs
            .Where(rung => rung.MinReputation > 0)
            .Select(rung => (uint)rung.MinReputation)
            .Distinct()
            .OrderBy(reputation => reputation)
            .ToList();

        for (var i = 0; i < positive.Count; i++)
        {
            // Past the end of the captured ladder the deepest discount stands: nothing says a fourth
            // rung existed, and no faction in prod-1962 has one.
            var fraction = fractions.Count == 0 ? 0f : fractions[Math.Min(i, fractions.Count - 1)].Discount;
            tiers.Add(new VendorDiscountTier(positive[i], fraction));
        }

        return tiers;
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
    ///     On live this was an id out of the server-only store tables: terminal 60 answered with
    ///     2321, and the capture's purchase request echoes 2321 back. Those tables did not survive,
    ///     so a captured store replays the id it was recorded with, a token machine answers with the
    ///     <c>dbitems::VendorTokenMachine.web_vendor_id</c> the database gives it (its web-store id,
    ///     which is what a machine's window stood in for on live), and any other vendor answers with
    ///     its own vendor id. All three are small, stable across a session and unique per vendor,
    ///     which is all the client needs: it never resolves the id itself, it just hands it back.
    ///     Keeping it under 2^32 matters - a 64-bit entity id (what this used to send) is both
    ///     truncated by the client and mangled by its UI's double arithmetic.
    /// </remarks>
    /// <param name="vendorId">The NPC's <c>dbcharacter::Monster.vendor_id</c>.</param>
    /// <returns>The store id to open the window with.</returns>
    public static uint StoreId(uint vendorId)
    {
        return StoreId(vendorId, SDBInterface.GetVendorTokenMachine);
    }

    /// <summary>Test seam: the same store id with the machine lookup injected.</summary>
    internal static uint StoreId(uint vendorId, Func<uint, VendorTokenMachine> machineLookup)
    {
        var captured = LiveVendorData.FindStore(vendorId);
        if (captured != null)
        {
            return captured.StoreId;
        }

        var machine = machineLookup?.Invoke(vendorId);
        if (machine != null && machine.WebVendorId != 0)
        {
            return machine.WebVendorId;
        }

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

    /// <summary>A captured store's stock, row for row, priced and gated as it was recorded.</summary>
    private static IReadOnlyList<VendorCatalogEntry> BuildCaptured(uint vendorId, CapturedVendorStore captured, Func<uint, RootItem> rootItemLookup)
    {
        var entries = new List<VendorCatalogEntry>();
        foreach (var product in captured.Products)
        {
            // A row the loaded database does not know would show up nameless; skip it rather than
            // list something the client cannot resolve.
            if (rootItemLookup(product.SdbId) == null)
            {
                continue;
            }

            entries.Add(new VendorCatalogEntry
            {
                Index = entries.Count,
                SdbId = product.SdbId,
                Quantity = product.Quantity,
                Duration = 0,
                CurrencySdbId = CrystiteSdbId,
                Cost = product.Cost,
                MinReputation = product.MinReputation,
                Provenance = VendorDataProvenance.Captured,
                Name = ItemName(rootItemLookup, product.SdbId),
            });
        }

        return entries;
    }

    /// <summary>
    ///     A token machine's window: the prizes its tables advertise, one key token apiece. Buying a
    ///     row spends the token and rolls the machine's loot tables instead of handing the row over -
    ///     that is what a vending machine does, and the advertised prizes are what its
    ///     <c>VendorTokenDisplayItems</c> rows are for.
    /// </summary>
    private static IReadOnlyList<VendorCatalogEntry> BuildTokenMachine(
        uint vendorId,
        Func<uint, RootItem> rootItemLookup,
        IReadOnlyList<VendorTokenDisplayItems> displayItems,
        IReadOnlyList<VendorTokenKeyItems> keyItems)
    {
        var entries = new List<VendorCatalogEntry>();

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
                TokenMachineRoll = true,
                Provenance = VendorDataProvenance.DatabaseDerived,
                Name = ItemName(rootItemLookup, display.ItemId),
            });
        }

        return entries;
    }

    /// <summary>
    ///     A quartermaster's shelf: the captured store's field supplies at their captured prices and
    ///     gates, then the consumables PIN added on its own at prices set beside them.
    /// </summary>
    private static IReadOnlyList<VendorCatalogEntry> BuildQuartermaster(uint vendorId, Func<uint, RootItem> rootItemLookup)
    {
        var entries = new List<VendorCatalogEntry>();

        foreach (var sdbId in CapturedConsumableShelf)
        {
            if (rootItemLookup(sdbId) == null || !LiveVendorData.TryGetCapturedPrice(sdbId, out uint cost, out uint minReputation))
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
                MinReputation = minReputation,
                Provenance = VendorDataProvenance.Captured,
                Name = ItemName(rootItemLookup, sdbId),
            });
        }

        foreach ((uint sdbId, uint cost) in EmulatedShelf)
        {
            if (rootItemLookup(sdbId) == null)
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
                Provenance = VendorDataProvenance.Emulated,
                Name = ItemName(rootItemLookup, sdbId),
            });
        }

        return entries;
    }

    private static string ItemName(Func<uint, RootItem> rootItemLookup, uint sdbId)
    {
        var item = rootItemLookup(sdbId);
        return SDBInterface.GetLocalizedString(item?.NameId ?? 0u) ?? $"item {sdbId}";
    }
}
