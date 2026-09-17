using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>Adds or takes a resource (crystite, a gene sample) from the owner; a take the owner cannot cover fails.</summary>
public class ModifyOwnerResourcesCommand : Command, ICommand
{
    private ModifyOwnerResourcesCommandDef Params;

    public ModifyOwnerResourcesCommand(ModifyOwnerResourcesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.ResourceSdbId == 0 || Params.Quantity == 0)
        {
            Logger.Warning("{Command} {CommandId} has no resource authored yet (item {Item}); nothing changed", nameof(ModifyOwnerResourcesCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character?.Player?.Inventory == null)
        {
            return false;
        }

        if (Params.Quantity > 0)
        {
            return PlayerRewards.GrantItem(context, character, Params.ResourceSdbId, (uint)Params.Quantity);
        }

        return PlayerRewards.TakeItem(context, character, Params.ResourceSdbId, (uint)(-Params.Quantity));
    }
}
