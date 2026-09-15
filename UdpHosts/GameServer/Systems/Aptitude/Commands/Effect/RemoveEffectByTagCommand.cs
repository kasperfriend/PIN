using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Effect;

/// <summary>
///     <c>agsRemoveEffectByTagCommandDef</c>: removes "tagged" effects. The server-exclusive record
///     carries only an id - whatever tagging the live server used is not in the client database - so
///     the command is implemented as an informed no-op.
/// </summary>
/// <remarks>
///     The one chain this matters for is the 200 ms update chain of the root "interacting" effect 269
///     (chain 1154324): it re-scans the cone in front of the player, restores the interaction target
///     from the target stack when it is still around, and ends in this command. Tracing that chain
///     with the PIN target-stack semantics shows the tail only runs while the interaction target is
///     still in range - when the target wanders off, the chain stops one command earlier at
///     <c>HasTargetsDuration</c>. A literal "remove the interaction effect here" implementation would
///     therefore cancel every healthy interaction after 200 ms, so the command deliberately removes
///     nothing and reports success.
///     <para>
///     Interaction cancellation is covered by the rest of the machinery instead: releasing the E key
///     fails <c>ActivationDuration</c> in the duration chain of 269, dying fails
///     <c>RequireCState</c>, and either removal ends in <c>agsEndInteractionCommandDef</c> taking the
///     cancelled path.
///     </para>
/// </remarks>
public class RemoveEffectByTagCommand : Command, ICommand
{
    private RemoveEffectByTagCommandDef Params;

    public RemoveEffectByTagCommand(RemoveEffectByTagCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        Logger.Debug(
            "{Command} {CommandId} is a no-op: the aptgss record carries no tag to remove by",
            nameof(RemoveEffectByTagCommand),
            Params.Id);
        return true;
    }
}
