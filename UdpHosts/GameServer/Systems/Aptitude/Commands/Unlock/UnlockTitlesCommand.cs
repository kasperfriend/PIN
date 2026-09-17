using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>
///     Unlocks a title (<c>dbcharacter::MonsterTitle</c>). A character without a title yet puts the new
///     one on straight away, so the unlock is visible; one already wearing a title keeps it.
/// </summary>
public class UnlockTitlesCommand : Command, ICommand
{
    private UnlockTitlesCommandDef Params;

    public UnlockTitlesCommand(UnlockTitlesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.TitleId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no title authored yet (item {Item}); nothing unlocked", nameof(UnlockTitlesCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        bool firstTime = !character.Unlocks.Has(CharacterUnlocks.Titles, Params.TitleId);
        PlayerRewards.Unlock(context, character, CharacterUnlocks.Titles, Params.TitleId);

        if (firstTime && character.StaticInfo.TitleId == 0)
        {
            character.SetTitle((ushort)Params.TitleId);
            context.ActivationRollbacks.Add(() => character.SetTitle(0));
        }

        return true;
    }
}
