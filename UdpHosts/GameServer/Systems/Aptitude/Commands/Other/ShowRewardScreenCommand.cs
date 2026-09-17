using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Shows the reward screen with what the activation granted so far (the items its
///     GrantOwnerItem/SpawnLoot/UnpackItem nodes recorded on the context). Nothing granted, nothing shown.
/// </summary>
public class ShowRewardScreenCommand : Command, ICommand
{
    private ShowRewardScreenCommandDef Params;

    public ShowRewardScreenCommand(ShowRewardScreenCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.AwardedItems.Count == 0)
        {
            return true;
        }

        var message = PlayerRewards.BuildRewardScreen(context, Params.ScreenType, Params.TitleTextId);
        var shown = false;
        foreach (var target in context.Targets)
        {
            if (target is CharacterEntity { IsPlayerControlled: true } character)
            {
                shown |= PlayerRewards.SendToPlayer(character, message);
            }
        }

        if (!shown)
        {
            PlayerRewards.SendToPlayer(PlayerRewards.OwnerOf(context), message);
        }

        return true;
    }
}
