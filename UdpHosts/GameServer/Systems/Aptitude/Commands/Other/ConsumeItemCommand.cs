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

        if (!inventory.ConsumeItemBySdbId(context.AbilityModuleId))
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

        return true;
    }
}
