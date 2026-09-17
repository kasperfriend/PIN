using System.Linq;
using GameServer.StaticDB;
using GameServer.Systems.Vendor;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Show your wallet: every stacked resource pool you carry (crystite, tokens, mats) with SDB id and quantity. The ids are what 'resource <sdbId>' wants.",
    "balance",
    "balance",
    "showwallet",
    "showcurrencies",
    "currencies_list",
    "listcurrencies")]
public class BalanceServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.Inventory == null)
        {
            SourceFeedback("balance requires a player character", context);
            return;
        }

        var inventory = context.SourcePlayer.Inventory;

        // Collect the resource SDB ids the player actually carries
        // We do it by iterating over the returned bag slots and filtering for resource pools (guid 0)
        // — but CharacterInventory exposes GetResourceQuantity and GetBagSlots; use both.
        var slots = inventory.GetBagSlots();
        var resources = slots.Where(s => s.ItemGuid == 0).OrderBy(s => s.ItemSdbId).ToList();

        if (resources.Count == 0)
        {
            SourceFeedback("Wallet is empty. Try 'wallet' or 'crystite 100000' or 'resource 10 500000'.", context);
            return;
        }

        // Header
        SourceFeedback($"Wallet: {resources.Count} pool(s), {inventory.BagSlotCount} bag slots used", context);

        // Show up to ~40 lines in chat (debug chat is rate-limited, rest goes to log)
        int shown = 0;
        foreach (var slot in resources)
        {
            var item = SDBInterface.GetRootItem(slot.ItemSdbId);
            var name = item != null ? (SDBInterface.GetLocalizedString(item.NameId) ?? $"item {slot.ItemSdbId}") : $"item {slot.ItemSdbId}";
            var isCrystite = slot.ItemSdbId == VendorCatalog.CrystiteSdbId ? "  <- vendor currency" : "";
            var line = $"{slot.ItemSdbId,8}  {slot.Quantity,10:N0}  {name}{isCrystite}";
            // Log always
            Logger.Information("{Line}", line);
            if (shown < 35)
            {
                SourceFeedback(line, context);
                shown++;
            }
        }

        if (resources.Count > shown)
        {
            SourceFeedback($"... and {resources.Count - shown} more (see server log)", context);
        }

        var crystite = inventory.GetResourceQuantity(VendorCatalog.CrystiteSdbId);
        SourceFeedback($"Crystite (10): {crystite:N0}  - vendors charge this. 'crystite 100000' to add more.", context);
        SourceFeedback("Add any: 'resource <sdbId> <amount>'  e.g. 'resource 10 250000'. Fill all: 'wallet' or 'wallet all'.", context);
    }
}
