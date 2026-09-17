using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Unlocks a warpaint (a type-15 <c>dbitems::RootItem</c>) for the character.</summary>
public class UnlockWarpaintsCommand : Command, ICommand
{
    private UnlockWarpaintsCommandDef Params;

    public UnlockWarpaintsCommand(UnlockWarpaintsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.WarpaintId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockWarpaintsCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockWarpaintsCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        PlayerRewards.Unlock(context, character, CharacterUnlocks.Warpaints, Params.WarpaintId);
        return true;
    }
}
