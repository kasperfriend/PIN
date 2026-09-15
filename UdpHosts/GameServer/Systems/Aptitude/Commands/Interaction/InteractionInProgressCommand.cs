using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Interaction;

/// <summary>
///     <c>agsInteractionInProgressCommandDef</c>: the duration-chain gate of every channelled
///     interaction effect (the per-type effects 273-279/2211/7168/7226-7228). It keeps the effect -
///     and with it the player's interaction channel - alive while the completion time recorded by
///     <see cref="InteractionCompletionTimeCommand" /> has not passed yet. The moment it returns
///     false the effect expires, its remove chain calls ability 181, which clears the root
///     "interacting" effect 269, whose own remove chain runs <see cref="EndInteractionCommand" />.
/// </summary>
public class InteractionInProgressCommand : Command, ICommand
{
    private InteractionInProgressCommandDef Params;

    public InteractionInProgressCommand(InteractionInProgressCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity { IsPlayerControlled: true } player)
        {
            // No recorded channel (it started without a completion time, or was already consumed)
            // means there is nothing to hold the effect open.
            return player.ActiveInteraction != null
                && !player.ActiveInteraction.IsCompleted(context.Shard.CurrentTime);
        }

        // Effects held by this check on non-players have no channel to time; keep them alive the
        // way the surrounding chains expect.
        return true;
    }
}
