using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Unlocks CZI patterns (<c>dbvisualrecords::CziPattern</c>) for the character.</summary>
public class UnlockPatternsCommand : Command, ICommand
{
    private UnlockPatternsCommandDef Params;

    public UnlockPatternsCommand(UnlockPatternsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.PatternIds == null || Params.PatternIds.Length == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockPatternsCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockPatternsCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        foreach (var patternId in Params.PatternIds)
        {
            PlayerRewards.Unlock(context, character, CharacterUnlocks.Patterns, patternId);
        }

        return true;
    }
}
