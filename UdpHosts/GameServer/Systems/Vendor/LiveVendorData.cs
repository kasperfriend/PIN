using System.Collections.Generic;
using System.Linq;

namespace GameServer.Systems.Vendor;

/// <summary>
///     Where one piece of vendor data came from. Nothing in the client database names a price -
///     <c>dbitems::RootItem</c> has 27 columns and not one of them is a cost - so every number a
///     vendor window carries has to be accounted for: captured from a live server, derived from real
///     database rows, or invented here.
/// </summary>
public enum VendorDataProvenance
{
    /// <summary>
    ///     Byte-exact from a live capture. The value a real server sent, for the item a real server
    ///     sold it on.
    /// </summary>
    Captured,

    /// <summary>
    ///     Computed from real rows of the client database (reputation tiers, token-machine loot
    ///     tables) using a rule the captures confirm.
    /// </summary>
    DatabaseDerived,

    /// <summary>
    ///     PIN's own. No original survives anywhere - not in a capture, not in the database - so the
    ///     value is chosen to sit with the captured evidence next to it, and says so.
    /// </summary>
    Emulated,
}

/// <summary>
///     One rung of a vendor window's reputation discount ladder: at this much reputation with the
///     store's faction, prices drop by this fraction (<c>VendorProductsResponse.FactionDiscounts</c>).
/// </summary>
/// <param name="MinReputation">The reputation threshold, a real <c>dbcharacter::FactionReputations.min_reputation</c>.</param>
/// <param name="Discount">The fraction off, 0.1 for 10%.</param>
public sealed record VendorDiscountTier(uint MinReputation, float Discount);

/// <summary>
///     One stock row of a captured live store, in the order the client's window listed it.
/// </summary>
/// <param name="SdbId">The <c>dbitems::RootItem</c> sold.</param>
/// <param name="Cost">What it cost, in <see cref="VendorCatalog.CrystiteSdbId" />.</param>
/// <param name="MinReputation">
///     The reputation the row was gated behind, or <see cref="LiveVendorData.NoReputationGate" /> for
///     an unrestricted row. Live carried this as a <c>MinReputationRestriction</c> with the options
///     <c>{"faction_id":"20","reputation":"6000"}</c>.
/// </param>
/// <param name="Quantity">Copies granted per purchase; every captured row sold one.</param>
public sealed record CapturedVendorProduct(uint SdbId, uint Cost, uint MinReputation = 0, uint Quantity = 1);

/// <summary>
///     A vendor store as a live Firefall server served it: the window's identity plus its complete
///     stock. These are the only vendor prices that exist anywhere - the store catalogs lived in
///     server-only tables that were never shipped to clients and did not survive the shutdown.
/// </summary>
public sealed class CapturedVendorStore
{
    /// <summary>The <c>dbcharacter::Monster.vendor_id</c> whose window this catalog filled.</summary>
    public uint VendorId { get; init; }

    /// <summary>
    ///     The store-table id the window was opened with (<c>VendorProductsResponse.Id</c>), which the
    ///     client truncates to 32 bits and echoes back as the last field of a purchase.
    /// </summary>
    public uint StoreId { get; init; }

    /// <summary>The window title. A store name, not the NPC's: live titled this one after its POI.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    ///     The faction the store's reputation ladder belongs to (<c>VendorProductsResponse.FactionId</c>).
    ///     Live sent the POI faction here - 20 is <c>dbcharacter::Faction</c> "copa", Copacabana -
    ///     while the NPC himself (monster 956, Supply Officer Cross) is faction 1, the Accord.
    /// </summary>
    public uint FactionId { get; init; }

    /// <summary>The discount ladder, exactly as captured.</summary>
    public IReadOnlyList<VendorDiscountTier> Discounts { get; init; } = [];

    /// <summary>The stock, in window order.</summary>
    public IReadOnlyList<CapturedVendorProduct> Products { get; init; } = [];

    /// <summary>Which capture this came from, so a future one can be told apart.</summary>
    public string Capture { get; init; } = string.Empty;
}

/// <summary>
///     The vendor store catalogs captured from live servers. One exists today: the "Copacabana ARES
///     Supplies" window of build 1869 (2015-05-02), a complete <c>VendorProductsResponse</c> - 3,006
///     bytes reassembled from four fragments, every field decoded, nothing left over - plus the three
///     purchases that followed it.
/// </summary>
/// <remarks>
///     <para>
///     <b>What the capture pins down.</b> Terminal 60 - <c>dbcharacter::Monster</c> 956, "Supply
///     Officer Cross" - answered as store 2321, titled after its POI, under faction 20 (Copacabana),
///     with three discount rungs at 6000/12000/24000 reputation for 10%/20%/30% off. Those three
///     thresholds are exactly the positive <c>min_reputation</c> rows that
///     <c>dbcharacter::FactionReputations</c> carries for faction 20 in build prod-1962, and 37 of the
///     38 factions that have positive rungs have those same three - so the ladder live sent is a rule
///     the database still holds, not a Copacabana quirk (see <see cref="VendorCatalog.DiscountTiersFor" />).
///     </para>
///     <para>
///     <b>The stock.</b> Ten unrestricted field consumables at 50..2500 crystite, then 21 rows gated
///     behind 6000 Copacabana reputation at 150 crystite each, then one behind 12000. The gated rows
///     are all level-10 (or level-1), tier-8, quality-2 <c>type 1</c> gear carrying a class
///     certificate - class-specific Accord issue, which is what a POI quartermaster sold to players
///     who had earned the local reputation. Their localized names are placeholders in the client
///     database (the strings are two control bytes), because autogen/class-specific gear is named by
///     the client from its <c>autogen_group_id</c>, not from <c>name_id</c>.
///     </para>
///     <para>
///     <b>What the capture does not give.</b> Product and price guids: live minted 469931..603221 and
///     1229621..1334821, primary keys of the server-only store tables. They are opaque to the client
///     (it only echoes them back), so <see cref="VendorCatalog" /> mints its own small deterministic
///     ones instead of replaying ids that mean nothing for any other store.
///     </para>
/// </remarks>
public static class LiveVendorData
{
    /// <summary>The store id live opened terminal 60's window with.</summary>
    public const uint CopacabanaStoreId = 2321;

    /// <summary><c>dbcharacter::Monster.vendor_id</c> 60 - Supply Officer Cross, Copacabana.</summary>
    public const uint SupplyOfficerCrossVendorId = 60;

    /// <summary><c>dbcharacter::Faction</c> 20, internal name "copa": the POI the captured store belonged to.</summary>
    public const uint CopacabanaFactionId = 20;

    /// <summary>A row's reputation gate when it has none.</summary>
    public const uint NoReputationGate = 0;

    /// <summary>
    ///     The captured discount ladder: the three positive reputation rungs of the store's faction,
    ///     10%/20%/30% off. Kept apart from the store so <see cref="VendorCatalog.DiscountTiersFor" />
    ///     can apply the same fractions to another faction's rungs.
    /// </summary>
    public static readonly IReadOnlyList<VendorDiscountTier> CapturedDiscountFractions =
    [
        new VendorDiscountTier(6000, 0.1f),
        new VendorDiscountTier(12000, 0.2f),
        new VendorDiscountTier(24000, 0.3f),
    ];

    /// <summary>
    ///     "Copacabana ARES Supplies", the complete stock of vendor 60 as live served it: every price,
    ///     every gate, in window order.
    /// </summary>
    public static readonly CapturedVendorStore CopacabanaAresSupplies = new()
    {
        VendorId = SupplyOfficerCrossVendorId,
        StoreId = CopacabanaStoreId,
        Title = "Copacabana ARES Supplies",
        FactionId = CopacabanaFactionId,
        Discounts = CapturedDiscountFractions,
        Capture = "2015-05-02 build beta-1869 (GSS protocol 17122), one VendorProductsResponse and three purchases",
        Products =
        [
            // Unrestricted field supplies. The price points live used: 50, 100, 150, 250, 500, 2500.
            new CapturedVendorProduct(56811, 50),    // Scan Hammer (quality 3)
            new CapturedVendorProduct(30287, 100),   // Health Pack, Small
            new CapturedVendorProduct(30298, 100),   // Ammo Pack
            new CapturedVendorProduct(75096, 500),   // Health Pack, Medium (quality 2)
            new CapturedVendorProduct(85193, 2500),  // Health Pack, Large (quality 3)
            new CapturedVendorProduct(116563, 150),  // Grenade, Concussion
            new CapturedVendorProduct(82577, 150),   // Grenade, Flash Freeze
            new CapturedVendorProduct(82595, 150),   // Grenade, Incendiary
            new CapturedVendorProduct(82596, 150),   // Grenade, Toxic
            new CapturedVendorProduct(82597, 250),   // Adrenaline Injector

            // Class-specific tier-8 Accord issue, 6000 Copacabana reputation, 150 crystite each.
            new CapturedVendorProduct(87764, 150, 6000),
            new CapturedVendorProduct(87823, 150, 6000),
            new CapturedVendorProduct(87882, 150, 6000),
            new CapturedVendorProduct(87941, 150, 6000),
            new CapturedVendorProduct(88001, 150, 6000),
            new CapturedVendorProduct(86765, 150, 6000),
            new CapturedVendorProduct(86874, 150, 6000),
            new CapturedVendorProduct(86933, 150, 6000),
            new CapturedVendorProduct(86992, 150, 6000),
            new CapturedVendorProduct(87051, 150, 6000),
            new CapturedVendorProduct(87110, 150, 6000),
            new CapturedVendorProduct(87169, 150, 6000),
            new CapturedVendorProduct(87228, 150, 6000),
            new CapturedVendorProduct(87287, 150, 6000),
            new CapturedVendorProduct(87346, 150, 6000),
            new CapturedVendorProduct(87409, 150, 6000),
            new CapturedVendorProduct(87527, 150, 6000),
            new CapturedVendorProduct(87586, 150, 6000),
            new CapturedVendorProduct(87645, 150, 6000),
            new CapturedVendorProduct(87705, 150, 6000),  // Broken LMG
            new CapturedVendorProduct(87468, 150, 6000),

            // The one row behind the second rung.
            new CapturedVendorProduct(32755, 150, 12000),  // 1-Use Glider Pad Calldown
        ],
    };

    /// <summary>Every captured store, newest capture last.</summary>
    public static IReadOnlyList<CapturedVendorStore> Stores { get; } = [CopacabanaAresSupplies];

    /// <summary>The captured store a vendor id served, or null when that vendor was never captured.</summary>
    /// <param name="vendorId">The <c>dbcharacter::Monster.vendor_id</c> a window is opened for.</param>
    /// <returns>The captured catalog, or null.</returns>
    public static CapturedVendorStore FindStore(uint vendorId)
    {
        return Stores.FirstOrDefault(store => store.VendorId == vendorId);
    }

    /// <summary>
    ///     The price a captured store sold an item for, wherever the captures show it. This is what
    ///     makes an item's price identical on every vendor PIN serves: a real price outranks any
    ///     number PIN might otherwise have chosen for it.
    /// </summary>
    /// <param name="sdbId">The <c>dbitems::RootItem</c> to look up.</param>
    /// <param name="cost">The captured cost, in crystite.</param>
    /// <param name="minReputation">The reputation the captured row was gated behind.</param>
    /// <returns>Whether any capture shows this item being sold.</returns>
    public static bool TryGetCapturedPrice(uint sdbId, out uint cost, out uint minReputation)
    {
        foreach (var store in Stores)
        {
            foreach (var product in store.Products)
            {
                if (product.SdbId != sdbId)
                {
                    continue;
                }

                cost = product.Cost;
                minReputation = product.MinReputation;
                return true;
            }
        }

        cost = 0;
        minReputation = NoReputationGate;
        return false;
    }
}
