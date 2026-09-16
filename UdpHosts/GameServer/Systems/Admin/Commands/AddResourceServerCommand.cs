using System;
using AeroMessages.GSS.Character.Event;
using GameServer.Data;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Add any stacked currency/resource to your wallet by SDB id. Works for crystite (10), tokens, salvage mats, etc. Bag-full or wrong-type? Use 'resource' for pools, 'createitem' for gear.",
    "resource <sdbId> [amount]",
    "resource",
    "addresource",
    "giveresource",
    "addcurrency",
    "givecurrency",
    "currency")]
public class AddResourceServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.Inventory == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("resource requires a player character", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback("Usage: resource <sdbId> [amount]  e.g. resource 10 500000  (10 = crystite)", context);
            // hint the wallet
            var hint = context.SourcePlayer.Inventory.GetResourceQuantity(10);
            SourceFeedback($"You have {hint} crystite (10). Try: resource 10 100000  or  wallet  or  balance", context);
            return;
        }

        uint sdbId = ParseUIntParameter(parameters[0]);
        if (sdbId == 0)
        {
            SourceFeedback("Invalid sdbId (use a numeric dbitems::RootItem id, e.g. 10 for crystite)", context);
            return;
        }

        uint amount = 1;
        if (parameters.Length >= 2)
        {
            amount = ParseUIntParameter(parameters[1]);
            if (amount == 0)
            {
                SourceFeedback("Amount must be >= 1", context);
                return;
            }
        }

        var inventory = context.SourcePlayer.Inventory;
        if (!inventory.EnablePartialUpdates)
        {
            inventory.EnablePartialUpdates = true;
        }

        var before = inventory.GetResourceQuantity(sdbId);
        // Name for feedback — works even if SDB not loaded (falls back to id)
        var item = SDBInterface.GetRootItem(sdbId);
        var displayName = item != null ? (SDBInterface.GetLocalizedString(item.NameId) ?? $"item {sdbId}") : $"item {sdbId}";

        inventory.AddResource(sdbId, amount);
        var after = inventory.GetResourceQuantity(sdbId);

        SourceFeedback($"{displayName} ({sdbId}) +{amount}  ({before} -> {after})", context);

        var msg = new SimulateLootPickup
        {
            Item = new() { SdbId = sdbId, Quantity = (ushort)System.Math.Min(amount, ushort.MaxValue) },
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
