using AeroMessages.GSS.Character.Event;
using GameServer.Data;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Fix a stuck 'inventory full' red blink and show bag usage. Forces a bag-model sync so the client's local 9-bag check matches the server's real inventory.",
    "bags [fix|status]",
    "bags",
    "fixbags",
    "bagfix",
    "inventorybags",
    "bagstatus")]
public class BagsServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.Inventory == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("bags requires a player character", context);
            return;
        }

        var inventory = context.SourcePlayer.Inventory;
        var slots = inventory.GetBagSlots();
        var count = inventory.BagSlotCount;
        var bagLength = BagInventoryLayout.BagLengthFor(count);
        var capacity = BagInventoryLayout.CapacityFor(bagLength);

        // Always push a fresh model — that's the fix for a client that still thinks it's full
        // after salvaging or after a cheat added new pools. The client asked for this via
        // BagInventorySettings; we can send it unsolicited and it will adopt it.
        var bagUpdate = new BagInventoryUpdate { Data = BagInventoryLayout.BuildUpdateJson(slots) };
        context.SourcePlayer.NetChannels[ChannelType.ReliableGss].SendMessage(bagUpdate, context.SourcePlayer.CharacterEntity.EntityId);

        SourceFeedback($"Bags: {count}/{capacity} slots used — 9 bags x {bagLength} ({BagInventoryLayout.NumberOfBags}*{bagLength})  —  model synced to client", context);
        if (count >= capacity)
        {
            SourceFeedback("Bags are FULL — salvage or 'clearbags' to free slots, then 'bags' again. Vendors will blink red while full.", context);
        }
        else if (count >= capacity - 5)
        {
            SourceFeedback($"Almost full ({capacity - count} free). Consider salvaging before buying more.", context);
        }
        else
        {
            SourceFeedback($"{capacity - count} free slots — vendors should work. If they still blink red, check 'balance' for crystite.", context);
        }
    }
}
