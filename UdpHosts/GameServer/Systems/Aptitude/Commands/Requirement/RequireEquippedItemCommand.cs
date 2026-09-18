using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     <c>aptfs::RequireEquippedItemCommandDef</c>: every target must have an item of the row's
///     type equipped - as a loadout-slotted gear piece or with the inventory's equipped flag set,
///     so both loadout-native gear and player-swapped gear answer "yes". Stacks that cannot carry
///     items cannot satisfy it. Params.Negate mirrors it for the "does not have it equipped"
///     chains.
/// </summary>
public class RequireEquippedItemCommand : Command, ICommand
{
    private RequireEquippedItemCommandDef Params;

    public RequireEquippedItemCommand(RequireEquippedItemCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        if (context.Targets.Count > 0)
        {
            result = true;
            foreach (IAptitudeTarget target in context.Targets)
            {
                bool targetResult = target is CharacterEntity character && IsEquipped(character);
                if (!targetResult)
                {
                    result = false;
                    break;
                }
            }
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }

    private bool IsEquipped(CharacterEntity character)
    {
        if (character.CurrentLoadout != null && character.CurrentLoadout.SlottedItems.ContainsValue(Params.ItemId))
        {
            return true;
        }

        return character.Player?.Inventory != null
            && character.Player.Inventory.HasItemEquipped(Params.ItemId);
    }
}
