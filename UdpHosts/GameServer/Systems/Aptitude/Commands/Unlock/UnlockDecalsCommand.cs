using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Unlocks a battleframe decal (<c>dbvisualrecords::TattooDecal</c>).</summary>
public class UnlockDecalsCommand : Command, ICommand
{
    private UnlockDecalsCommandDef Params;

    public UnlockDecalsCommand(UnlockDecalsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.DecalId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockDecalsCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockDecalsCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        PlayerRewards.Unlock(context, character, CharacterUnlocks.Decals, Params.DecalId);
        return true;
    }
}
