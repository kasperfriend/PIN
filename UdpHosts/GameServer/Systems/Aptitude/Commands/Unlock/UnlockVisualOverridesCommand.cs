using GameServer.Data;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Unlock;

/// <summary>Unlocks a visual override (the <c>visual_overrides</c> unlock group).</summary>
public class UnlockVisualOverridesCommand : Command, ICommand
{
    private UnlockVisualOverridesCommandDef Params;

    public UnlockVisualOverridesCommand(UnlockVisualOverridesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.VisualOverrideId == 0)
        {
            Logger.Warning("{Command} {CommandId} has no unlock id authored yet (item {Item}); nothing unlocked", nameof(UnlockVisualOverridesCommand), Params.Id, context.AbilityModuleId);
            return true;
        }

        var character = PlayerRewards.OwnerOf(context);
        if (character == null)
        {
            Logger.Warning("{Command} {CommandId}: no player character to unlock for", nameof(UnlockVisualOverridesCommand), Params.Id);
            return false;
        }

        if (!PlayerRewards.ConsumeActivatingItem(context, character))
        {
            return false;
        }

        PlayerRewards.Unlock(context, character, CharacterUnlocks.VisualOverrides, Params.VisualOverrideId);
        return true;
    }
}
