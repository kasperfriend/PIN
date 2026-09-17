using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Unlocks an ornament group (<c>dbvisualrecords::OrnamentsMapGroups</c>): a hat, mask or other New You accessory.</summary>
public class UnlockOrnamentsCommand : Command, ICommand
{
    private UnlockOrnamentsCommandDef Params;

    public UnlockOrnamentsCommand(UnlockOrnamentsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.OrnamentId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockOrnamentsCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockOrnamentsCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        PlayerRewards.Unlock(context, character, CharacterUnlocks.Ornaments, Params.OrnamentId);
        return true;
    }
}
