using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Interaction;

/// <summary>
///     <c>agsInteractionCompletionTimeCommandDef</c>: records when the interaction the player just
///     started finishes. It runs inside the apply chain of every channelled interaction effect (the
///     per-type effects 273-279/2211/7168/7226-7228 applied by ability 187's dispatch), right after
///     <c>agsBeginInteractionCommandDef</c> announced the start on the root "interacting" effect 269.
///     The matching <see cref="InteractionInProgressCommand" /> in those effects' duration chains reads
///     the timestamp back: once past it, the effect expires, its remove chain calls ability 181, which
///     clears effect 269 and ends with <see cref="EndInteractionCommand" /> firing the interaction's
///     content.
/// </summary>
public class InteractionCompletionTimeCommand : Command, ICommand
{
    private InteractionCompletionTimeCommandDef Params;

    public InteractionCompletionTimeCommand(InteractionCompletionTimeCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity { IsPlayerControlled: true } player)
        {
            // The command also appears in chains that apply effects to non-players; there is no
            // channel to time there, so just let the chain continue.
            return true;
        }

        // The interaction target rode along from the root activation: ability 187 applies effect 269
        // with pass_targets, and the type dispatch hands the same target list to the per-type effect.
        BaseEntity target = null;
        if (context.Targets.TryPeek(out IAptitudeTarget peeked))
        {
            target = peeked as BaseEntity;
        }

        uint durationMs = target?.Interaction?.DurationMs ?? 0;
        uint now = context.Shard.CurrentTime;

        player.ActiveInteraction = new InteractionState
        {
            TargetEntityId = target?.EntityId ?? 0,
            StartTimeMs = now,
            CompletionTimeMs = now + durationMs,
        };

        Logger.Debug(
            "{Command} {CommandId}: player {Player} channels interaction on {Target} for {DurationMs}ms",
            nameof(InteractionCompletionTimeCommand),
            Params.Id,
            player,
            target,
            durationMs);

        return true;
    }
}
