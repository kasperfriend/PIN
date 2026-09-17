using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Unlocks hairstyles / facial hair (<c>dbcharacter::HeadAccessory</c>, one id per sex where the style exists for both).</summary>
public class UnlockHeadAccessoriesCommand : Command, ICommand
{
    private UnlockHeadAccessoriesCommandDef Params;

    public UnlockHeadAccessoriesCommand(UnlockHeadAccessoriesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.HeadAccessoryIds == null || Params.HeadAccessoryIds.Length == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockHeadAccessoriesCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockHeadAccessoriesCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        foreach (var headAccessoryId in Params.HeadAccessoryIds)
        {
            PlayerRewards.Unlock(context, character, CharacterUnlocks.HeadAccessories, headAccessoryId);
        }

        return true;
    }
}
