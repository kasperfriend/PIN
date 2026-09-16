using AeroMessages.GSS.Character.Event;
using GameServer.Data;
using GameServer.StaticDB;
using GameServer.Systems.Vendor;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Add crystite (vendor currency) to your wallet — the one vendors charge. No amount = 100k, use e.g. 'crystite 500000' for more.",
    "crystite [amount]",
    "crystite",
    "cy",
    "money",
    "cash",
    "addcrystite",
    "givecrystite")]
public class CrystiteServerCommand : ServerCommand
{
    private const uint CrystiteSdbId = VendorCatalog.CrystiteSdbId;
    private const uint DefaultAmount = 100_000;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.Inventory == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("crystite requires a player character", context);
            return;
        }

        uint amount = DefaultAmount;
        if (parameters.Length >= 1)
        {
            var parsed = ParseUIntParameter(parameters[0]);
            if (parsed == 0)
            {
                SourceFeedback("Usage: crystite [amount]  (e.g. crystite 500000)", context);
                return;
            }

            amount = parsed;
        }
        else if (parameters.Length > 1)
        {
            SourceFeedback("Usage: crystite [amount]", context);
            return;
        }

        var inventory = context.SourcePlayer.Inventory;
        // If the player hasn't finished the first respawn yet, partial updates are still off and
        // AddResource would not replicate — flip it on so the wallet update actually reaches the client.
        if (!inventory.EnablePartialUpdates)
        {
            inventory.EnablePartialUpdates = true;
        }

        var before = inventory.GetResourceQuantity(CrystiteSdbId);
        inventory.AddResource(CrystiteSdbId, amount);
        var after = inventory.GetResourceQuantity(CrystiteSdbId);

        var name = SDBInterface.GetLocalizedString(SDBInterface.GetRootItem(CrystiteSdbId)?.NameId ?? 0u) ?? "Crystite";
        SourceFeedback($"{name} +{amount}  ({before} -> {after})", context);

        var msg = new SimulateLootPickup
        {
            Item = new() { SdbId = CrystiteSdbId, Quantity = (ushort)System.Math.Min(amount, ushort.MaxValue) },
            RewardType = SimulateLootPickup.Type.General,
        };
        context.SourcePlayer.NetChannels[ChannelType.ReliableGss].SendMessage(msg, context.SourcePlayer.CharacterEntity.EntityId);

        if (before == 0)
        {
            var bagUpdate = new BagInventoryUpdate { Data = BagInventoryLayout.BuildUpdateJson(inventory.GetBagSlots()) };
            context.SourcePlayer.NetChannels[ChannelType.ReliableGss].SendMessage(bagUpdate, context.SourcePlayer.CharacterEntity.EntityId);
        }
    }
}
