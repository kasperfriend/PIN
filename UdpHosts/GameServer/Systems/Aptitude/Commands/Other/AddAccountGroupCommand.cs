using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     Puts the owner into an account group - VIP membership, an LGV rental contract - for the row's
///     duration (stacking extends the running membership) or permanently.
/// </summary>
public class AddAccountGroupCommand : Command, ICommand
{
    private AddAccountGroupCommandDef Params;

    public AddAccountGroupCommand(AddAccountGroupCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (string.IsNullOrWhiteSpace(Params.Group))
        {
            Logger.Warning("{Command} {CommandId} has no group authored yet (item {Item}); nothing changed", nameof(AddAccountGroupCommand), Params.Id, context.AbilityModuleId);
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

        ulong now = PlayerRewards.UnixNow();
        bool wasMember = character.Unlocks.IsInAccountGroup(Params.Group, now);
        if (!character.Unlocks.AddAccountGroup(Params.Group, Params.DurationSeconds, now))
        {
            return true;
        }

        if (!wasMember)
        {
            // A fresh membership goes with a failed activation; an extension of a running one is kept
            // (the contract that paid for it is refunded by the ConsumeItem rollback either way).
            context.ActivationRollbacks.Add(() => character.Unlocks.RemoveAccountGroup(Params.Group));
        }

        context.DirtyUnlocks.Add(character);
        Logger.Information("{Character} joined account group {Group} for {Duration}s", character, Params.Group, Params.DurationSeconds);
        return true;
    }
}
