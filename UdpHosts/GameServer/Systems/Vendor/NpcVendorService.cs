using AeroMessages.GSS.Generic;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.Vendor;

/// <summary>
///     Answers the client's vendor-window requests for NPC vendors. Completing an interaction on a
///     vendor NPC (<see cref="Aptitude.Commands.Interaction.EndInteractionCommand" />) authorizes the
///     terminal type 7 with the monster's <c>vendor_id</c>; the client then opens the vendor UI and
///     asks for the stock list with <c>VendorProductRequest</c>.
/// </summary>
/// <remarks>
///     The build's client database carries no vendor stock lists - the only vendor-ish tables are the
///     <c>dbitems::VendorToken*</c> rows of the web token machines - so windows open empty rather
///     than with invented goods. Purchases are declined for the same reason. The window itself, the
///     interaction that opens it and the terminal authorization around it are the parts that are
///     implemented.
/// </remarks>
public static class NpcVendorService
{
    /// <summary>The terminal type whose window asks for products (see <c>EndInteractionCommand</c>).</summary>
    public const byte VendorTerminalType = 7;

    private static readonly ILogger Logger = Log.ForContext(typeof(NpcVendorService));

    /// <summary>
    ///     Builds the product list response for the vendor the player is authorized to use, or null
    ///     when the request does not match an authorized vendor terminal.
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
            title = "Vendor";
        }

        Logger.Information(
            "VendorProductRequest: opening vendor {VendorId} ({Title}) for {Player}",
            requestedVendorId,
            title,
            player.CharacterEntity);

        // No stock list data survives in the client database, so the window opens empty instead of
        // inventing goods. Interaction, authorization and window all work; shelving goods is a
        // content question (see the class remarks).
        return new VendorProductsResponse
        {
            VendorId = requestedVendorId,
            Id = terminal.TerminalEntityId,
            RemoteId = requestedVendorId,
            Title = title,
            FactionId = vendorNpc?.HostilityInfo.FactionId ?? 0,
            FactionDiscounts = [],
            Products = [],
        };
    }

    /// <summary>
    ///     The purchase decline the vendor handlers answer with: without stock data there is nothing
    ///     to sell, but the client still deserves a response instead of a hung window.
    /// </summary>
    public const string PurchaseDeclinedCode = "VENDOR_OUT_OF_STOCK";
}
