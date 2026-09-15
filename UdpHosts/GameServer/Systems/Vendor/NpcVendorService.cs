using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AeroMessages.GSS.Character.Command;
using AeroMessages.GSS.Generic;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Vendor;

/// <summary>
///     The vendor windows of NPC shops. Completing an interaction on a vendor NPC
///     (<see cref="Aptitude.Commands.Interaction.EndInteractionCommand" />) authorizes terminal type 7
///     with the monster's <c>vendor_id</c>; the client then opens the window and asks for the stock
///     with <c>VendorProductRequest</c>, and buys with <c>VendorPurchaseRequest</c>.
/// </summary>
/// <remarks>
///     <para>
///     Stock comes from the client database via <see cref="VendorCatalog" />: token-machine vendors
///     list the prizes their machine tables author, every other vendor a curated shelf of real
///     consumables priced in crystite (the live store catalogs were server-only and did not
///     survive, so those prices are emulated).
///     </para>
///     <para>
///     Product and price guids deterministically encode (vendor id, stock index), so a purchase is
///     re-derived from the catalog when it arrives - there is no per-player shop state to lose.
///     </para>
///     <para>
///     The wire shapes here are the ones a live 2015 capture of a real vendor session shows (build
///     1869, one <c>VendorProductRequest</c>, one <c>VendorProductsResponse</c>, three purchases):
///     small store and guid ids, both root-namespace answers sent against the shard entity rather
///     than the character, and an empty <c>Code</c> on a successful purchase. Deviating from any of
///     those made the client's Buy button do nothing at all - see <see cref="VendorCatalog" /> for
///     why the id sizes matter.
///     </para>
/// </remarks>
public static class NpcVendorService
{
    /// <summary>The terminal type whose window asks for products (see <c>EndInteractionCommand</c>).</summary>
    public const byte VendorTerminalType = 7;

    /// <summary>
    ///     Response code a successful purchase carries: none. A live 2015 capture shows the real
    ///     server answering a purchase with an empty string, and the client's own code comments say
    ///     it falls back to its success text when the code is empty and <c>Success</c> is set - so
    ///     sending a literal "PURCHASE_SUCCESSFUL" risks the window displaying that token instead.
    /// </summary>
    public const string PurchaseSuccessfulCode = "";

    /// <summary>Response code when the player cannot afford the selected price.</summary>
    public const string InsufficientFundsCode = "INSUFFICIENT_FUNDS";

    /// <summary>Response code when the guids do not match a catalog entry of the requested vendor.</summary>
    public const string InvalidProductCode = "INVALID_PRODUCT";

    /// <summary>Response code when the player has no authorized vendor terminal open.</summary>
    public const string VendorUnavailableCode = "VENDOR_UNAVAILABLE";

    private static readonly ILogger Logger = Log.ForContext(typeof(NpcVendorService));

    /// <summary>
    ///     The catalog source the service answers from. Production uses the database-backed
    ///     <see cref="VendorCatalog.Build(uint)" />; tests swap in a fixed shelf.
    /// </summary>
    internal static Func<uint, IReadOnlyList<VendorCatalogEntry>> BuildCatalog { get; set; } = VendorCatalog.Build;

    /// <summary>
    ///     Builds the stock response for the vendor the player is authorized to use, or null when the
    ///     request does not match an authorized vendor terminal.
    /// </summary>
    /// <param name="player">The player whose client opened the vendor window.</param>
    /// <param name="requestedVendorId">The terminal id of the client's <c>VendorProductRequest</c>.</param>
    /// <returns>The response to send, or null when the request is not for an authorized vendor.</returns>
    public static VendorProductsResponse BuildProductsResponse(IPlayer player, uint requestedVendorId)
    {
        var character = player.CharacterEntity;
        var terminal = character.AuthorizedTerminal;

        if (terminal.TerminalType != VendorTerminalType || terminal.TerminalId != requestedVendorId)
        {
            Logger.Debug(
                "VendorProductRequest for vendor {VendorId} ignored: the player's authorized terminal is type {TerminalType} id {TerminalId}",
                requestedVendorId,
                terminal.TerminalType,
                terminal.TerminalId);
            return null;
        }

        CharacterEntity vendorNpc = null;
        if (terminal.TerminalEntityId != 0)
        {
            character.Shard.Entities.TryGetValue(terminal.TerminalEntityId & 0xffffffffffffff00, out var entity);
            vendorNpc = entity as CharacterEntity;
        }

        string title = vendorNpc?.StaticInfo.DisplayName;
        if (string.IsNullOrWhiteSpace(title) || title == "_noname")
        {
            // NPC display names replicate as "_noname"; the row's localized name is what the
            // client would show (e.g. Corporal Belle), so title the window with that.
            title = vendorNpc != null
                ? SDBInterface.GetLocalizedString(vendorNpc.StaticInfo.NameLocalizationId)
                : null;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Vendor";
        }

        var stock = BuildCatalog(requestedVendorId);
        var products = new VendorProduct[stock.Count];
        for (var i = 0; i < stock.Count; i++)
        {
            var entry = stock[i];
            products[i] = new VendorProduct
            {
                GUID = VendorCatalog.ProductGuid(requestedVendorId, entry.Index),
                SdbId = entry.SdbId,
                Quantity = entry.Quantity,
                Duration = 0,
                Prices =
                [
                    new VendorProductPrice
                    {
                        GUID = VendorCatalog.PriceGuid(requestedVendorId, entry.Index),
                        CurrencyType = "resource",
                        CurrencyRemoteId = entry.CurrencySdbId,
                        Amount = entry.Cost,
                    },
                ],
                Restrictions = [],
                Priority = (byte)(i & 0xFF),
            };
        }

        Logger.Information(
            "VendorProductRequest: opening vendor {VendorId} ({Title}) as store {StoreId} with {Products} product(s), guids {FirstGuid}..{LastGuid}, for {Player}",
            requestedVendorId,
            title,
            VendorCatalog.StoreId(requestedVendorId),
            products.Length,
            products.Length > 0 ? products[0].GUID : 0ul,
            products.Length > 0 ? products[products.Length - 1].GUID : 0ul,
            character);

        return new VendorProductsResponse
        {
            VendorId = requestedVendorId,

            // The window id the client echoes back in a purchase. Live sent a store-table id here
            // (2321 for terminal 60); a 64-bit entity id - what this used to send - is truncated by
            // the client and mangled by its UI's double arithmetic. See VendorCatalog.StoreId.
            Id = VendorCatalog.StoreId(requestedVendorId),
            RemoteId = requestedVendorId,
            Title = title,
            FactionId = vendorNpc?.HostilityInfo.FactionId ?? 0,
            FactionDiscounts = [],
            Products = products,
        };
    }

    /// <summary>
    ///     The store id a purchase request carries, read from whichever shape the client sends.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     AeroMessages models the tail of <c>VendorPurchaseRequest</c> as three optional fields
    ///     (<c>HaveUnk2/ScuffedVendorID</c>, <c>HaveUnk3/VendorRemoteID</c>, <c>HaveUnk4/Unk4</c>),
    ///     which is a misreading of what a live 2015 capture actually shows: the request is a fixed
    ///     36 bytes ending in a plain uint32 - <c>00 00 00 00 00 00 00 00</c> (unknown), the buyer's
    ///     entity id, the product guid, the price guid, then <c>11 09 00 00</c> = 2321, exactly the
    ///     <c>VendorProductsResponse.Id</c> the window had been opened with. Those bytes unpack as
    ///     HaveUnk2=0x11, HaveUnk3=0x09, HaveUnk4=0x00 with one byte left over, so neither optional
    ///     field is ever filled in and the modeled values read as zero.
    ///     </para>
    ///     <para>
    ///     Take the modeled fields when the client does fill them, and otherwise read the uint32 the
    ///     capture shows. The value is only a cross-check - <see cref="ResolvePurchaseVendorId" />
    ///     still decides which shop a purchase belongs to from the authorized terminal.
    ///     </para>
    /// </remarks>
    /// <param name="request">The unpacked request.</param>
    /// <param name="payload">The request's raw message body (everything after the GSS header).</param>
    /// <returns>The store id the client named, or 0 when it named none.</returns>
    internal static uint ReadStoreId(VendorPurchaseRequest request, ReadOnlySpan<byte> payload)
    {
        if (request == null)
        {
            return 0;
        }

        if (request.HaveUnk2 == 1 && request.ScuffedVendorID != 0)
        {
            return request.ScuffedVendorID;
        }

        if (request.HaveUnk3 == 1 && request.VendorRemoteID != 0)
        {
            return request.VendorRemoteID;
        }

        // 8 unknown + 8 buyer + 8 product + 8 price + 4 store id.
        const int storeIdOffset = 32;
        const int storeIdEnd = storeIdOffset + sizeof(uint);
        if (payload.Length >= storeIdEnd)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(storeIdOffset, sizeof(uint)));
        }

        return 0;
    }

    /// <summary>
    ///     Which vendor a purchase belongs to: the player's authorized vendor terminal when one is
    ///     open, otherwise whatever id the packet named (which then fails the terminal check in
    ///     <see cref="TryPurchase" />).
    /// </summary>
    /// <remarks>
    ///     The id a client sends is the store id its window was opened with (<see cref="ReadStoreId" />),
    ///     which for this server is the vendor id itself - so the two normally agree. The authorized
    ///     terminal still wins: it is server-side truth about which shop the player has open, and the
    ///     product/price guids have to decode to that same vendor anyway, so a purchase can never
    ///     escape its own shop no matter what the packet claims.
    /// </remarks>
    /// <param name="player">The buying player.</param>
    /// <param name="packetVendorId">The vendor id the client's request named, if any.</param>
    /// <returns>The vendor id the purchase is validated against.</returns>
    public static uint ResolvePurchaseVendorId(IPlayer player, uint packetVendorId)
    {
        var terminal = player.CharacterEntity.AuthorizedTerminal;
        if (terminal.TerminalType == VendorTerminalType && terminal.TerminalId != 0)
        {
            return terminal.TerminalId;
        }

        return packetVendorId;
    }

    /// <summary>
    ///     Executes a vendor purchase: validates the guids against the catalog, charges the currency,
    ///     grants the goods. Answers every request with a response so the client window never hangs.
    /// </summary>
    /// <param name="player">The buying player.</param>
    /// <param name="requestedVendorId">The vendor id the client names in the request.</param>
    /// <param name="productGuid">The product guid the client selected.</param>
    /// <param name="priceGuid">The price guid the client selected.</param>
    /// <returns>The response to send back.</returns>
    public static VendorPurchaseResponse TryPurchase(IPlayer player, uint requestedVendorId, ulong productGuid, ulong priceGuid)
    {
        var character = player.CharacterEntity;

        VendorPurchaseResponse Decline(byte success, string code)
        {
            return new VendorPurchaseResponse
            {
                Success = success,
                ProductId = productGuid,
                PriceId = priceGuid,
                VendorId = requestedVendorId,
                Code = code,
            };
        }

        var terminal = character.AuthorizedTerminal;
        if (terminal.TerminalType != VendorTerminalType || terminal.TerminalId != requestedVendorId)
        {
            Logger.Information("VendorPurchase from vendor {VendorId} declined: no matching authorized terminal", requestedVendorId);
            return Decline(0, VendorUnavailableCode);
        }

        if (!VendorCatalog.TryDecodeGuid(productGuid, out uint productVendorId, out int index, out bool productIsPrice)
            || productIsPrice
            || productVendorId != requestedVendorId
            || !VendorCatalog.TryDecodeGuid(priceGuid, out uint priceVendorId, out int priceIndex, out bool priceIsPrice)
            || !priceIsPrice
            || priceVendorId != requestedVendorId
            || priceIndex != index)
        {
            Logger.Information(
                "VendorPurchase from vendor {VendorId} declined: guids {ProductGuid:X16}/{PriceGuid:X16} do not decode to a catalog entry",
                requestedVendorId,
                productGuid,
                priceGuid);
            return Decline(0, InvalidProductCode);
        }

        var stock = BuildCatalog(requestedVendorId);
        if (index < 0 || index >= stock.Count)
        {
            Logger.Information("VendorPurchase from vendor {VendorId} declined: stock index {Index} out of range", requestedVendorId, index);
            return Decline(0, InvalidProductCode);
        }

        var entry = stock[index];
        var inventory = player.Inventory;
        if (inventory == null)
        {
            Logger.Warning("VendorPurchase declined: {Player} has no inventory", character);
            return Decline(0, VendorUnavailableCode);
        }

        if (!inventory.ConsumeResource(entry.CurrencySdbId, entry.Cost))
        {
            Logger.Information(
                "VendorPurchase of {Item} (x{Quantity}) declined: {Player} cannot pay {Cost} of currency {CurrencyId}",
                entry.Name,
                entry.Quantity,
                character,
                entry.Cost,
                entry.CurrencySdbId);
            return Decline(0, InsufficientFundsCode);
        }

        Grant(inventory, entry);

        Logger.Information(
            "VendorPurchase: {Player} bought {Item} (x{Quantity}) for {Cost} of currency {CurrencyId} at vendor {VendorId}",
            character,
            entry.Name,
            entry.Quantity,
            entry.Cost,
            entry.CurrencySdbId,
            requestedVendorId);

        return Decline(1, PurchaseSuccessfulCode);
    }

    /// <summary>
    ///     Hands the purchase to the player: currency-style items (RootItem type Basic, like crystite
    ///     itself or merit points) top up the matching resource pool, everything else lands in the
    ///     inventory as a real item.
    /// </summary>
    private static void Grant(CharacterInventory inventory, VendorCatalogEntry entry)
    {
        var item = SDBInterface.GetRootItem(entry.SdbId);
        if (item != null && (ItemType)item.Type == ItemType.Basic)
        {
            inventory.AddResource(entry.SdbId, entry.Quantity);
            return;
        }

        for (uint copy = 0; copy < entry.Quantity; copy++)
        {
            inventory.CreateItem(entry.SdbId);
        }
    }
}
