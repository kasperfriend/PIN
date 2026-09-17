using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Self;

/// <summary>
///     Hands the owner an item. Three shapes, all from the same row:
///     <list type="bullet">
///         <item>plain grant (<c>item_sdb_id</c> x <c>quantity</c>): Campaign Tokens, rentals, bundles, experience packs;</item>
///         <item>exchange (<c>cost_sdb_id</c> too): a Turbo Upgrade Kit takes the plain LGV, fifty fragments become one component;</item>
///         <item>roll (<c>loot_table_id</c>): a Secure Locker takes its key and rolls the locker's master table.</item>
///     </list>
///     The cost is checked before anything is granted, so a chain whose <c>RequireHasItem</c> guard was
///     skipped still cannot take what is not there; everything is undone if the activation fails later.
/// </summary>
public class GrantOwnerItemCommand : Command, ICommand
{
    private GrantOwnerItemCommandDef Params;

    public GrantOwnerItemCommand(GrantOwnerItemCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.ItemSdbId == 0 && Params.LootTableId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no item authored yet (item {Item}); nothing granted", nameof(GrantOwnerItemCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character?.Player?.Inventory == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to grant to", nameof(GrantOwnerItemCommand), Params.Id);
            return false;
        }

        if (Params.CostSdbId != 0)
        {
            uint costQuantity = Params.CostQuantity == 0 ? 1 : Params.CostQuantity;
            if (Params.CostSdbId == context.AbilityModuleId)
            {
                // Fifty fragments become one component: the stack the player activated is the cost,
                // so there is no separate "spend the activating item" on top of it.
                context.ActivatingItemConsumed = true;
            }

            if (!PlayerRewards.TakeItem(context, character, Params.CostSdbId, costQuantity))
            {
                Logger.Information(
                    "{Command} {CommandId}: {Character} lacks {Cost} x{Quantity}; nothing granted",
                    nameof(GrantOwnerItemCommand),
                    Params.Id,
                    character,
                    PlayerRewards.ItemName(Params.CostSdbId),
                    costQuantity);
                return false;
            }
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        if (Params.ItemSdbId != 0)
        {
            PlayerRewards.GrantItem(context, character, Params.ItemSdbId, Params.Quantity == 0 ? 1 : Params.Quantity);
        }

        if (Params.LootTableId != 0)
        {
            var awards = PlayerRewards.GrantLoot(context, character, Params.LootTableId);
            if (awards.Count == 0)
            {
                Logger.Warning("{Command} {CommandId}: loot table {LootTable} awarded nothing to {Character}", nameof(GrantOwnerItemCommand), Params.Id, Params.LootTableId, character);
            }
        }

        return true;
    }
}
