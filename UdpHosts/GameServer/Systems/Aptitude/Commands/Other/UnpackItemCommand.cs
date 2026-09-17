using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Opens a package: the packaged item (a pet, an LGV, a glider) replaces the package. The package
///     is the consumable being activated, so the chain has no <c>ConsumeItem</c> of its own; a package
///     the player no longer holds fails the activation.
/// </summary>
public class UnpackItemCommand : Command, ICommand
{
    private UnpackItemCommandDef Params;

    public UnpackItemCommand(UnpackItemCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.ItemSdbId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no packaged item authored yet (item {Item}); nothing unpacked", nameof(UnpackItemCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character?.Player?.Inventory == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unpack for", nameof(UnpackItemCommand), Params.Id);
            return false;
        }

        uint package = Params.PackageSdbId != 0 ? Params.PackageSdbId : context.AbilityModuleId;
        if (package == context.AbilityModuleId)
        {
            if (!PlayerRewards.ConsumeActivatingItem(context, character))
            {
                return false;
            }
        }
        else if (package != 0 && !PlayerRewards.TakeItem(context, character, package, 1))
        {
            Logger.Information("{Command} {CommandId}: {Character} holds no {Package} to unpack", nameof(UnpackItemCommand), Params.Id, character, PlayerRewards.ItemName(package));
            return false;
        }

        PlayerRewards.GrantItem(context, character, Params.ItemSdbId, 1);
        return true;
    }
}
