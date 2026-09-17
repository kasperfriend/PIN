using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     The chain node that spends the consumable an activation came from. The combat controller
///     hands the chain the consumable's sdb id as its "module" (<see cref="Context.AbilityModuleId" />),
///     so this takes one copy out of the activating player's inventory - from the stack that
///     consumables live in, or a guid copy of it when an older inventory still holds those. A
///     chain whose caster does not actually hold the item fails here, so the free use does not
///     even cost a cooldown; the controller already refuses such activations up front, this is
///     the backstop.
///     <para>
///         224 consumable chains spend the item before their <c>InstantActivation</c> gate runs
///         (<c>TimeCooldown, ConsumeItem, ..., InstantActivation</c>), and 48 of them (Ammo Pack 30192,
///         Arcfold Beacon 34132, the Pyrotechnics fuses) have no <c>TimeCooldown</c> check ahead of the
///         spend, so a use inside the cooldown window used to fail the activation with the item already
///         gone. The command therefore queues a rollback that hands the copy back when the root
///         activation fails (<see cref="Context.ActivationRollbacks" />).
///     </para>
/// </summary>
public class ConsumeItemCommand : Command, ICommand
{
    private ConsumeItemCommandDef Params;

    public ConsumeItemCommand(ConsumeItemCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.AbilityModuleId == 0)
        {
            return true;
        }

        if (context.Initiator is not CharacterEntity { IsPlayerControlled: true } character)
        {
            // NPCs and deployables own no backpack; there is nothing to take from them.
            return true;
        }

        var inventory = character.Player?.Inventory;
        if (inventory == null)
        {
            return true;
        }

        if (context.ActivatingItemConsumedImplicitly)
        {
            // A reward node earlier in this activation already spent the item (see PlayerRewards.ConsumeActivatingItem);
            // this node is the explicit spend it stood in for.
            context.ActivatingItemConsumedImplicitly = false;
            return true;
        }

        uint itemSdbId = context.AbilityModuleId;
        if (!inventory.ConsumeItemBySdbId(itemSdbId, 1, out var removedItems))
        {
            Logger.Warning(
                "{Command} {CommandId}: {Character} has no {ItemSdbId} left for ability {AbilityId}; failing the activation",
                nameof(ConsumeItemCommand),
                Params.Id,
                character,
                context.AbilityModuleId,
                context.AbilityId);
            return false;
        }

        context.ActivatingItemConsumed = true;
        context.ActivationRollbacks.Add(() =>
        {
            if (removedItems.Count == 0)
            {
                inventory.AddResource(itemSdbId, 1);
            }
            else
            {
                foreach (var item in removedItems)
                {
                    inventory.RestoreItem(item);
                }
            }

            Logger.Information(
                "{Command} {CommandId}: activation of ability {AbilityId} failed after the item was spent; returned {ItemSdbId} to {Character}",
                nameof(ConsumeItemCommand),
                Params.Id,
                context.AbilityId,
                itemSdbId,
                character);
        });

        return true;
    }
}
