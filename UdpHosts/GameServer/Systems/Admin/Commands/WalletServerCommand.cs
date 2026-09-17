using System.Collections.Generic;
using AeroMessages.GSS.Character.Event;
using GameServer.Data;
using GameServer.StaticDB;
using GameServer.Systems.Vendor;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand(
    "Fill your wallet for vendors: crystite + vending tokens. No grind, just buy. 'wallet' gives 500k cy + 100 tokens, 'wallet all' tops every fallback resource too.",
    "wallet [all|amount]",
    "wallet",
    "fillwallet",
    "givewallet",
    "refill",
    "currencies")]
public class WalletServerCommand : ServerCommand
{
    private const uint DefaultCrystite = 500_000;
    private const uint DefaultTokenAmount = 100;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.Inventory == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("wallet requires a player character", context);
            return;
        }

        var inventory = context.SourcePlayer.Inventory;
        if (!inventory.EnablePartialUpdates)
        {
            inventory.EnablePartialUpdates = true;
        }

        // Parse: wallet [all]  or wallet <amount>  or wallet all <amount>
        bool giveAll = false;
        uint crystiteAmount = DefaultCrystite;
        uint tokenAmount = DefaultTokenAmount;

        if (parameters.Length >= 1)
        {
            if (parameters[0].Equals("all", System.StringComparison.OrdinalIgnoreCase))
            {
                giveAll = true;
                if (parameters.Length >= 2)
                {
                    var parsed = ParseUIntParameter(parameters[1]);
                    if (parsed != 0)
                    {
                        crystiteAmount = parsed;
                        tokenAmount = parsed;
                    }
                }
            }
            else
            {
                var parsed = ParseUIntParameter(parameters[0]);
                if (parsed != 0)
                {
                    crystiteAmount = parsed;
                    tokenAmount = System.Math.Max(10, parsed / 5000); // scale tokens with cy
                }
                else
                {
                    SourceFeedback("Usage: wallet [amount]  or  wallet all [amount]  e.g. wallet 1000000", context);
                    return;
                }
            }
        }

        var awarded = new List<string>();

        // 1) Crystite — the quartermaster currency every PIN vendor charges (and the live capture's)
        {
            var before = inventory.GetResourceQuantity(VendorCatalog.CrystiteSdbId);
            inventory.AddResource(VendorCatalog.CrystiteSdbId, crystiteAmount);
            var after = inventory.GetResourceQuantity(VendorCatalog.CrystiteSdbId);
            var name = SDBInterface.GetLocalizedString(SDBInterface.GetRootItem(VendorCatalog.CrystiteSdbId)?.NameId ?? 0u) ?? "Crystite";
            awarded.Add($"{name} +{crystiteAmount} ({before}->{after})");
            SendPickup(context, VendorCatalog.CrystiteSdbId, crystiteAmount);
        }

        // 2) Vending tokens — every VendorTokenKeyItems row (machines 5, 105 etc. take 85771)
        var keyItems = SDBInterface.GetVendorTokenKeyItems();
        var tokenIds = new HashSet<uint>();
        if (keyItems != null)
        {
            foreach (var ki in keyItems)
            {
                if (ki.KeyItemId != 0)
                {
                    tokenIds.Add(ki.KeyItemId);
                }
            }
        }

        // Fallback when SDB not loaded or no rows (tests)
        if (tokenIds.Count == 0)
        {
            tokenIds.Add(85771); // Accord token — the one the two live machines take
        }

        foreach (var tokenId in tokenIds)
        {
            var before = inventory.GetResourceQuantity(tokenId);
            inventory.AddResource(tokenId, tokenAmount);
            var after = inventory.GetResourceQuantity(tokenId);
            var item = SDBInterface.GetRootItem(tokenId);
            var name = item != null ? (SDBInterface.GetLocalizedString(item.NameId) ?? $"item {tokenId}") : $"item {tokenId}";
            awarded.Add($"{name} ({tokenId}) +{tokenAmount} ({before}->{after})");
            SendPickup(context, tokenId, tokenAmount);
        }

        // 3) Optionally top up every fallback resource pool (research, mats, etc.) so
        //    salvage / crafting never starves either. These are the stacks the admin
        //    sandbox ships with (characters.example.json) — a fresh account starts with none.
        if (giveAll)
        {
            foreach ((uint sdbId, uint fallbackQty) in HardcodedCharacterData.FallbackInventoryResources)
            {
                if (sdbId == VendorCatalog.CrystiteSdbId || tokenIds.Contains(sdbId))
                {
                    continue; // already handled
                }

                // Give at least fallbackQty or 100, whichever is larger, but not absurd
                uint give = System.Math.Max(fallbackQty, 100);
                give = System.Math.Min(give, 10_000); // cap
                var before = inventory.GetResourceQuantity(sdbId);
                inventory.AddResource(sdbId, give);
                var after = inventory.GetResourceQuantity(sdbId);
                if (after != before)
                {
                    // don't spam every mat — only log the first few, full list goes to console
                    if (awarded.Count < 8)
                    {
                        var item = SDBInterface.GetRootItem(sdbId);
                        var name = item != null ? (SDBInterface.GetLocalizedString(item.NameId) ?? $"item {sdbId}") : $"item {sdbId}";
                        awarded.Add($"{name} ({sdbId}) +{give}");
                    }
                }
            }

            awarded.Add($"(+ topped up {HardcodedCharacterData.FallbackInventoryResources.Length} fallback pools)");
        }

        // Push the updated bag model — the client enforces it locally and a stale model
        // (e.g. after 'wallet all' which adds many new pools) would keep refusing purchases
        // with a red flash and no request on the wire.
        var bagUpdate = new BagInventoryUpdate { Data = BagInventoryLayout.BuildUpdateJson(inventory.GetBagSlots()) };
        context.SourcePlayer.NetChannels[ChannelType.ReliableGss].SendMessage(bagUpdate, context.SourcePlayer.CharacterEntity.EntityId);

        SourceFeedback($"Wallet filled: {string.Join(", ", awarded)}", context);
        if (giveAll)
        {
            SourceFeedback("Tip: 'balance' shows every pool you now carry. 'resource <sdbId> <amount>' tops any single one.", context);
        }
        else
        {
            SourceFeedback("Tip: 'wallet all' tops every research/mat pool too. 'balance' shows your wallet. 'resource 10 100000' adds any id.", context);
        }
    }

    private static void SendPickup(ServerCommandContext context, uint sdbId, uint amount)
    {
        var msg = new SimulateLootPickup
        {
            Item = new() { SdbId = sdbId, Quantity = (ushort)System.Math.Min(amount, ushort.MaxValue) },
            RewardType = SimulateLootPickup.Type.General,
        };
        context.SourcePlayer.NetChannels[ChannelType.ReliableGss].SendMessage(msg, context.SourcePlayer.CharacterEntity.EntityId);
    }
}
